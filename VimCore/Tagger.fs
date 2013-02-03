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
        [<EditorUtilsImport>] _taggerFactory : ITaggerFactory
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
                let tagger = _taggerFactory.CreateBasicTaggerRaw taggerSource
                tagger :> obj :?> ITagger<'T>

type HighlightSearchData = {
    Pattern : string
    VimRegexOptions : VimRegexOptions
}

/// Tagger for completed incremental searches
type HighlightSearchTaggerSource
    ( 
        _textView : ITextView,
        _globalSettings : IVimGlobalSettings,
        _vimData : IVimData,
        _vimHost : IVimHost
    ) as this =

    let _textBuffer = _textView.TextBuffer
    let _changed = StandardEvent()
    let _eventHandlers = DisposableBag()

    /// Whether or not we are currently supressing tags because the user executed the 
    /// :nohl command
    let mutable _oneTimeDisabled = false

    /// Whether or not the ITextView is visible.  There is one setting which controls the tags for
    /// all ITextView's in the system.  It's very wasteful to raise tags changed when we are not 
    /// visible.  Many components call immediately back into GetTags even if the ITextView is not
    /// visible.  Even for an async tagger this can kill perf by the sheer work done in the 
    /// background.
    let mutable _isVisible = true

    do
        let lastIsProvidingTags = ref this.IsProvidingTags
        let lastPatternData = ref _vimData.LastPatternData

        // If anything around how we display tags has changed then need to raise the Changed event
        let maybeRaiseChanged () = 
            if lastIsProvidingTags.Value <> this.IsProvidingTags || lastPatternData.Value <> _vimData.LastPatternData then
                lastPatternData := _vimData.LastPatternData
                lastIsProvidingTags := this.IsProvidingTags

                _changed.Trigger this

        // When the 'LastSearchData' property changes we want to reset the 'oneTimeDisabled' flag and 
        // begin highlighting again
        _vimData.SearchRan
        |> Observable.subscribe (fun _ -> 
            _oneTimeDisabled <- false
            maybeRaiseChanged())
        |> _eventHandlers.Add

        // Make sure we respond to the HighlightSearchOneTimeDisabled event
        _vimData.HighlightSearchOneTimeDisabled
        |> Observable.subscribe (fun _ ->
            _oneTimeDisabled <- true
            maybeRaiseChanged())
        |> _eventHandlers.Add

        // When the setting is changed it also resets the one time disabled flag
        // Up cast here to work around the F# bug which prevents accessing a CLIEvent from
        // a derived type
        (_globalSettings :> IVimSettings).SettingChanged 
        |> Observable.filter (fun args -> StringUtil.isEqual args.Setting.Name GlobalSettingNames.HighlightSearchName)
        |> Observable.subscribe (fun _ -> 
            _oneTimeDisabled <- false
            maybeRaiseChanged())
        |> _eventHandlers.Add

        _vimHost.IsVisibleChanged
        |> Observable.filter (fun args -> args.TextView = _textView)
        |> Observable.subscribe (fun _ -> 
            _isVisible <- _vimHost.IsVisible _textView
            maybeRaiseChanged())
        |> _eventHandlers.Add

    /// Whether or not we are currently providing any tags.  Tags are surpressed in a
    /// number of scenarios
    member x.IsProvidingTags = 
        not _oneTimeDisabled &&
        _isVisible &&
        _globalSettings.HighlightSearch

    /// Get the search information for a background 
    member x.GetDataForSnapshot() =  

        let highlightSearchData = {
            Pattern = _vimData.LastPatternData.Pattern
            VimRegexOptions = VimRegexFactory.CreateRegexOptions _globalSettings
        }

        highlightSearchData

    member x.GetTagsPrompt() = 
        let searchData = x.GetDataForSnapshot()
        if not x.IsProvidingTags then
            // Not currently providing any tags.  Return an empty set here for any requests
            Some List.empty
        elif StringUtil.isNullOrEmpty searchData.Pattern then
            // Nothing to give if there is no pattern
            Some List.empty
        else
            // There is a search pattern and we can't provide the data promptly because it would
            // negatively impact performance.  Let the request migrate to the background
            None

    [<UsedInBackgroundThread>]
    static member GetTagsInBackground (highlightSearchData : HighlightSearchData) span (cancellationToken : CancellationToken) = 

        if StringUtil.isNullOrEmpty highlightSearchData.Pattern then
            ReadOnlyCollectionUtil.Empty
        else

            // Note: We specifically avoid using ITextSearchService here.  It's a very inefficient search and 
            // allocates a lot of memory (the FindAll function will bring the contents of the entire 
            // ITextSnapshot into memory even if you are only searching a single line 

            let spans = 
                match VimRegexFactory.Create highlightSearchData.Pattern highlightSearchData.VimRegexOptions with
                | None -> Seq.empty
                | Some vimRegex ->
                    try
                        let snapshot = SnapshotSpanUtil.GetSnapshot span
                        let text = SnapshotSpanUtil.GetText span
                        let offset = span |> SnapshotSpanUtil.GetStartPoint |> SnapshotPointUtil.GetPosition
                        let collection = vimRegex.Regex.Matches(text)

                        if collection.Count >= span.Length && collection |> Seq.cast<System.Text.RegularExpressions.Match> |> Seq.forall (fun m -> m.Length = 0) then
                            // If the user enters a regex which has a 0 match for every character in the 
                            // ITextBuffer then we special case this and just tag everything.  If we don't
                            // then the user can essentially hang Visual Studio in a big file by searching 
                            // for say '\|i'.  
                            [span] |> Seq.ofList
                        else
                            collection
                            |> Seq.cast<System.Text.RegularExpressions.Match>
                            |> Seq.map (fun m -> 
                                let start = m.Index + offset
    
                                // It's possible for the SnapshotSpan here to be a 0 length span since Regex expressions
                                // can match 0 width text.  For example "\|i\>" from issue #480.  Vim handles this by 
                                // treating the 0 width match as a 1 width match. 
                                let length = 
                                    if m.Length = 0 && start < snapshot.Length then 
                                        1
                                    else 
                                        m.Length
    
                                SnapshotSpan(snapshot, start, length))
                    with 
                    | :? System.InvalidOperationException ->
                        // Happens when we provide an invalid regular expression.  Just return empty list
                        Seq.empty

            let tag = TextMarkerTag(Constants.HighlightIncrementalSearchTagName)
            spans
            |> Seq.map (fun span -> TagSpan(span,tag) :> ITagSpan<TextMarkerTag> )
            |> ReadOnlyCollectionUtil.OfSeq

    interface IAsyncTaggerSource<HighlightSearchData, TextMarkerTag> with
        member x.Delay = NullableUtil.Create 100
        member x.TextSnapshot = _textBuffer.CurrentSnapshot
        member x.TextViewOptional = _textView
        member x.GetDataForSnapshot _ = x.GetDataForSnapshot()
        member x.GetTagsInBackground(highlightSearchData, span, cancellationToken) = HighlightSearchTaggerSource.GetTagsInBackground highlightSearchData span cancellationToken
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
        [<EditorUtilsImport>] _taggerFactory : ITaggerFactory
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
                let tagger = _taggerFactory.CreateAsyncTagger(textView.Properties, _key, fun () ->
                    let wordNavigator = vimTextBuffer.WordNavigator
                    let taggerSource = new HighlightSearchTaggerSource(textView, vimTextBuffer.GlobalSettings, _vim.VimData, _vim.VimHost)
                    taggerSource :> IAsyncTaggerSource<HighlightSearchData , TextMarkerTag>)
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
        [<EditorUtilsImport>] _taggerFactory : ITaggerFactory
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
                let tagger = _taggerFactory.CreateBasicTaggerRaw(taggerSource)
                tagger :> obj :?> ITagger<'T>

/// Fold tagger for the IOutliningRegion tags created by folds.  Note that folds work
/// on an ITextBuffer level and not an ITextView level.  Hence this works directly with
/// IFoldManagerData instead of IFoldManager
type internal FoldTaggerSource(_foldData : IFoldData) as this =

    let _textBuffer = _foldData.TextBuffer
    let _changed = StandardEvent()

    do 
        _foldData.FoldsUpdated 
        |> Event.add (fun _ -> _changed.Trigger this)

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
        [<EditorUtilsImport>] _taggerFactory : ITaggerFactory
    ) =

    let _key = obj()

    interface ITaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> textBuffer =
            let tagger = _taggerFactory.CreateBasicTagger(textBuffer.Properties, _key, fun() ->
                let foldData = _factory.GetFoldData textBuffer
                let taggerSource = new FoldTaggerSource(foldData)
                taggerSource :> IBasicTaggerSource<OutliningRegionTag>)
            tagger :> obj :?> ITagger<'T>


