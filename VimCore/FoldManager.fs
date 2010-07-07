#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Text.Tagging
open Microsoft.VisualStudio.Text.Classification
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition
open System.Collections.Generic

type FoldManager(_textBuffer : ITextBuffer) = 
    
    let _updated = Event<System.EventArgs>()
    let mutable _folds : ITrackingSpan list = List.empty

    member x.Folds = 
        _folds 
        |> Seq.ofList 
        |> Seq.map (TrackingSpanUtil.GetSpan _textBuffer.CurrentSnapshot) 
        |> SeqUtil.filterToSome
        |> Seq.sortBy (fun span -> span.Start.Position)
    member x.CreateFold span = 
        let startLine,endLine = SnapshotSpanUtil.GetStartAndEndLine span
        let span = SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak)
        let span = _textBuffer.CurrentSnapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeInclusive)
        _folds <- span :: _folds
        _updated.Trigger System.EventArgs.Empty
    member x.DeleteFold (point:SnapshotPoint) = 
        let snapshot = _textBuffer.CurrentSnapshot
        let data = 
            _folds
            |> Seq.map (fun span -> TrackingSpanUtil.GetSpan snapshot span,span)
            |> Seq.map OptionUtil.combine2
            |> SeqUtil.filterToSome
            |> Seq.sortBy (fun (span,_) -> span.Start.Position)
        let ret = 
            match data |> Seq.tryFind (fun (span,_) -> span.Contains(point)) with
            | None -> false
            | Some(_,tracking) -> 
                _folds <- _folds |> Seq.ofList |> Seq.filter (fun t -> t <> tracking) |> List.ofSeq
                true
        _updated.Trigger System.EventArgs.Empty
        ret

    interface IFoldManager with
        member x.TextBuffer = _textBuffer
        member x.Folds = x.Folds
        member x.CreateFold span = x.CreateFold span
        member x.DeleteFold point = x.DeleteFold point
        [<CLIEvent>]
        member x.FoldsUpdated = _updated.Publish

type internal FoldTagger 
    (
        _textBuffer : ITextBuffer,
        _foldManager : IFoldManager ) as this =

    let _tagsChanged = new Event<System.EventHandler<SnapshotSpanEventArgs>, SnapshotSpanEventArgs>()

    do 
        let handle _ = _tagsChanged.Trigger(this, new SnapshotSpanEventArgs(SnapshotUtil.GetFullSpan _textBuffer.CurrentSnapshot))
        _foldManager.FoldsUpdated |> Event.add handle

    member x.GetTags (col:NormalizedSnapshotSpanCollection) =
        let getDescription span = 
            let startLine,endLine = SnapshotSpanUtil.GetStartAndEndLine span
            sprintf "%d lines ---" ((endLine.LineNumber - startLine.LineNumber) + 1)

        if col.Count = 0 then Seq.empty
        else 
            let snapshot = (col.Item(0)).Snapshot
            _foldManager.Folds
            |> Seq.filter ( fun span -> span.Snapshot = snapshot )
            |> Seq.map (fun span -> 
                let description = getDescription span
                let tag = OutliningRegionTag(true, true, description, "Fold Hint")
                TagSpan<OutliningRegionTag>(span, tag) :> ITagSpan<OutliningRegionTag> )

    interface ITagger<OutliningRegionTag > with
        member x.GetTags col = x.GetTags col
        [<CLIEvent>]
        member x.TagsChanged = _tagsChanged.Publish


[<Export(typeof<ITaggerProvider>)>]
[<ContentType("text")>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
[<TagType(typeof<OutliningRegionTag>)>]
type FoldTaggerProvider
    [<ImportingConstructor>]
    (_factory : IFoldManagerFactory) = 

    interface ITaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> (textBuffer) = 
            let foldManager = _factory.GetFoldManager textBuffer
            let tagger = FoldTagger(textBuffer,foldManager) 
            tagger :> obj :?> ITagger<'T>

[<Export(typeof<IFoldManagerFactory>)>]
type FoldManagerFactory() = 

    let _key = new System.Object()

    interface IFoldManagerFactory with
        member x.GetFoldManager (buffer : ITextBuffer) =
            buffer.Properties.GetOrCreateSingletonProperty(_key, fun () -> FoldManager(buffer)) :> IFoldManager

