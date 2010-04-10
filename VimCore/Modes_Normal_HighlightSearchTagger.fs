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
        _wordNav : ITextStructureNavigator,
        _search : ISearchService ) as this = 

    let _tagsChanged = new Event<System.EventHandler<SnapshotSpanEventArgs>, SnapshotSpanEventArgs>()
    let mutable _lastSearchData : SearchData option = None

    do 
        let raiseAllChanged () = 
            // Don't bother calculating the range of changed spans.  Simply raise the event for the entire 
            // buffer.  The editor will only call back for visible spans
            let snapshot = _textBuffer.CurrentSnapshot
            let allSpan = SnapshotSpan(snapshot, 0, snapshot.Length)
            _tagsChanged.Trigger (this,SnapshotSpanEventArgs(allSpan))

        _search.LastSearchChanged 
        |> Event.add (fun data -> 
            
            _lastSearchData <- Some data
            raiseAllChanged() )

        // Up cast here to work around the F# bug which prevents accessing a CLIEvent from
        // a derived type
        (_settings :> IVimSettings).SettingChanged 
        |> Event.filter (fun args -> StringUtil.isEqual args.Name GlobalSettingNames.HighlightSearchName)
        |> Event.add (fun _ -> raiseAllChanged())
        
    member private x.GetTagsCore (col:NormalizedSnapshotSpanCollection) = 
        // Build up the search information
        let searchData = { _search.LastSearch with Kind = SearchKind.Forward }
            
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
            let tag = TextMarkerTag("vsvim_highlightsearch")
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

[<Export(typeof<ITaggerProvider>)>]
[<ContentType("text")>]
[<TextViewRole(PredefinedTextViewRoles.Editable)>]
[<TagType(typeof<TextMarkerTag>)>]
type internal HighlightIncrementalSearchTaggerProvider
    [<ImportingConstructor>]
    ( _vim : IVim ) = 

    interface ITaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> (textBuffer) = 
            match _vim.GetBufferForBuffer textBuffer with
            | None -> null
            | Some(buffer) ->
                let nav = buffer.NormalMode.IncrementalSearch.WordNavigator
                let tagger = HighlightIncrementalSearchTagger(textBuffer, buffer.Settings.GlobalSettings, nav, _vim.SearchService)
                tagger :> obj :?> ITagger<'T>
