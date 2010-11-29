
#light

namespace Vim.Modes.SubstituteConfirm
open Vim
open Vim.NullableUtil
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Tagging
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition

/// Tagger for matches as they appear during a confirm substitute
type internal SubstituteConfirmTagger
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

    member private x.GetTags (col:NormalizedSnapshotSpanCollection) = 
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
type internal SubstituteConfirmTaggerProvider
    [<ImportingConstructor>]
    ( _vim : IVim ) = 

    interface ITaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> (textBuffer) = 
            match _vim.GetBufferForBuffer textBuffer with
            | None -> null
            | Some(buffer) ->
                let tagger = new SubstituteConfirmTagger(textBuffer, buffer.SubstituteConfirmMode)
                tagger :> obj :?> ITagger<'T>
