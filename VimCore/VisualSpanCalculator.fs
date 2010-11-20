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

    /// Calculate the new span collection for the previous old span collection which
    /// was in block mode.  All block spans start on the same column and have the same
    /// length (as permitted by the length of the line).  Use the maximum length of the
    /// old span collection as the desired length of the new span
    member x.CalculateForBlock point (oldSpanCol:NormalizedSnapshotSpanCollection) =
        let desiredLength = 
            oldSpanCol
            |> Seq.map SnapshotSpanUtil.GetLength
            |> Seq.max
        let snapshot = SnapshotPointUtil.GetSnapshot point
        let line,column = SnapshotPointUtil.GetLineColumn point
        let col = 
            SnapshotUtil.GetLines snapshot line SearchKind.Forward
            |> Seq.truncate (oldSpanCol.Count)
            |> Seq.map (fun snapshotLine ->
                let empty = snapshotLine.End |> SnapshotSpanUtil.CreateEmpty 
                match SnapshotPointUtil.TryAdd snapshotLine.Start column with
                | None -> empty
                | Some(startPoint) -> 
                    let endPoint = 
                        if startPoint.Position + desiredLength >= snapshotLine.EndIncludingLineBreak.Position then snapshotLine.End
                        else startPoint.Add(desiredLength)
                    SnapshotSpan(startPoint, endPoint) )
        let col = new NormalizedSnapshotSpanCollection(col)
        VisualSpan.Multiple (VisualKind.Block,col)

    member x.CalculateForPoint point oldVisualSpan = 
        match oldVisualSpan with
        | VisualSpan.Single(kind,oldSpan) -> 
            match kind with
            | VisualKind.Block -> 
                let col = new NormalizedSnapshotSpanCollection(oldSpan)
                x.CalculateForBlock point col
            | VisualKind.Character -> 
                let span = x.CalculateForChar point oldSpan 
                VisualSpan.Single (kind, span)
            | VisualKind.Line -> 
                let span = x.CalculateForLine point oldSpan
                VisualSpan.Single (kind, span)
        | VisualSpan.Multiple(_,oldSpanCol) -> x.CalculateForBlock point oldSpanCol

    member x.CalculateForTextView textView oldVisualSpan = 
        let point = TextViewUtil.GetCaretPoint textView
        x.CalculateForPoint point oldVisualSpan

    interface IVisualSpanCalculator with
        member x.CalculateForPoint point visualSpan = x.CalculateForPoint point visualSpan 
        member x.CalculateForTextView textView visualSpan = x.CalculateForTextView textView visualSpan
