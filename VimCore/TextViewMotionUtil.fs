#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations


type internal TextViewMotionUtil 
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

    member private x.GetParagraphs point kind count = 
        let span = 
            MotionUtil.GetParagraphs point kind 
            |> Seq.truncate count
            |> Seq.map (fun p -> p.Span)
            |> SnapshotSpanUtil.CreateCombined
        let span = 
            match span with 
            | Some(span) -> span
            | None -> 
                // Can have no paragraphs at either end
                if SearchKindUtil.IsBackward kind || point.Snapshot.Length = 0 then 
                    point.Snapshot 
                    |> SnapshotUtil.GetStartPoint 
                    |> SnapshotSpanUtil.CreateEmpty
                else 
                    point.Snapshot 
                    |> SnapshotUtil.GetEndPoint 
                    |> SnapshotPointUtil.SubtractOne 
                    |> SnapshotSpanUtil.CreateEmpty
        let isForward = SearchKindUtil.IsForward kind
        {Span=span; IsForward=isForward; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.LineWise; Column=None}

    member x.SectionBackwardOrOther count otherChar = 
        let startPoint = SnapshotUtil.GetStartPoint _textView.TextSnapshot
        let endPoint = SnapshotUtil.GetEndPoint _textView.TextSnapshot

        // Get the previous point is the chain given the "current" point.  Must move
        // backwards to avoid infinite looping on the control chars.  Has the pleasant
        // side effect of implementing the motion as exclusive
        let rec getPrev point =
            let rec inner point = 
                if point = startPoint then startPoint
                elif point = endPoint then inner (endPoint.Subtract(1))
                elif SnapshotPointUtil.IsStartOfLine point then 
                    let c = SnapshotPointUtil.GetChar point 
                    if c = '\f' then point
                    elif c = otherChar then point
                    else inner (point.Subtract(1))
                else inner (point.Subtract(1))
            if point = startPoint then point
            else point.Subtract(1) |> inner

        let caretPoint = TextViewUtil.GetCaretPoint _textView 
        let beginPoint = 
            let rec inner count point = 
                if count = 0 then point
                else inner (count-1) (getPrev point)
            inner count caretPoint
        let span = SnapshotSpan(beginPoint,caretPoint)
        {Span=span; IsForward=false; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=Some 0}

    interface ITextViewMotionUtil with
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

            // Does the given point refer to a valid 'word' character?
            let isPointWordChar point = 
                if SnapshotPointUtil.IsEndPoint point then false
                else TextUtil.IsWordChar (point.GetChar()) kind

            // Create the appropriate MotionData structure with the provided SnapshotSpan
            let withSpan span = {
                Span=span 
                IsForward=true 
                MotionKind=MotionKind.Inclusive 
                OperationKind=OperationKind.CharacterWise 
                Column=None} 

            // Need to adjust the count of words that we will skip.  If we are currently 
            // not on a word then we move forward to the first word and lose a count.  If
            // we at the end of a word then we simply don't count it 
            let point,count = 
                let point = x.StartPoint
                if isPointWordChar point then 
                    match TssUtil.FindCurrentWordSpan point kind with
                    | None -> point,count
                    | Some(span) -> 
                        match SnapshotSpanUtil.GetLastIncludedPoint span with
                        | None -> point,count
                        | Some(endPoint) ->
                            if endPoint = point then (point.Add(1)),count
                            else point,count
                else point,(count - 1)
            let count = max 0 (count-1)

            // Now actually search the list of Word spans
            let wordSpan = 
                TssUtil.GetWordSpans point kind SearchKind.Forward
                |> SeqUtil.skipMax count
                |> SeqUtil.tryHeadOnly
            match wordSpan with
            | Some(span) -> SnapshotSpanUtil.Create x.StartPoint span.End |> withSpan
            | None -> 
                let snapshot = SnapshotPointUtil.GetSnapshot point
                let endPoint = SnapshotUtil.GetEndPoint snapshot
                SnapshotSpanUtil.Create point endPoint |> withSpan
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
            let line = x.StartPoint |> SnapshotPointUtil.GetContainingLine
            let number = line.LineNumber + count
            let endLine = SnapshotUtil.GetLineOrLast line.Snapshot number
            let column = TssUtil.FindFirstNonWhitespaceCharacter endLine |> SnapshotPointUtil.GetColumn |> Some
            let span = SnapshotSpan(line.Start, endLine.End)
            {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise; Column=column}
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
            let span = SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak)
            {Span=span; IsForward=false; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.LineWise; Column=None } 
        member x.LineDown count = 
            let point = x.StartPoint
            let startLine = SnapshotPointUtil.GetContainingLine point
            let endLineNumber = startLine.LineNumber + count
            let endLine = SnapshotUtil.GetLineOrLast startLine.Snapshot endLineNumber
            let span = SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak)            
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
        member x.SentenceForward count = 
            let caretPoint = TextViewUtil.GetCaretPoint _textView
            let span = 
                MotionUtil.GetSentences caretPoint SearchKind.Forward
                |> Seq.truncate count
                |> SnapshotSpanUtil.CreateCombined 
                |> Option.get   // GetSentences must return at least one
            {Span=span; IsForward=true; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        member x.SentenceBackward count = 
            let caretPoint = TextViewUtil.GetCaretPoint _textView
            let span = 
                MotionUtil.GetSentences caretPoint SearchKind.Backward
                |> Seq.truncate count
                |> SnapshotSpanUtil.CreateCombined 
                |> Option.get   // GetSentences must return at least one
            {Span=span; IsForward=false; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        member x.SentenceFullForward count =
            let caretPoint,caretLine = TextViewUtil.GetCaretPointAndLine _textView
            let span = 
                if SnapshotLineUtil.GetLength caretLine = 0 then 
                    // This behavior is not specified in the standard but if the caret line has
                    // 0 length then only it is included in the span 
                    SnapshotLineUtil.GetExtentIncludingLineBreak caretLine
                else 
                    let startPoint = 
                        MotionUtil.GetSentenceFull caretPoint
                        |> SnapshotSpanUtil.GetStartPoint
                    MotionUtil.GetSentences startPoint SearchKind.Forward 
                    |> Seq.truncate count
                    |> SnapshotSpanUtil.CreateCombinedOrEmpty caretPoint.Snapshot
            {Span=span; IsForward=true; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        member x.ParagraphForward count =
            x.GetParagraphs (TextViewUtil.GetCaretPoint _textView) SearchKind.Forward count
        member x.ParagraphBackward count =
            x.GetParagraphs (TextViewUtil.GetCaretPoint _textView) SearchKind.Backward count
        member x.ParagraphFullForward count =
            let caretPoint = TextViewUtil.GetCaretPoint _textView
            let span = MotionUtil.GetFullParagraph caretPoint
            x.GetParagraphs span.Start SearchKind.Forward count 
        member x.SectionForward motionArg count = 
            let endPoint = SnapshotUtil.GetEndPoint _textView.TextSnapshot

            // Move a single count forward from the given point
            let rec getNext point = 
                if point = endPoint then point
                else
                    let nextPoint = point.Add(1)
                    if nextPoint = endPoint then endPoint
                    elif SnapshotPointUtil.IsStartOfLine nextPoint then
                        match nextPoint.GetChar() with
                        | '\f' -> nextPoint
                        | '{' -> nextPoint 
                        | '}' -> 
                            match motionArg with
                            | MotionArgument.ConsiderCloseBrace ->
                                let line = SnapshotPointUtil.GetContainingLine nextPoint
                                line.EndIncludingLineBreak
                            | MotionArgument.None -> getNext nextPoint
                        | _ -> getNext nextPoint
                    else getNext nextPoint

            let startPoint = TextViewUtil.GetCaretPoint _textView 
            let endPoint = 
                let rec inner count point = 
                    if count = 0 then point
                    else inner (count-1) (getNext point)
                inner count startPoint 

            let span = SnapshotSpan(startPoint,endPoint)
            {Span=span; IsForward=true; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=Some 0}

        member x.SectionBackwardOrOpenBrace count = x.SectionBackwardOrOther count '{'
        member x.SectionBackwardOrCloseBrace count = x.SectionBackwardOrOther count '}'

    