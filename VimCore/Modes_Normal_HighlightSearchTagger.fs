#light

namespace Vim.Modes.Normal
open Vim
open Vim.NullableUtil
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Tagging
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition

/// Tagger for completed incremental searches
type internal HighlightIncrementalSearchTagger
    ( 
        _textBuffer : ITextBuffer,
        _settings : IVimGlobalSettings,
        _search : IIncrementalSearch,
        _searchService : ITextSearchService ) as this =

    let _tagsChanged = new Event<System.EventHandler<SnapshotSpanEventArgs>, SnapshotSpanEventArgs>()
    let mutable _lastSearchData : SearchData option = None

    do 
        let raiseAllChanged () = 
            // Don't bother calculating the range of changed spans.  Simply raise the event for the entire 
            // buffer.  The editor will only call back for visible spans
            let snapshot = _textBuffer.CurrentSnapshot
            let allSpan = SnapshotSpan(snapshot, 0, snapshot.Length)
            _tagsChanged.Trigger (this,SnapshotSpanEventArgs(allSpan))

        _search.CurrentSearchCompleted
        |> Event.add (fun (data,result) -> 
            
            _lastSearchData <- 
                match result with
                | SearchFound(_) -> Some(data)
                | SearchNotFound -> None

            raiseAllChanged() )

        // _settings.SettingChanged 
        // |> Event.filter (fun args -> StringUtil.isEqual args.Name "hlsearch")
        // |> Event.add (fun _ -> raiseAllChanged())
        
    member private x.GetTagsCore (col:NormalizedSnapshotSpanCollection) = 
        let options = NormalModeUtil.CreateFindOptions SearchKind.Forward _settings
        let withSpan pattern (span:SnapshotSpan) =  
            span.Start.Position
            |> Seq.unfold (fun pos -> 
                let snapshot = _textBuffer.CurrentSnapshot
                if pos >= snapshot.Length then None
                else
                    let findData = FindData(pattern, _textBuffer.CurrentSnapshot, options, _search.WordNavigator)
                    match _searchService.FindNext(pos, false, findData) with
                    | Null -> None
                    | HasValue(span) -> Some(span,span.End.Position) )
                    
        let searchData = _search.LastSearch
        if StringUtil.isNullOrEmpty searchData.Pattern then Seq.empty
        else 
            let tag = TextMarkerTag("vsvim_highlightsearch")
            col 
            |> Seq.map (fun span -> withSpan searchData.Pattern span) 
            |> Seq.concat
            |> Seq.map (fun span -> TagSpan(span,tag) :> ITagSpan<TextMarkerTag> )


    member private x.GetTags (col:NormalizedSnapshotSpanCollection) = 
        if _settings.HighlightSearch then x.GetTagsCore col
        else Seq.empty

    interface ITagger<TextMarkerTag> with
        member x.GetTags col = x.GetTags col
        [<CLIEvent>]
        member x.TagsChanged = _tagsChanged.Publish

[<Export(typeof<ITaggerProvider>)>]
[<ContentType("text")>]
[<TextViewRole(PredefinedTextViewRoles.Editable)>]
[<TagType(typeof<TextMarkerTag>)>]
type internal HighlightIncrementalSearchTaggerProvider
    [<ImportingConstructor>]
    ( _vim : IVim, _searchService : ITextSearchService ) = 

    interface ITaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> (textBuffer) = 
            match _vim.GetBufferForBuffer textBuffer with
            | None -> null
            | Some(buffer) ->
                let normal = buffer.NormalMode
                let search = normal.IncrementalSearch
                let tagger = HighlightIncrementalSearchTagger(textBuffer, buffer.Settings.GlobalSettings, search, _searchService)
                tagger :> obj :?> ITagger<'T>
