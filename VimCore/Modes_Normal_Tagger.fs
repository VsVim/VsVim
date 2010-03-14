#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Tagging
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition

/// Tagger for incremental searches
type internal Tagger
    ( 
        _textBuffer : ITextBuffer,
        _search : IIncrementalSearch ) as this= 

    let _tagsChanged = new Event<System.EventHandler<SnapshotSpanEventArgs>, SnapshotSpanEventArgs>()
    let mutable _searchSpan : ITrackingSpan option = None
    do 
        let handleChange (_,result) = 

            // Make sure to reset _searchSpan before raising the event.  The editor can and will call back
            // into us synchronously and access a stale value if we don't
            match result with
            | SearchFound(span) -> 
                _searchSpan <- Some(span.Snapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeExclusive))
            | SearchNotFound ->
                _searchSpan <- None
               
            // Don't bother calculating the range of changed spans.  Simply raise the event for the entire 
            // buffer.  The editor will only call back for visible spans
            let snapshot = _textBuffer.CurrentSnapshot
            let allSpan = SnapshotSpan(snapshot, 0, snapshot.Length)
            _tagsChanged.Trigger (this,SnapshotSpanEventArgs(allSpan))

        _search.CurrentSearchUpdated
        |> Event.add handleChange

    member private x.GetTags (col:NormalizedSnapshotSpanCollection) =
        let inner snapshot = 
            let span = 
                match _searchSpan with 
                | None -> None
                | (Some(trackingSpan)) -> TssUtil.SafeGetTrackingSpan trackingSpan snapshot
            match span with
            | None -> Seq.empty
            | Some(span) ->
                let tag = TextMarkerTag("vsvim_incrementalsearch")
                let tagSpan = TagSpan(span, tag) :> ITagSpan<TextMarkerTag>
                Seq.singleton tagSpan

        if col.Count = 0 then Seq.empty
        else inner (col.Item(0)).Snapshot

    interface ITagger<TextMarkerTag> with
        member x.GetTags col = x.GetTags col
        [<CLIEvent>]
        member x.TagsChanged = _tagsChanged.Publish

[<Export(typeof<ITaggerProvider>)>]
[<ContentType("text")>]
[<TextViewRole(PredefinedTextViewRoles.Editable)>]
[<TagType(typeof<TextMarkerTag>)>]
type internal TaggerProvider
    [<ImportingConstructor>]
    ( _vim : IVim ) = 

    interface ITaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> (textBuffer) = 
            match _vim.GetBufferForBuffer textBuffer with
            | None -> null
            | Some(buffer) ->
                let normal = buffer.NormalMode
                let search = normal.IncrementalSearch
                let tagger = Tagger(textBuffer,search)
                tagger :> obj :?> ITagger<'T>

