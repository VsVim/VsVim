namespace Vim

open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Tagging
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition

/// Tagger for incremental searches
type IncrementalSearchTagger
    ( 
        _textBuffer : ITextBuffer,
        _search : IIncrementalSearch ) as this= 

    let _tagsChanged = new Event<System.EventHandler<SnapshotSpanEventArgs>, SnapshotSpanEventArgs>()
    let mutable _searchSpan : ITrackingSpan option = None
    do 
        let raiseAllChanged () = 
            // Don't bother calculating the range of changed spans.  Simply raise the event for the entire 
            // buffer.  The editor will only call back for visible spans
            let snapshot = _textBuffer.CurrentSnapshot
            let allSpan = SnapshotSpan(snapshot, 0, snapshot.Length)
            _tagsChanged.Trigger (this,SnapshotSpanEventArgs(allSpan))

        let handleChange (_,result) = 

            // Make sure to reset _searchSpan before raising the event.  The editor can and will call back
            // into us synchronously and access a stale value if we don't
            match result with
            | SearchFound(span) -> 
                _searchSpan <- Some(span.Snapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeExclusive))
            | SearchNotFound ->
                _searchSpan <- None
            raiseAllChanged()               

        let clearTags _ =
            _searchSpan <- None
            raiseAllChanged()
            

        _search.CurrentSearchUpdated
        |> Event.add handleChange
        _search.CurrentSearchCompleted |> Event.add clearTags
        _search.CurrentSearchCancelled |> Event.add clearTags

    member private x.GetTags (col:NormalizedSnapshotSpanCollection) =
        let inner snapshot = 
            let span = 
                match _searchSpan with 
                | None -> None
                | (Some(trackingSpan)) -> TrackingSpanUtil.GetSpan snapshot trackingSpan
            match span with
            | None -> Seq.empty
            | Some(span) ->
                let tag = TextMarkerTag(Constants.IncrementalSearchTagName)
                let tagSpan = TagSpan(span, tag) :> ITagSpan<TextMarkerTag>
                Seq.singleton tagSpan

        if col.Count = 0 then Seq.empty
        else inner (col.Item(0)).Snapshot

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
                let normal = buffer.NormalMode
                let search = buffer.IncrementalSearch
                let tagger = IncrementalSearchTagger(textBuffer,search)
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
                    | None -> None
                    | Some(foundSpan) -> 
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
