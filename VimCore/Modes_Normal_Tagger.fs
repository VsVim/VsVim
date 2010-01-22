#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Tagging
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
open System.Windows.Media

/// Tagger for incremental searches
type internal Tagger( _search : IIncrementalSearch ) as this= 
    let _tagsChanged = new Event<System.EventHandler<SnapshotSpanEventArgs>, SnapshotSpanEventArgs>()
    let mutable _searchSpan : SnapshotSpan option = None 
    do 
        let handleChange opt = 
            let raise opt = if Option.isSome opt then _tagsChanged.Trigger (this,SnapshotSpanEventArgs(Option.get opt))
            let prev = _searchSpan

            // Make sure to reset _searchSpan before raising the event.  The editor can and will call back
            // into us synchronously and access a stale value if we don't
            _searchSpan <- opt
            raise prev
            raise opt
        _search.CurrentSearchSpanChanged 
        |> Event.add handleChange

    member private x.GetTags col =
        match _searchSpan with
        | None -> Seq.empty
        | Some(span) ->
            let tag = TextMarkerTag("blue")
            let tagSpan = TagSpan(span, tag) :> ITagSpan<TextMarkerTag>
            Seq.singleton tagSpan

    interface ITagger<TextMarkerTag> with
        member x.GetTags col = x.GetTags col
        [<CLIEvent>]
        member x.TagsChanged = _tagsChanged.Publish


