namespace Vim

open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Tagging
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition

/// Tagger for incremental searches
type IncrementalSearchTagger(_buffer : IVimBuffer) as this =

    let _search = _buffer.IncrementalSearch
    let _textBuffer = _buffer.TextBuffer
    let _tagsChanged = new Event<System.EventHandler<SnapshotSpanEventArgs>, SnapshotSpanEventArgs>()
    let mutable _previousSearchSpan : ITrackingSpan option = None
    let mutable _currentSearchSpan : ITrackingSpan option = None

    do 
        let raiseAllChanged () = 
            // Don't bother calculating the range of changed spans.  Simply raise the event for the entire 
            // buffer.  The editor will only call back for visible spans
            let snapshot = _textBuffer.CurrentSnapshot
            let allSpan = SnapshotSpan(snapshot, 0, snapshot.Length)
            _tagsChanged.Trigger (this,SnapshotSpanEventArgs(allSpan))

        let updateCurrentWithResult result = 
            _currentSearchSpan <- 
                match result with
                | SearchResult.Found (_, span, _) -> span.Snapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeExclusive) |> Some
                | SearchResult.NotFound _ -> None

        let handleCurrentSearchUpdated result = 

            // Make sure to reset the stored spans before raising the event.  The editor can and will call back
            // into us synchronously and access a stale value if we don't
            updateCurrentWithResult result
            raiseAllChanged()

        let handleCurrentSearchCompleted result = 
            updateCurrentWithResult result
            _previousSearchSpan <- _currentSearchSpan
            _currentSearchSpan <- None
            raiseAllChanged()

        let handleCurrentSearchCancelled () = 
            _currentSearchSpan <- None
            raiseAllChanged()

        _search.CurrentSearchUpdated |> Event.add handleCurrentSearchUpdated 
        _search.CurrentSearchCompleted |> Event.add handleCurrentSearchCompleted
        _search.CurrentSearchCancelled |> Event.add (fun _ -> handleCurrentSearchCancelled())
        _buffer.SwitchedMode |> Event.add (fun _ -> raiseAllChanged())

    member x.GetTags (col:NormalizedSnapshotSpanCollection) =

        let inner snapshot searchSpan = 
            let span = 
                match searchSpan with 
                | None -> None
                | (Some(trackingSpan)) -> TrackingSpanUtil.GetSpan snapshot trackingSpan
            match span with
            | None -> None
            | Some(span) ->
                let tag = TextMarkerTag(Constants.IncrementalSearchTagName)
                let tagSpan = TagSpan(span, tag) :> ITagSpan<TextMarkerTag>
                Some tagSpan

        if col.Count = 0 || VisualKind.IsAnyVisual _buffer.ModeKind then 
            Seq.empty
        else 
            let snapshot = col.Item(0).Snapshot
            [
                inner snapshot _currentSearchSpan
                inner snapshot _previousSearchSpan
            ]
            |> SeqUtil.filterToSome

    interface ITagger<TextMarkerTag> with
        member x.GetTags col = x.GetTags col
        [<CLIEvent>]
        member x.TagsChanged = _tagsChanged.Publish

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
                let tagger = IncrementalSearchTagger(buffer)
                tagger :> obj :?> ITagger<'T>

/// Tagger for completed incremental searches
type HighlightIncrementalSearchTagger
    ( 
        _textBuffer : ITextBuffer,
        _settings : IVimGlobalSettings,
        _wordNav : ITextStructureNavigator,
        _search : ISearchService,
        _vimData : IVimData) as this =

    let _tagsChanged = new Event<System.EventHandler<SnapshotSpanEventArgs>, SnapshotSpanEventArgs>()
    let mutable _lastSearchData : SearchData option = None
    let _eventHandlers = DisposableBag()

    do 
        let raiseAllChanged () = 
            // Don't bother calculating the range of changed spans.  Simply raise the event for the entire 
            // buffer.  The editor will only call back for visible spans
            let snapshot = _textBuffer.CurrentSnapshot
            let allSpan = SnapshotSpan(snapshot, 0, snapshot.Length)
            _tagsChanged.Trigger (this,SnapshotSpanEventArgs(allSpan))

        _vimData.LastSearchDataChanged 
        |> Observable.subscribe (fun data -> 
            _lastSearchData <- Some data
            raiseAllChanged() )
        |> _eventHandlers.Add

        // Up cast here to work around the F# bug which prevents accessing a CLIEvent from
        // a derived type
        (_settings :> IVimSettings).SettingChanged 
        |> Observable.filter (fun args -> StringUtil.isEqual args.Name GlobalSettingNames.HighlightSearchName)
        |> Observable.subscribe (fun _ -> raiseAllChanged())
        |> _eventHandlers.Add
        
    member private x.GetTagsCore (col:NormalizedSnapshotSpanCollection) = 
        // Build up the search information
        let searchData = { _vimData.LastSearchData with Kind = SearchKind.Forward }
            
        let withSpan (span:SnapshotSpan) =  
            span.Start
            |> Seq.unfold (fun point -> 
                if point.Position >= span.Length then None
                else
                    match _search.FindNext searchData point _wordNav with
                    | SearchResult.NotFound _ -> 
                        None
                    | SearchResult.Found (_, foundSpan, _) ->
                        if foundSpan.Start.Position <= span.End.Position then Some(foundSpan, foundSpan.End)
                        else None )
                    
        if StringUtil.isNullOrEmpty searchData.Text.RawText then Seq.empty
        else 
            let tag = TextMarkerTag(Constants.HighlightIncrementalSearchTagName)
            col 
            |> Seq.map (fun span -> withSpan span) 
            |> Seq.concat
            |> Seq.map (fun span -> TagSpan(span,tag) :> ITagSpan<TextMarkerTag> )


    member private x.GetTags (col:NormalizedSnapshotSpanCollection) = 
        if _settings.HighlightSearch then x.GetTagsCore col
        else Seq.empty

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
            | Some(buffer) ->
                let nav = buffer.IncrementalSearch.WordNavigator
                let tagger = new HighlightIncrementalSearchTagger(textBuffer, buffer.Settings.GlobalSettings, nav, _vim.SearchService, _vim.VimData)
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
