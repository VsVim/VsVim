namespace Vim

open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Tagging
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition
open System.Threading
open System.Threading.Tasks

/// A tagger source for asynchronous taggers.  Methods on this interface can and will
/// be called on any thread
type IAsyncTaggerSource<'TData, 'TTag when 'TTag :> ITag> = 

    inherit System.IDisposable

    /// The current Snapshot.  
    ///
    /// Called from the main thread only
    abstract TextSnapshot : ITextSnapshot

    /// To prevent needless spawning of Task<T> values the async tagger has the option
    /// of providing prompt data.  This method should only be used when determination
    /// of the tokens requires no calculation.  It will be called on the main thread
    ///
    /// Called from the main thread only
    abstract GetTagsPrompt : span : SnapshotSpan -> ITagSpan<'TTag> list option

    /// This method is called to gather data on the UI thread which will then be passed
    /// down to the background thread for processing
    ///
    /// Called from the main thread only
    abstract GetDataForSpan : span : SnapshotSpan -> 'TData

    /// Return the applicable tags for the given SnapshotSpan instance.  This will be
    /// called on a background thread and should respect the provided CancellationToken
    abstract GetTagsInBackground : data : 'TData -> span : SnapshotSpan -> cancellationToken : CancellationToken -> ITagSpan<'TTag> list

    /// Raised by the source when the tags have changed for the given SnapshotSpan
    [<CLIEvent>]
    abstract TagsChanged : IEvent<SnapshotSpan option>

type TagCache<'TTag when 'TTag :> ITag> = {

    Span : SnapshotSpan

    TagList : ITagSpan<'TTag> list
}

/// Data about a background request for tags
type AsyncBackgroundRequest = {

    Span : SnapshotSpan

    CancellationTokenSource : CancellationTokenSource

    Task : Task
}

type AsyncTagger<'TData, 'TTag when 'TTag :> ITag>
    (
        _asyncTaggerSource : IAsyncTaggerSource<'TData, 'TTag>
    ) as this =

    /// Number of lines on either side of a request for which we will cache background
    /// data
    [<Literal>]
    static let LineAdjustment = 40

    let _tagsChanged = new Event<System.EventHandler<SnapshotSpanEventArgs>, SnapshotSpanEventArgs>()
    let _eventHandlers = DisposableBag()

    /// The one and only active AsyncBackgroundRequest instance.  There can be several
    /// in flight at once.  But we will cancel the earlier ones should a new one be 
    /// requested
    let mutable _asyncBackgroundRequest : AsyncBackgroundRequest option = None

    /// Cache of ITag values for a given SnapshotSpan
    let mutable _tagCache : TagCache<'TTag> option = None

    do 
        _asyncTaggerSource.TagsChanged 
        |> Observable.subscribe this.OnTagsChanged
        |> _eventHandlers.Add

    member x.TagCache 
        with get() = _tagCache
        and set value = _tagCache <- value

    member x.AsyncBackgroundRequest 
        with get() = _asyncBackgroundRequest
        and set value = _asyncBackgroundRequest <- value

    /// Get the tags for the specified NormalizedSnapshotSpanCollection.  Use the cache if 
    /// possible and possibly go to the background if necessary
    member x.GetTags (col : NormalizedSnapshotSpanCollection) = 
        let span = NormalizedSnapshotSpanCollectionUtil.GetCombinedSpan col

        let tagList = 

            // First try and see if the tagger can provide prompt data.  We want to avoid 
            // creating Task<T> instances if possible.  
            match _asyncTaggerSource.GetTagsPrompt span with
            | Some tagList -> tagList
            | None -> 

                // Next step is to hit the cache
                match x.GetTagsFromCache span with
                | Some tagList -> tagList
                | None -> 

                    // No choice now but to grab the tags from a background thread.  Schedule it
                    // now and return an empty list to the caller
                    x.GetTagsInBackground span
                    List.empty

        // Now filter the set of returned ITagSpan values to those which are part of the 
        // requested NormalizedSnapshotSpanCollection.  The cache lookups don't dig down and 
        // instead return all available tags.  We filter down the collection here to what's 
        // necessary.
        tagList
        |> Seq.filter (fun tagSpan -> tagSpan.Span.IntersectsWith(span))

    /// Get the tags from the cache.
    member x.GetTagsFromCache (span : SnapshotSpan) = 
        match _tagCache with
        | None -> None
        | Some tagCache ->
            let cachedSpan = tagCache.Span
            if cachedSpan.Snapshot = span.Snapshot && cachedSpan.Contains(span) then
                Some tagCache.TagList
            else
                // TODO: Map the cache to the requested snapshot and kick off a 
                // background request
                None

    /// Get the tags for the specified SnapshotSpan in a background task
    member x.GetTagsInBackground (span : SnapshotSpan) = 

        let startRequest (synchronizationContext : SynchronizationContext) = 
            // We proactively look for more than the requested SnapshotSpan of data.  Extend the
            // line count by the expected value
            let span =
                let startLine, endLine = SnapshotSpanUtil.GetStartAndEndLine span
                let startLine = 
                    let number = max 0 (startLine.LineNumber - LineAdjustment)
                    SnapshotUtil.GetLine span.Snapshot number

                let endLine = 
                    let number = endLine.LineNumber + LineAdjustment
                    SnapshotUtil.GetLineOrLast span.Snapshot number

                SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak)

            // Create the data which is needed by the 
            let data = _asyncTaggerSource.GetDataForSpan span
            let cancellationTokenSource = new CancellationTokenSource()
            let cancellationToken = cancellationTokenSource.Token

            // Function which finally gets the tags.  This is run on a background thread and can
            // throw as the implementor is encouraged to use CancellationToken::ThrowIfCancelled
            let getTags () = 

                let tagList = 
                    try
                        _asyncTaggerSource.GetTagsInBackground data span cancellationToken
                    with
                        _ -> List.Empty
                synchronizationContext.Post((fun _ -> x.OnTagsInBackgroundComplete cancellationTokenSource span tagList), null)

            let task = new Task(getTags, cancellationToken)
            let asyncBackgroundRequest = {
                Span = span
                CancellationTokenSource = cancellationTokenSource
                Task = task }
            _asyncBackgroundRequest <- Some asyncBackgroundRequest

            // Now kick off the request
            task.Start()

        let synchronizationContext = SynchronizationContext.Current
        if synchronizationContext <> null then
            match _asyncBackgroundRequest with
            | None -> startRequest synchronizationContext
            | Some asyncBackgroundRequest ->

                // Don't spawn a new task if the existing one is a superset of the current request
                if span.Start.Position < asyncBackgroundRequest.Span.Start.Position ||
                   span.End.Position > asyncBackgroundRequest.Span.End.Position ||
                   span.Snapshot <> asyncBackgroundRequest.Span.Snapshot then

                   x.CancelAsyncBackgroundRequest()
                   startRequest synchronizationContext

    /// Cancel the pending AsyncBackgoundRequest if one is currently running
    member x.CancelAsyncBackgroundRequest() =
        match _asyncBackgroundRequest with 
        | None -> ()
        | Some asyncBackgroundRequest ->
            try
                asyncBackgroundRequest.CancellationTokenSource.Cancel()
            with 
                _ -> ()
            _asyncBackgroundRequest <- None

    member x.Dispose() =
        _asyncTaggerSource.Dispose()
        _eventHandlers.DisposeAll()

    member x.RaiseTagsChanged span =
        _tagsChanged.Trigger(x, SnapshotSpanEventArgs(span))

    /// Called when the IAsyncTaggerSource raises a TagsChanged event.  Clear out the 
    /// cache, pass on the event to the ITagger and wait for the next request
    member x.OnTagsChanged span =   

        // Calculate the span we should raise as the changed Snapshotspan.  It needs 
        // to be the combined span which changed in the source and that for which 
        // we have given out tag information for
        let span = 
            match span, _tagCache |> Option.map (fun tagCache -> tagCache.Span) with
            | None, None -> SnapshotSpan(_asyncTaggerSource.TextSnapshot, 0, 0)
            | Some span, None -> span
            | None, Some span -> span
            | Some left, Some right ->
                let startPoint = SnapshotPointUtil.OrderAscending left.Start right.Start |> fst
                let endPoint = SnapshotPointUtil.OrderAscending left.End right.End |> snd
                SnapshotSpan(startPoint, endPoint)

        // Clear out the cache.  The underlying source was invalidated
        _tagCache <- None
        x.CancelAsyncBackgroundRequest()
        x.RaiseTagsChanged span

    /// Called on the main thread when the request for tags completes.
    member x.OnTagsInBackgroundComplete cancellationTokenSource span tagList = 
        match _asyncBackgroundRequest with
        | None -> ()
        | Some asyncBackgroundRequest ->
            if asyncBackgroundRequest.CancellationTokenSource = cancellationTokenSource then
                // This is the active request that completed.  Update the cache and notify the
                // caller our tags have changed
                _tagCache <- { 
                    Span = span
                    TagList = tagList } |> Some
                _asyncBackgroundRequest <- None

                // This is called directly on the message pump.  Protect against Dispose and event
                // raising from taking down the process
                try 
                    x.RaiseTagsChanged span
                    asyncBackgroundRequest.CancellationTokenSource.Dispose()
                with 
                    _ -> ()

    interface ITagger<'TTag> with
        member x.GetTags col = x.GetTags col
        [<CLIEvent>]
        member x.TagsChanged = _tagsChanged.Publish

    interface System.IDisposable with
        member x.Dispose() = x.Dispose()

/// Tagger for incremental searches
type IncrementalSearchTagger (_vimBuffer : IVimBuffer) as this =

    let _search = _vimBuffer.IncrementalSearch
    let _textBuffer = _vimBuffer.TextBuffer
    let _globalSettings = _vimBuffer.GlobalSettings
    let _eventHandlers = DisposableBag()
    let _tagsChanged = new Event<System.EventHandler<SnapshotSpanEventArgs>, SnapshotSpanEventArgs>()
    let mutable _searchSpan : ITrackingSpan option = None

    do 
        let raiseAllChanged () = 
            // Don't bother calculating the range of changed spans.  Simply raise the event for the entire 
            // buffer.  The editor will only call back for visible spans
            let snapshot = _textBuffer.CurrentSnapshot
            let extent = SnapshotUtil.GetExtent snapshot
            _tagsChanged.Trigger (this, SnapshotSpanEventArgs(extent))

        let updateCurrentWithResult result = 
            _searchSpan <-
                match result with
                | SearchResult.Found (_, span, _) -> span.Snapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeExclusive) |> Some
                | SearchResult.NotFound _ -> None

        // When the search is updated we need to update the result.  Make sure to do so before raising 
        // the event.  The editor can and will call back into us synchronously and access a stale value
        // if we don't
        _search.CurrentSearchUpdated 
        |> Observable.subscribe (fun result ->

            updateCurrentWithResult result
            raiseAllChanged())
        |> _eventHandlers.Add

        // When the search is completed there is nothing left for us to tag.
        _search.CurrentSearchCompleted 
        |> Observable.subscribe (fun result ->
            _searchSpan <- None
            raiseAllChanged())
        |> _eventHandlers.Add

        // When the search is cancelled there is nothing left for us to tag.
        _search.CurrentSearchCancelled
        |> Observable.subscribe (fun result ->
            _searchSpan <- None
            raiseAllChanged())
        |> _eventHandlers.Add

        // We need to pay attention to the current IVimBuffer mode.  If it's any visual mode then we don't want
        // to highlight any spans.
        _vimBuffer.SwitchedMode
        |> Observable.subscribe (fun _ -> raiseAllChanged())
        |> _eventHandlers.Add

        // When the 'incsearch' setting is changed it impacts our tag display
        //
        // Up cast here to work around the F# bug which prevents accessing a CLIEvent from
        // a derived type
        (_globalSettings :> IVimSettings).SettingChanged 
        |> Observable.filter (fun args -> StringUtil.isEqual args.Name GlobalSettingNames.IncrementalSearchName)
        |> Observable.subscribe (fun _ -> raiseAllChanged())
        |> _eventHandlers.Add

    member x.GetTags (col : NormalizedSnapshotSpanCollection) =

        if col.Count = 0 || VisualKind.IsAnyVisual _vimBuffer.ModeKind || not _globalSettings.IncrementalSearch then 
            // If any of these are true then we shouldn't be displaying any tags
            Seq.empty
        else
            let snapshot = col.[0].Snapshot
            match _searchSpan |> Option.map (TrackingSpanUtil.GetSpan snapshot) |> OptionUtil.collapse with
            | None -> 
                // No search span or the search span doesn't map into the current ITextSnapshot so there
                // is nothing to return
                Seq.empty
            | Some span -> 
                // We have a span so return the tag
                let tag = TextMarkerTag(Constants.IncrementalSearchTagName)
                let tagSpan = TagSpan(span, tag) :> ITagSpan<TextMarkerTag>
                [ tagSpan ] |> Seq.ofList

    interface ITagger<TextMarkerTag> with
        member x.GetTags col = x.GetTags col
        [<CLIEvent>]
        member x.TagsChanged = _tagsChanged.Publish

    interface System.IDisposable with
        member x.Dispose() = _eventHandlers.DisposeAll()

[<Export(typeof<IViewTaggerProvider>)>]
[<ContentType(Constants.AnyContentType)>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
[<TagType(typeof<TextMarkerTag>)>]
type internal IncrementalSearchTaggerProvider
    [<ImportingConstructor>]
    ( _vim : IVim ) = 

    interface IViewTaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> (textView : ITextView, textBuffer) = 
            match textView.TextBuffer = textBuffer, _vim.GetVimBuffer textView with
            | false, _ ->
                null
            | true, None ->
                null
            | true, Some vimBuffer ->
                let tagger = new IncrementalSearchTagger(vimBuffer)
                tagger :> obj :?> ITagger<'T>

/// Tagger for completed incremental searches
type HighlightSearchTaggerSource
    ( 
        _textBuffer : ITextBuffer,
        _settings : IVimGlobalSettings,
        _wordNav : ITextStructureNavigator,
        _search : ISearchService,
        _vimData : IVimData
    ) =

    let _tagsChanged = new Event<SnapshotSpan option>()
    let _eventHandlers = DisposableBag()

    /// Users can temporarily disable highlight search with the ':noh' command.  This is true while 
    /// we are in that mode
    let mutable _oneTimeDisabled = false

    do 
        let raiseAllChanged () = 

            // We have no specific knowledge about what changed hence use none
            _tagsChanged.Trigger None

        let resetDisabledAndRaiseAllChanged () =
            _oneTimeDisabled <- false
            raiseAllChanged()

        // When the 'LastSearchData' property changes we want to reset the 'oneTimeDisabled' flag and 
        // begin highlighting again
        _vimData.LastPatternDataChanged
        |> Observable.subscribe (fun _ -> resetDisabledAndRaiseAllChanged() )
        |> _eventHandlers.Add

        // Make sure we respond to the HighlightSearchOneTimeDisabled event
        _vimData.HighlightSearchOneTimeDisabled
        |> Observable.subscribe (fun _ ->
            _oneTimeDisabled <- true
            raiseAllChanged())
        |> _eventHandlers.Add

        // When the setting is changed it also resets the one time disabled flag
        // Up cast here to work around the F# bug which prevents accessing a CLIEvent from
        // a derived type
        (_settings :> IVimSettings).SettingChanged 
        |> Observable.filter (fun args -> StringUtil.isEqual args.Name GlobalSettingNames.HighlightSearchName)
        |> Observable.subscribe (fun _ -> resetDisabledAndRaiseAllChanged())
        |> _eventHandlers.Add

    /// Get the search information for a background 
    member x.GetDataForSpan() =

        // Build up the search information.  Don't need to wrap here as we just want
        // to consider the SnapshotSpan going forward
        let searchData = SearchData.OfPatternData _vimData.LastPatternData false
        { searchData with Kind = SearchKind.Forward }

    member x.GetTagsPrompt() = 
        let searchData = x.GetDataForSpan()
        if StringUtil.isNullOrEmpty searchData.Pattern then
            // Nothing to give if there is no pattern
            Some List.empty
        elif not _settings.HighlightSearch || _oneTimeDisabled then
            // Nothing to give if we are disabled
            Some List.empty
        else
            // There is a search pattern and we can't provide the data promptly
            None

    static member GetTagsInBackground (searchService : ISearchService) wordNavigator searchData span (cancellationToken : CancellationToken) = 

        let withSpan (span : SnapshotSpan) =  
            span.Start
            |> Seq.unfold (fun point -> 

                cancellationToken.ThrowIfCancellationRequested()

                if SnapshotPointUtil.IsEndPoint point then
                    // If this is the end point of the ITextBuffer then we are done
                    None
                else
                    match searchService.FindNext searchData point wordNavigator with
                    | SearchResult.NotFound _ -> 
                        None
                    | SearchResult.Found (_, foundSpan, _) ->

                        // It's possible for the SnapshotSpan here to be a 0 length span since Regex expressions
                        // can match 0 width text.  For example "\|i\>" from issue #480.  Vim handles this by 
                        // treating the 0 width match as a 1 width match. 
                        let foundSpan = 
                            if foundSpan.Length = 0 then
                                SnapshotSpan(foundSpan.Start, 1)
                            else
                                foundSpan

                        // Don't continue searching once we pass the end of the SnapshotSpan we are searching
                        if foundSpan.Start.Position <= span.End.Position then 
                            Some(foundSpan, foundSpan.End)
                        else 
                            None)

        let tag = TextMarkerTag(Constants.HighlightIncrementalSearchTagName)
        withSpan span
        |> Seq.map (fun span -> TagSpan(span,tag) :> ITagSpan<TextMarkerTag> )
        |> List.ofSeq

    interface IAsyncTaggerSource<SearchData, TextMarkerTag> with
        member x.TextSnapshot = _textBuffer.CurrentSnapshot
        member x.GetDataForSpan _ = x.GetDataForSpan()
        member x.GetTagsPrompt _ = x.GetTagsPrompt()
        member x.GetTagsInBackground searchData span cancellationToken = HighlightSearchTaggerSource.GetTagsInBackground _search _wordNav searchData span cancellationToken
        [<CLIEvent>]
        member x.TagsChanged = _tagsChanged.Publish

    interface System.IDisposable with
        member x.Dispose() = _eventHandlers.DisposeAll()

[<Export(typeof<ITaggerProvider>)>]
[<ContentType(Constants.AnyContentType)>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
[<TagType(typeof<TextMarkerTag>)>]
type HighlightIncrementalSearchTaggerProvider
    [<ImportingConstructor>]
    ( _vim : IVim ) = 

    interface ITaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> textBuffer = 
            match _vim.GetVimTextBuffer textBuffer with
            | None ->
                null
            | Some vimTextBuffer ->
                let wordNavigator = vimTextBuffer.WordNavigator
                let asyncTaggerSource = new HighlightSearchTaggerSource(textBuffer, vimTextBuffer.GlobalSettings, wordNavigator, _vim.SearchService, _vim.VimData)
                let asyncTagger = new AsyncTagger<SearchData, TextMarkerTag>(asyncTaggerSource)
                asyncTagger :> obj :?> ITagger<'T>

/// Tagger for matches as they appear during a confirm substitute
type SubstituteConfirmTagger
    ( 
        _textBuffer : ITextBuffer,
        _mode : ISubstituteConfirmMode) as this =

    let _tagsChanged = new Event<System.EventHandler<SnapshotSpanEventArgs>, SnapshotSpanEventArgs>()
    let _eventHandlers = DisposableBag()
    let mutable _currentMatch : SnapshotSpan option = None

    do 
        let raiseAllChanged () = 
            // Don't bother calculating the range of changed spans.  Simply raise the event for the entire 
            // buffer.  The editor will only call back for visible spans
            let snapshot = _textBuffer.CurrentSnapshot
            let allSpan = SnapshotSpan(snapshot, 0, snapshot.Length)
            _tagsChanged.Trigger (this,SnapshotSpanEventArgs(allSpan))

        _mode.CurrentMatchChanged
        |> Observable.subscribe (fun data -> 
            _currentMatch <- data
            raiseAllChanged())
        |> _eventHandlers.Add

    member x.GetTags (col:NormalizedSnapshotSpanCollection) = 
        match _currentMatch with
        | Some(currentMatch) -> 
            let tag = TextMarkerTag(Constants.HighlightIncrementalSearchTagName)
            let span = TagSpan(currentMatch, tag) :> ITagSpan<TextMarkerTag>
            span |> Seq.singleton
        | None -> Seq.empty

    interface ITagger<TextMarkerTag> with
        member x.GetTags col = x.GetTags col
        [<CLIEvent>]
        member x.TagsChanged = _tagsChanged.Publish

    interface System.IDisposable with
        member x.Dispose() = _eventHandlers.DisposeAll()

[<Export(typeof<IViewTaggerProvider>)>]
[<ContentType(Constants.AnyContentType)>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
[<TagType(typeof<TextMarkerTag>)>]
type SubstituteConfirmTaggerProvider
    [<ImportingConstructor>]
    ( _vim : IVim ) = 

    interface IViewTaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> ((textView : ITextView), textBuffer) = 
            match textView.TextBuffer = textBuffer, _vim.GetVimBuffer textView with
            | false, _ ->
                null
            | true, None -> 
                null
            | true, Some buffer ->
                let tagger = new SubstituteConfirmTagger(textBuffer, buffer.SubstituteConfirmMode)
                tagger :> obj :?> ITagger<'T>
