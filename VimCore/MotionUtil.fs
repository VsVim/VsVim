#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

type internal MotionUtil 
    ( 
        _textView : ITextView,
        _settings : IVimGlobalSettings) = 

    /// Caret point in the view
    member x.StartPoint = TextViewUtil.GetCaretPoint _textView

    member private x.SpanAndForwardFromLines (line1:ITextSnapshotLine) (line2:ITextSnapshotLine) = 
        if line1.LineNumber <= line2.LineNumber then SnapshotSpan(line1.Start, line2.End),true
        else SnapshotSpan(line2.Start, line1.End),false

    /// Apply the startofline option to the given MotionData
    member private x.ApplyStartOfLineOption (motionData:MotionData) =
        if not _settings.StartOfLine then motionData 
        else
            let endLine = 
                if motionData.IsForward then SnapshotSpanUtil.GetEndLine motionData.Span
                else SnapshotSpanUtil.GetStartLine motionData.Span
            let point = TssUtil.FindFirstNonWhitespaceCharacter endLine
            let _,column = SnapshotPointUtil.GetLineColumn point
            { motionData with Column=Some column }

    member private x.ForwardCharMotionCore c count func = 
        let start = x.StartPoint
        match func start c count with
        | None -> None 
        | Some(point:SnapshotPoint) -> 
            let span = SnapshotSpan(start, point.Add(1))
            {Span=span; IsForward=true; OperationKind=OperationKind.CharacterWise; MotionKind=MotionKind.Inclusive; Column=None} |> Some

    member private x.BackwardCharMotionCore c count func = 
        let start = x.StartPoint
        match func start c count with
        | None -> None
        | Some(point:SnapshotPoint) ->
            let span = SnapshotSpan(point, start)
            {Span=span; IsForward=false; OperationKind=OperationKind.CharacterWise; MotionKind=MotionKind.Exclusive; Column=None} |> Some

    member private x.LineToLineFirstNonWhitespaceMotion (originLine:ITextSnapshotLine) (endLine:ITextSnapshotLine) =
        let _,column = TssUtil.FindFirstNonWhitespaceCharacter endLine |> SnapshotPointUtil.GetLineColumn
        let span,isForward = 
            if originLine.LineNumber < endLine.LineNumber then
                let span = SnapshotSpan(originLine.Start, endLine.End)
                (span, true)
            elif originLine.LineNumber > endLine.LineNumber then
                let span = SnapshotSpan(endLine.Start, originLine.End)
                (span, false)
            else 
                let span = SnapshotSpan(endLine.Start, endLine.End)
                (span, true)
        {Span=span; IsForward=isForward; OperationKind=OperationKind.LineWise; MotionKind=MotionKind.Inclusive; Column= Some column }

    interface IMotionUtil with
        member x.TextView = _textView
        member x.ForwardChar c count = x.ForwardCharMotionCore c count TssUtil.FindNextOccurranceOfCharOnLine
        member x.ForwardTillChar c count = x.ForwardCharMotionCore c count TssUtil.FindTillNextOccurranceOfCharOnLine
        member x.BackwardChar c count = x.BackwardCharMotionCore c count TssUtil.FindPreviousOccurranceOfCharOnLine 
        member x.BackwardTillChar c count = x.BackwardCharMotionCore c count TssUtil.FindTillPreviousOccurranceOfCharOnLine
        member x.WordForward kind count = 
            let start = x.StartPoint
            let endPoint = TssUtil.FindNextWordStart start count kind  
            let span = SnapshotSpan(start,endPoint)
            {Span=span; IsForward=true; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        member x.WordBackward kind count =
            let start = x.StartPoint
            let startPoint = TssUtil.FindPreviousWordStart start count kind
            let span = SnapshotSpan(startPoint,start)
            {Span=span; IsForward=false; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        member x.AllWord kind count = 
            let start = x.StartPoint
            let start = match TssUtil.FindCurrentFullWordSpan start kind with 
                            | Some (s) -> s.Start
                            | None -> start
            let endPoint = TssUtil.FindNextWordStart start count kind  
            let span = SnapshotSpan(start,endPoint)
            {Span=span; IsForward=true; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        member x.EndOfWord kind count = 
            let start = x.StartPoint
            let snapshot = SnapshotPointUtil.GetSnapshot start
            let snapshotEnd = SnapshotUtil.GetEndPoint snapshot

            // Find the next point from the end of the current word
            let findNext point = 
                let next = 
                    SnapshotSpan(point, snapshotEnd)
                    |> SnapshotSpanUtil.GetPoints
                    |> Seq.tryFind (fun p -> TextUtil.IsWordChar (p.GetChar()) kind)
                match next with 
                | None -> None
                | Some(point) -> 
                    match TssUtil.FindCurrentFullWordSpan point kind with
                    | None -> None
                    | Some(span) -> SnapshotSpanUtil.GetLastIncludedPoint span 

            // Find the point to start the actual search from
            let firstPoint = 
                if start = snapshotEnd then start
                else start.Add(1)

            let rec inner point count = 
                if count = 0 || point = snapshotEnd then Some point 
                else 
                    match findNext (point.Add(1)) with
                    | None -> None
                    | Some(point) -> inner point (count - 1)

            let endPoint = inner firstPoint count
            match endPoint with 
            | None -> None
            | Some(endPoint) ->
                // Make it inclusive
                let endPoint = if endPoint = snapshotEnd then endPoint else endPoint.Add(1)
                let span = SnapshotSpan(start,endPoint)
                {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.CharacterWise; Column=None} |> Some
        member x.EndOfLine count = 
            let start = x.StartPoint
            let span = SnapshotPointUtil.GetLineRangeSpan start count
            {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        member x.FirstNonWhitespaceOnLine () =
            let start = x.StartPoint
            let line = start.GetContainingLine()
            let found = SnapshotLineUtil.GetPoints line
                            |> Seq.filter (fun x -> x.Position < start.Position)
                            |> Seq.tryFind (fun x-> not (CharUtil.IsWhiteSpace (x.GetChar())))
            let span = match found with 
                        | Some p -> new SnapshotSpan(p, start)
                        | None -> new SnapshotSpan(start,0)
            {Span=span; IsForward=false; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None} 
        member x.BeginingOfLine () =
            let start = x.StartPoint
            let line = SnapshotPointUtil.GetContainingLine start
            let span = SnapshotSpan(line.Start, start)
            {Span=span; IsForward=false; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        member x.LineDownToFirstNonWhitespace count =
            let start = x.StartPoint
            let line = SnapshotPointUtil.GetContainingLine start
            let number = line.LineNumber + count
            let endLine = SnapshotUtil.GetLineOrLast line.Snapshot number
            let point = TssUtil.FindFirstNonWhitespaceCharacter endLine
            let span = SnapshotSpan(start, point.Add(1)) // Add 1 since it's inclusive
            {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise; Column=None}
        member x.LineUpToFirstNonWhitespace count =
            let point = x.StartPoint
            let endLine = SnapshotPointUtil.GetContainingLine point
            let startLine = SnapshotUtil.GetLineOrFirst endLine.Snapshot (endLine.LineNumber - count)
            let span = SnapshotSpan(startLine.Start, endLine.End)
            let column = TssUtil.FindFirstNonWhitespaceCharacter startLine
            {Span=span; IsForward=false; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise; Column= Some column.Position} 
        member x.CharLeft count = 
            let start = x.StartPoint
            let prev = SnapshotPointUtil.GetPreviousPointOnLine start count 
            if prev = start then None
            else {Span=SnapshotSpan(prev,start); IsForward=false; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None} |> Some
        member x.CharRight count =
            let start = x.StartPoint
            let next = SnapshotPointUtil.GetNextPointOnLine start count 
            if next = start then None
            else {Span=SnapshotSpan(start,next); IsForward=true; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None } |> Some
        member x.LineUp count =     
            let point = x.StartPoint
            let endLine = SnapshotPointUtil.GetContainingLine point
            let startLineNumber = max 0 (endLine.LineNumber - count)
            let startLine = SnapshotUtil.GetLine endLine.Snapshot startLineNumber
            let span = SnapshotSpan(startLine.Start, endLine.End)
            {Span=span; IsForward=false; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise; Column=None } 
        member x.LineDown count = 
            let point = x.StartPoint
            let startLine = SnapshotPointUtil.GetContainingLine point
            let endLineNumber = startLine.LineNumber + count
            let endLine = SnapshotUtil.GetLineOrLast startLine.Snapshot endLineNumber
            let span = SnapshotSpan(startLine.Start, endLine.End)            
            {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise; Column=None } 
        member x.LineOrFirstToFirstNonWhitespace numberOpt = 
            let point = x.StartPoint
            let originLine = SnapshotPointUtil.GetContainingLine point
            let tss= originLine.Snapshot
            let endLine = 
                match numberOpt with
                | Some(number) ->  SnapshotUtil.GetLineOrFirst tss (TssUtil.VimLineToTssLine number)
                | None -> SnapshotUtil.GetFirstLine tss 
            x.LineToLineFirstNonWhitespaceMotion originLine endLine
        member x.LineOrLastToFirstNonWhitespace numberOpt = 
            let point = x.StartPoint
            let originLine = SnapshotPointUtil.GetContainingLine point
            let tss= originLine.Snapshot
            let endLine = 
                match numberOpt with
                | Some(number) ->  SnapshotUtil.GetLineOrLast tss (TssUtil.VimLineToTssLine number)
                | None -> SnapshotUtil.GetLastLine tss 
            x.LineToLineFirstNonWhitespaceMotion originLine endLine
        member x.LastNonWhitespaceOnLine count = 
            let start = x.StartPoint
            let startLine = SnapshotPointUtil.GetContainingLine start
            let snapshot = startLine.Snapshot
            let number = startLine.LineNumber + (count-1)
            let endLine = SnapshotUtil.GetLineOrLast snapshot number
            let endPoint = TssUtil.FindLastNonWhitespaceCharacter endLine
            let endPoint = if SnapshotUtil.GetEndPoint snapshot = endPoint then endPoint else endPoint.Add(1)
            let span = SnapshotSpan(start,endPoint)
            {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        member x.LineFromTopOfVisibleWindow countOpt = 
            let caretPoint,caretLine = TextViewUtil.GetCaretPointAndLine _textView
            let lines = TextViewUtil.GetVisibleSnapshotLines _textView |> List.ofSeq
            let span = 
                if lines.Length = 0 then caretLine.Extent
                else  
                    let count = (CommandUtil.CountOrDefault countOpt) 
                    let count = min count lines.Length
                    let startLine = lines.Head
                    SnapshotPointUtil.GetLineRangeSpan startLine.Start count
            let isForward = caretPoint.Position <= span.End.Position
            {Span=span; IsForward=isForward; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise; Column=None}
            |> x.ApplyStartOfLineOption
        member x.LineFromBottomOfVisibleWindow countOpt =
            let caretPoint,caretLine = TextViewUtil.GetCaretPointAndLine _textView
            let lines = TextViewUtil.GetVisibleSnapshotLines _textView |> List.ofSeq
            let span,isForward = 
                if lines.Length = 0 then caretLine.Extent,true
                else
                    let endLine = 
                        match countOpt with 
                        | None -> List.nth lines (lines.Length-1)
                        | Some(count) ->    
                            let count = lines.Length - count
                            List.nth lines count
                    x.SpanAndForwardFromLines caretLine endLine
            {Span=span; IsForward=isForward; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise; Column=None}
            |> x.ApplyStartOfLineOption
        member x.LineInMiddleOfVisibleWindow () =
            let caretLine = TextViewUtil.GetCaretLine _textView
            let lines = TextViewUtil.GetVisibleSnapshotLines _textView |> List.ofSeq
            let middleLine =
                if lines.Length = 0 then caretLine
                else 
                    let index = lines.Length / 2
                    List.nth lines index
            let span,isForward = x.SpanAndForwardFromLines caretLine middleLine
            {Span=span; IsForward=isForward; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise; Column=None}
            |> x.ApplyStartOfLineOption
