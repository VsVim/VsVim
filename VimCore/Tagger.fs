namespace Vim

open Vim
open EditorUtils
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Tagging
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition
open System.Collections.ObjectModel
open System.Threading

/// Tagger for incremental searches
type IncrementalSearchTaggerSource (_vimBuffer : IVimBuffer) as this =

    let _search = _vimBuffer.IncrementalSearch
    let _textBuffer = _vimBuffer.TextBuffer
    let _globalSettings = _vimBuffer.GlobalSettings
    let _eventHandlers = DisposableBag()
    let _changed = StandardEvent()
    let mutable _searchSpan : ITrackingSpan option = None

    do 
        let raiseChanged () = _changed.Trigger this 

        let updateCurrentWithResult result = 
            _searchSpan <-
                match result with
                | SearchResult.Found (_, span, _) -> span.Snapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeExclusive) |> Some
                | SearchResult.NotFound _ -> None

        // When the search is updated we need to update the result.  Make sure to do so before raising 
        // the event.  The editor can and will call back into us synchronously and access a stale value
        // if we don't
        _search.CurrentSearchUpdated 
        |> Observable.subscribe (fun args ->

            updateCurrentWithResult args.SearchResult
            raiseChanged())
        |> _eventHandlers.Add

        // When the search is completed there is nothing left for us to tag.
        _search.CurrentSearchCompleted 
        |> Observable.subscribe (fun result ->
            _searchSpan <- None
            raiseChanged())
        |> _eventHandlers.Add

        // When the search is cancelled there is nothing left for us to tag.
        _search.CurrentSearchCancelled
        |> Observable.subscribe (fun result ->
            _searchSpan <- None
            raiseChanged())
        |> _eventHandlers.Add

        // We need to pay attention to the current IVimBuffer mode.  If it's any visual mode then we don't want
        // to highlight any spans.
        _vimBuffer.SwitchedMode
        |> Observable.subscribe (fun _ -> raiseChanged())
        |> _eventHandlers.Add

        // When the 'incsearch' setting is changed it impacts our tag display
        //
        // Up cast here to work around the F# bug which prevents accessing a CLIEvent from
        // a derived type
        (_globalSettings :> IVimSettings).SettingChanged 
        |> Observable.filter (fun args -> StringUtil.isEqual args.Setting.Name GlobalSettingNames.IncrementalSearchName)
        |> Observable.subscribe (fun _ -> raiseChanged())
        |> _eventHandlers.Add

    member x.GetTags span =

        if VisualKind.IsAnyVisual _vimBuffer.ModeKind || not _globalSettings.IncrementalSearch then 
            // If any of these are true then we shouldn't be displaying any tags
            ReadOnlyCollectionUtil.Empty
        else
            let snapshot = SnapshotSpanUtil.GetSnapshot span
            match _searchSpan |> Option.map (TrackingSpanUtil.GetSpan snapshot) |> OptionUtil.collapse with
            | None -> 
                // No search span or the search span doesn't map into the current ITextSnapshot so there
                // is nothing to return
                ReadOnlyCollectionUtil.Empty
            | Some span -> 
                // We have a span so return the tag
                let tag = TextMarkerTag(Constants.IncrementalSearchTagName)
                let tagSpan = TagSpan(span, tag) :> ITagSpan<TextMarkerTag>
                ReadOnlyCollectionUtil.Single tagSpan

    interface IBasicTaggerSource<TextMarkerTag> with
        member x.TextSnapshot = _textBuffer.CurrentSnapshot
        member x.GetTags span = x.GetTags span
        [<CLIEvent>]
        member x.Changed = _changed.Publish

    interface System.IDisposable with
        member x.Dispose() = _eventHandlers.DisposeAll()

[<Export(typeof<IViewTaggerProvider>)>]
[<ContentType(Constants.AnyContentType)>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
[<TagType(typeof<TextMarkerTag>)>]
type internal IncrementalSearchTaggerProvider
    [<ImportingConstructor>]
    (
        _vim : IVim,
        _taggerFactory : ITaggerFactory
    ) = 

    interface IViewTaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> (textView : ITextView, textBuffer) = 
            match textView.TextBuffer = textBuffer, _vim.GetVimBuffer textView with
            | false, _ ->
                null
            | true, None ->
                null
            | true, Some vimBuffer ->
                let taggerSource = new IncrementalSearchTaggerSource(vimBuffer)
                let tagger = _taggerFactory.CreateBasicTagger taggerSource
                tagger :> obj :?> ITagger<'T>

/// Tagger for completed incremental searches
type HighlightSearchTaggerSource
    ( 
        _textView : ITextView,
        _globalSettings : IVimGlobalSettings,
        _wordNav : ITextStructureNavigator,
        _search : ISearchService,
        _vimData : IVimData,
        _vimHost : IVimHost
    ) as this =

    let _textBuffer = _textView.TextBuffer
    let _changed = StandardEvent()
    let _eventHandlers = DisposableBag()

    /// Users can temporarily disable highlight search with the ':noh' command.  This is true while 
    /// we are in that mode
    let mutable _oneTimeDisabled = false

    /// Whether or not the ITextView is visible.  There is one setting which controls the tags for
    /// all ITextView's in the system.  It's very wasteful to raise tags changed when we are not 
    /// visible.  Many components call immediately back into GetTags even if the ITextView is not
    /// visible.  Even for an async tagger this can kill perf by the sheer work done in the 
    /// background.
    let mutable _isVisible = true

    do 
        let raiseChanged () = _changed.Trigger this

        let resetDisabledAndRaiseAllChanged () =
            _oneTimeDisabled <- false
            raiseChanged()

        // When the 'LastSearchData' property changes we want to reset the 'oneTimeDisabled' flag and 
        // begin highlighting again
        _vimData.LastPatternDataChanged
        |> Observable.subscribe (fun _ -> resetDisabledAndRaiseAllChanged() )
        |> _eventHandlers.Add

        // Make sure we respond to the HighlightSearchOneTimeDisabled event
        _vimData.HighlightSearchOneTimeDisabled
        |> Observable.subscribe (fun _ ->
            _oneTimeDisabled <- true
            raiseChanged())
        |> _eventHandlers.Add

        // When the setting is changed it also resets the one time disabled flag
        // Up cast here to work around the F# bug which prevents accessing a CLIEvent from
        // a derived type
        (_globalSettings :> IVimSettings).SettingChanged 
        |> Observable.filter (fun args -> StringUtil.isEqual args.Setting.Name GlobalSettingNames.HighlightSearchName)
        |> Observable.subscribe (fun _ -> resetDisabledAndRaiseAllChanged())
        |> _eventHandlers.Add

        _vimHost.IsVisibleChanged
        |> Observable.filter (fun textView -> textView = _textView)
        |> Observable.subscribe (fun _ -> 
            let isVisible = _vimHost.IsVisible _textView
            if _isVisible <> isVisible then
                _isVisible <- isVisible
                raiseChanged())
        |> _eventHandlers.Add

    /// Get the search information for a background 
    member x.GetDataForSpan() =
        // Build up the search information.  Don't need to wrap here as we just want
        // to consider the SnapshotSpan going forward
        let searchData = SearchData.OfPatternData _vimData.LastPatternData false
        { searchData with Kind = SearchKind.Forward }

    member x.GetTagsPrompt() = 
        let searchData = x.GetDataForSpan()
        if not _isVisible then
            Some List.empty
        elif StringUtil.isNullOrEmpty searchData.Pattern then
            // Nothing to give if there is no pattern
            Some List.empty
        elif not _globalSettings.HighlightSearch || _oneTimeDisabled then
            // Nothing to give if we are disabled
            Some List.empty
        else
            // There is a search pattern and we can't provide the data promptly
            None

    [<UsedInBackgroundThread>]
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
        |> ReadOnlyCollectionUtil.OfSeq

    interface IAsyncTaggerSource<SearchData, TextMarkerTag> with
        member x.Delay = NullableUtil.Create 100
        member x.TextSnapshot = _textBuffer.CurrentSnapshot
        member x.TextViewOptional = _textView
        member x.GetDataForSpan _ = x.GetDataForSpan()
        member x.GetTagsInBackground(searchData, span, cancellationToken) = HighlightSearchTaggerSource.GetTagsInBackground _search _wordNav searchData span cancellationToken
        member x.TryGetTagsPrompt(_, value : byref<ITagSpan<TextMarkerTag> seq>) =
            match x.GetTagsPrompt() with
            | None -> 
                value <- Seq.empty
                false
            | Some tagList ->
                value <- tagList
                true
        [<CLIEvent>]
        member x.Changed = _changed.Publish

    interface System.IDisposable with
        member x.Dispose() = _eventHandlers.DisposeAll()

[<Export(typeof<IViewTaggerProvider>)>]
[<ContentType(Constants.AnyContentType)>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
[<TagType(typeof<TextMarkerTag>)>]
type HighlightIncrementalSearchTaggerProvider
    [<ImportingConstructor>]
    ( 
        _vim : IVim,
        _taggerFactory : ITaggerFactory
    ) = 

    let _key = obj()

    interface IViewTaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> ((textView : ITextView), textBuffer) = 
            match textView.TextBuffer = textBuffer, _vim.GetVimBuffer textView with
            | false, _ ->
                null
            | true, None -> 
                null
            | true, Some vimTextBuffer ->
                let tagger = _taggerFactory.CreateAsyncTaggerCounted(_key, textView.Properties, fun () ->
                    let wordNavigator = vimTextBuffer.WordNavigator
                    let taggerSource = new HighlightSearchTaggerSource(textView, vimTextBuffer.GlobalSettings, wordNavigator, _vim.SearchService, _vim.VimData, _vim.VimHost)
                    taggerSource :> IAsyncTaggerSource<SearchData, TextMarkerTag>)
                tagger :> obj :?> ITagger<'T>

/// Tagger for matches as they appear during a confirm substitute
type SubstituteConfirmTaggerSource
    ( 
        _textBuffer : ITextBuffer,
        _mode : ISubstituteConfirmMode
    ) as this =

    let _changed = StandardEvent()
    let _eventHandlers = DisposableBag()
    let mutable _currentMatch : SnapshotSpan option = None

    do 
        let raiseChanged () = _changed.Trigger this

        _mode.CurrentMatchChanged
        |> Observable.subscribe (fun data -> 
            _currentMatch <- data
            raiseChanged())
        |> _eventHandlers.Add

    member x.GetTags span = 
        match _currentMatch with
        | Some currentMatch -> 
            let tag = TextMarkerTag(Constants.HighlightIncrementalSearchTagName)
            let tagSpan = TagSpan(currentMatch, tag) :> ITagSpan<TextMarkerTag>
            ReadOnlyCollectionUtil.Single tagSpan
        | None -> 
            ReadOnlyCollectionUtil.Empty

    interface IBasicTaggerSource<TextMarkerTag> with
        member x.TextSnapshot = _textBuffer.CurrentSnapshot
        member x.GetTags span = x.GetTags span
        [<CLIEvent>]
        member x.Changed = _changed.Publish

    interface System.IDisposable with
        member x.Dispose() = _eventHandlers.DisposeAll()

[<Export(typeof<IViewTaggerProvider>)>]
[<ContentType(Constants.AnyContentType)>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
[<TagType(typeof<TextMarkerTag>)>]
type SubstituteConfirmTaggerProvider
    [<ImportingConstructor>]
    ( 
        _vim : IVim,
        _taggerFactory : ITaggerFactory
    ) = 

    interface IViewTaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> ((textView : ITextView), textBuffer) = 
            match textView.TextBuffer = textBuffer, _vim.GetVimBuffer textView with
            | false, _ ->
                null
            | true, None -> 
                null
            | true, Some buffer ->
                let taggerSource = new SubstituteConfirmTaggerSource(textBuffer, buffer.SubstituteConfirmMode)
                let tagger = _taggerFactory.CreateBasicTagger(taggerSource)
                tagger :> obj :?> ITagger<'T>

/// Fold tagger for the IOutliningRegion tags created by folds.  Note that folds work
/// on an ITextBuffer level and not an ITextView level.  Hence this works directly with
/// IFoldManagerData instead of IFoldManager
type internal FoldTaggerSource(_foldData : IFoldData) as this =

    let _textBuffer = _foldData.TextBuffer
    let _changed = new DelegateEvent<System.EventHandler>()

    do 
        _foldData.FoldsUpdated 
        |> Event.add (fun _ -> _changed.Trigger([| this; System.EventArgs.Empty |]))

    member x.GetTags span =

        // Get the description for the given SnapshotSpan.  This is the text displayed for
        // the folded lines.
        let getDescription span = 
            let startLine, lastLine = SnapshotSpanUtil.GetStartAndLastLine span
            sprintf "%d lines ---" ((lastLine.LineNumber - startLine.LineNumber) + 1)

        let snapshot = SnapshotSpanUtil.GetSnapshot span
        _foldData.Folds
        |> Seq.filter ( fun span -> span.Snapshot = snapshot )
        |> Seq.map (fun span ->
            let description = getDescription span
            let hint = span.GetText()
            let tag = OutliningRegionTag(true, true, description, hint)
            TagSpan<OutliningRegionTag>(span, tag) :> ITagSpan<OutliningRegionTag> )
        |> ReadOnlyCollectionUtil.OfSeq

    interface IBasicTaggerSource<OutliningRegionTag> with
        member x.TextSnapshot = _textBuffer.CurrentSnapshot
        member x.GetTags span = x.GetTags span
        [<CLIEvent>]
        member x.Changed = _changed.Publish

    interface System.IDisposable with
        member x.Dispose() = ()

[<Export(typeof<ITaggerProvider>)>]
[<ContentType(Constants.AnyContentType)>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
[<TagType(typeof<OutliningRegionTag>)>]
type FoldTaggerProvider
    [<ImportingConstructor>]
    (
        _factory : IFoldManagerFactory,
        _taggerFactory : ITaggerFactory
    ) =

    let _key = obj()

    interface ITaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> textBuffer =
            let tagger = _taggerFactory.CreateBasicTaggerCounted(_key, textBuffer.Properties, fun() ->
                let foldData = _factory.GetFoldData textBuffer
                let taggerSource = new FoldTaggerSource(foldData)
                taggerSource :> IBasicTaggerSource<OutliningRegionTag>)
            tagger :> obj :?> ITagger<'T>


