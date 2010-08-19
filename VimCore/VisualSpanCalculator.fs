#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal VisualSpanCalculator() =

    member x.CalculateForChar point oldSpan = 
        let oldLineCount = SnapshotSpanUtil.GetLineCount oldSpan
        let snapshot = SnapshotPointUtil.GetSnapshot point
        let pointLine = SnapshotPointUtil.GetContainingLine point
        let diff = oldLineCount - 1
        let span = SnapshotSpanUtil.ExtendDownIncludingLineBreak pointLine.ExtentIncludingLineBreak diff
        let endPoint = 
            let endLineEnd = span |> SnapshotSpanUtil.GetEndLine |> SnapshotLineUtil.GetEnd
            let count = 
                let oldEndLine = oldSpan |> SnapshotSpanUtil.GetEndLine
                oldSpan.End.Position - oldEndLine.Start.Position
            match SnapshotPointUtil.TryAdd span.End count with
            | None -> endLineEnd
            | Some(point) -> if point.Position > endLineEnd.Position then endLineEnd else point
        SnapshotSpan(span.Start, endPoint)

    member x.CalculateForLine point oldSpan = 
        let oldLineCount = SnapshotSpanUtil.GetLineCount oldSpan
        let snapshot = SnapshotPointUtil.GetSnapshot point
        let pointLine = SnapshotPointUtil.GetContainingLine point
        let diff = oldLineCount - 1
        SnapshotSpanUtil.ExtendDownIncludingLineBreak pointLine.ExtentIncludingLineBreak diff

    member x.CalculateForPoint point oldVisualSpan = 
        match oldVisualSpan with
        | VisualSpan.Single(kind,oldSpan) -> 
            let span = 
                match kind with
                | VisualKind.Block -> failwith "Need to implement"
                | VisualKind.Character -> x.CalculateForChar point oldSpan
                | VisualKind.Line -> x.CalculateForLine point oldSpan
            VisualSpan.Single(kind,span)
        | VisualSpan.Multiple(_,_) -> failwith "Need to implement"

    member x.CalculateForTextView textView oldVisualSpan = 
        let point = TextViewUtil.GetCaretPoint textView
        x.CalculateForPoint point oldVisualSpan

    interface IVisualSpanCalculator with
        member x.CalculateForPoint point visualSpan = x.CalculateForPoint point visualSpan 
        member x.CalculateForTextView textView visualSpan = x.CalculateForTextView textView visualSpan
