namespace Vim

open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Tagging
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition

/// Tagger for incremental searches
type IncrementalSearchTagger (_buffer : IVimBuffer) as this =

    let _search = _buffer.IncrementalSearch
    let _textBuffer = _buffer.TextBuffer
    let _globalSettings = _buffer.LocalSettings.GlobalSettings
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
        _buffer.SwitchedMode
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

        if col.Count = 0 || VisualKind.IsAnyVisual _buffer.ModeKind || not _globalSettings.IncrementalSearch then 
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

[<Export(typeof<ITaggerProvider>)>]
[<ContentType(Constants.ContentType)>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
[<TagType(typeof<TextMarkerTag>)>]
type internal IncrementalSearchTaggerProvider
    [<ImportingConstructor>]
    ( _vim : IVim ) = 

    interface ITaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> (textBuffer) = 
            match _vim.GetBufferForBuffer textBuffer with
            | None -> null
            | Some(buffer) ->
                let tagger = new IncrementalSearchTagger(buffer)
                tagger :> obj :?> ITagger<'T>

/// Tagger for completed incremental searches
type HighlightIncrementalSearchTagger
    ( 
        _textBuffer : ITextBuffer,
        _settings : IVimGlobalSettings,
        _wordNav : ITextStructureNavigator,
        _search : ISearchService,
        _vimData : IVimData
    ) as this =

    let _tagsChanged = new Event<System.EventHandler<SnapshotSpanEventArgs>, SnapshotSpanEventArgs>()
    let _eventHandlers = DisposableBag()

    /// Users can temporarily disable highlight search with the ':noh' command.  This is true while 
    /// we are in that mode
    let mutable _oneTimeDisabled = false

    do 
        let raiseAllChanged () = 
            // Don't bother calculating the range of changed spans.  Simply raise the event for the entire 
            // buffer.  The editor will only call back for visible spans
            let snapshot = _textBuffer.CurrentSnapshot
            let allSpan = SnapshotSpan(snapshot, 0, snapshot.Length)
            _tagsChanged.Trigger (this, SnapshotSpanEventArgs(allSpan))

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
        
    member private x.GetTagsCore (col : NormalizedSnapshotSpanCollection) = 
        // Build up the search information.  Don't need to wrap here as we just want
        // to consider the SnapshotSpan going forward
        let searchData = SearchData.OfPatternData _vimData.LastPatternData false
        let searchData = { searchData with Kind = SearchKind.Forward }

        let withSpan (span : SnapshotSpan) =  
            span.Start
            |> Seq.unfold (fun point -> 
                if SnapshotPointUtil.IsEndPoint point then
                    // If this is the end point of the ITextBuffer then we are done
                    None
                else
                    match _search.FindNext searchData point _wordNav with
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

        if StringUtil.isNullOrEmpty searchData.Pattern then
            Seq.empty
        else 
            let tag = TextMarkerTag(Constants.HighlightIncrementalSearchTagName)
            col 
            |> Seq.map (fun span -> withSpan span) 
            |> Seq.concat
            |> Seq.map (fun span -> TagSpan(span,tag) :> ITagSpan<TextMarkerTag> )

    member private x.GetTags (col:NormalizedSnapshotSpanCollection) = 
        if _settings.HighlightSearch && not _oneTimeDisabled then 
            x.GetTagsCore col
        else 
            Seq.empty

    interface ITagger<TextMarkerTag> with
        member x.GetTags col = x.GetTags col
        [<CLIEvent>]
        member x.TagsChanged = _tagsChanged.Publish

    interface System.IDisposable with
        member x.Dispose() = _eventHandlers.DisposeAll()

[<Export(typeof<ITaggerProvider>)>]
[<ContentType(Constants.ContentType)>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
[<TagType(typeof<TextMarkerTag>)>]
type HighlightIncrementalSearchTaggerProvider
    [<ImportingConstructor>]
    ( _vim : IVim ) = 

    interface ITaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> (textBuffer) = 
            match _vim.GetBufferForBuffer textBuffer with
            | None -> null
            | Some buffer ->
                let nav = buffer.IncrementalSearch.WordNavigator
                let tagger = new HighlightIncrementalSearchTagger(textBuffer, buffer.LocalSettings.GlobalSettings, nav, _vim.SearchService, _vim.VimData)
                tagger :> obj :?> ITagger<'T>

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

[<Export(typeof<ITaggerProvider>)>]
[<ContentType(Constants.ContentType)>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
[<TagType(typeof<TextMarkerTag>)>]
type SubstituteConfirmTaggerProvider
    [<ImportingConstructor>]
    ( _vim : IVim ) = 

    interface ITaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> (textBuffer) = 
            match _vim.GetBufferForBuffer textBuffer with
            | None -> null
            | Some(buffer) ->
                let tagger = new SubstituteConfirmTagger(textBuffer, buffer.SubstituteConfirmMode)
                tagger :> obj :?> ITagger<'T>
