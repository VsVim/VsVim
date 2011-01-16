#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

type QuotedStringData =  {
        LeadingWhiteSpace : SnapshotSpan
        LeadingQuote : SnapshotPoint
        Contents : SnapshotSpan
        TrailingQuote : SnapshotPoint
        TrailingWhiteSpace : SnapshotSpan 
} with
    
    member x.FullSpan = SnapshotSpanUtil.Create x.LeadingWhiteSpace.Start x.TrailingWhiteSpace.End

type internal TextViewMotionUtil 
    ( 
        _textView : ITextView,
        _markMap : IMarkMap,
        _localSettings : IVimLocalSettings ) = 

    let _textBuffer = _textView.TextBuffer
    let _settings = _localSettings.GlobalSettings

    /// Caret point in the view
    member x.StartPoint = TextViewUtil.GetCaretPoint _textView

    member x.SpanAndForwardFromLines (line1:ITextSnapshotLine) (line2:ITextSnapshotLine) = 
        if line1.LineNumber <= line2.LineNumber then SnapshotSpan(line1.Start, line2.End),true
        else SnapshotSpan(line2.Start, line1.End),false

    /// Apply the startofline option to the given MotionData
    member x.ApplyStartOfLineOption (motionData:MotionData) =
        if not _settings.StartOfLine then motionData 
        else
            let endLine = 
                if motionData.IsForward then SnapshotSpanUtil.GetEndLine motionData.Span
                else SnapshotSpanUtil.GetStartLine motionData.Span
            let point = TssUtil.FindFirstNonWhitespaceCharacter endLine
            let _,column = SnapshotPointUtil.GetLineColumn point
            { motionData with Column=Some column }

    member x.ForwardCharMotionCore c count func = 
        let start = x.StartPoint
        match func start c count with
        | None -> None 
        | Some(point:SnapshotPoint) -> 
            let span = SnapshotSpan(start, point.Add(1))
            {Span=span; IsForward=true; OperationKind=OperationKind.CharacterWise; MotionKind=MotionKind.Inclusive; Column=None} |> Some

    member x.BackwardCharMotionCore c count func = 
        let start = x.StartPoint
        match func start c count with
        | None -> None
        | Some(point:SnapshotPoint) ->
            let span = SnapshotSpan(point, start)
            {Span=span; IsForward=false; OperationKind=OperationKind.CharacterWise; MotionKind=MotionKind.Exclusive; Column=None} |> Some

    member x.LineToLineFirstNonWhitespaceMotion (originLine:ITextSnapshotLine) (endLine:ITextSnapshotLine) =
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

    member x.GetParagraphs point direction count = 
        let span = 
            MotionUtil.GetParagraphs point direction
            |> Seq.truncate count
            |> Seq.map (fun p -> p.Span)
            |> SnapshotSpanUtil.CreateCombined
        let span = 
            match span with 
            | Some(span) -> span
            | None -> 
                // Can have no paragraphs at either end
                if Direction.Backward = direction || point.Snapshot.Length = 0 then 
                    point.Snapshot 
                    |> SnapshotUtil.GetStartPoint 
                    |> SnapshotSpanUtil.CreateEmpty
                else 
                    point.Snapshot 
                    |> SnapshotUtil.GetEndPoint 
                    |> SnapshotPointUtil.SubtractOne 
                    |> SnapshotSpanUtil.CreateEmpty
        let isForward = Direction.Forward = direction
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

    member x.GetQuotedStringData () = 
        let caretPoint,caretLine = TextViewUtil.GetCaretPointAndLine _textView

        // Find the quoted data structure from a given point on the line 
        let getData startPoint = 

            // Find a quote from the given point.  This takes into account escaped quotes
            // and will only return a non-escaped one
            let findQuote point =
                let rec inner point inEscape = 
                    if point = caretLine.End then None
                    else
                        let c = SnapshotPointUtil.GetChar point
                        if c = '\"' then
                            if inEscape then inner (point.Add(1)) false 
                            else Some point
                        elif StringUtil.containsChar _localSettings.QuoteEscape c then 
                            inner (point.Add(1)) true
                        else
                            inner (point.Add(1)) false
                inner point false
    
            match findQuote startPoint with
            | None -> None
            | Some(firstQuote) ->
                match findQuote (firstQuote.Add(1)) with
                | None -> None
                | Some(secondQuote) ->
    
                    let span = SnapshotSpanUtil.Create (SnapshotPointUtil.AddOne firstQuote) secondQuote
                    let isWhiteSpace = SnapshotPointUtil.GetChar >> CharUtil.IsWhiteSpace

                    // Calculate the leading whitespace span.  It includes the white space just
                    // before the leading quote.  The quote is not included in the span
                    let leadingSpan = 
    
                        let isPreviousWhiteSpace point = 
                            if point = caretLine.Start then false
                            else 
                                match SnapshotPointUtil.TrySubtractOne point with
                                | None -> false
                                | Some(prev) -> prev |> isWhiteSpace
    
                        let rec inner start = 
                            if isPreviousWhiteSpace start then start |> SnapshotPointUtil.SubtractOne |> inner
                            else SnapshotSpanUtil.Create start firstQuote
                        inner firstQuote

                    // Calculate the trailing whitespace span
                    let trailingSpan =
                        
                        let isNextWhiteSpace point = 
                            match SnapshotPointUtil.TryAddOne point with
                            | None -> false
                            | Some(next) ->
                                if next.Position >= caretLine.End.Position then false
                                else isWhiteSpace next
    
                        let start = SnapshotPointUtil.AddOne secondQuote
                        let rec inner endPoint =
                            if isNextWhiteSpace endPoint then endPoint |> SnapshotPointUtil.AddOne |> inner
                            elif endPoint = start then SnapshotSpanUtil.CreateEmpty start
                            else 
                                let endPoint = SnapshotPointUtil.AddOne endPoint
                                SnapshotSpanUtil.Create start endPoint
                        inner start

                    {
                        LeadingWhiteSpace = leadingSpan
                        LeadingQuote = firstQuote
                        Contents = span
                        TrailingQuote = secondQuote
                        TrailingWhiteSpace = trailingSpan } |> Some

        // Holds all of the QuotedStringData for the given line.  This doesn't really 
        // understand string infrastructure and will see two quoted strings as three.  
        let all = 
            seq {
                let more = ref true
                let cur = ref caretLine.Start
                while more.Value do
                    match getData cur.Value with
                    | None -> more := false
                    | Some(data) ->
                        yield data
                        cur := data.TrailingQuote 
            }

        // First search for the QuotedStringData who's FullSpan includes the caret 
        // point 
        match all |> Seq.tryFind (fun data -> data.FullSpan.Contains caretPoint) with
        | Some(data) -> Some data
        | None -> SeqUtil.tryHeadOnly all

    member x.Mark c = 
        match _markMap.GetLocalMark _textBuffer c with 
        | None -> 
            None
        | Some (virtualPoint) ->
            let caretPoint = TextViewUtil.GetCaretPoint _textView
            let startPoint, endPoint = SnapshotPointUtil.OrderAscending caretPoint virtualPoint.Position
            let span = SnapshotSpan(startPoint, endPoint)
            {
                Span = span
                IsForward = caretPoint = startPoint
                MotionKind = MotionKind.Exclusive
                OperationKind = OperationKind.CharacterWise
                Column = SnapshotPointUtil.GetColumn virtualPoint.Position |> Some } |> Some

    member x.MarkLine c =
        match _markMap.GetLocalMark _textBuffer c with 
        | None -> 
            None
        | Some (virtualPoint) ->
            let caretPoint = TextViewUtil.GetCaretPoint _textView
            let startPoint, endPoint = SnapshotPointUtil.OrderAscending caretPoint virtualPoint.Position
            let startLine = SnapshotPointUtil.GetContainingLine startPoint
            let endLine = SnapshotPointUtil.GetContainingLine endPoint
            let range = SnapshotLineRangeUtil.CreateForLineRange startLine endLine
            {
                Span = range.ExtentIncludingLineBreak
                IsForward = caretPoint = startPoint
                MotionKind = MotionKind.Inclusive
                OperationKind = OperationKind.LineWise
                Column = 
                    virtualPoint.Position
                    |> SnapshotPointUtil.GetContainingLine
                    |> TssUtil.FindFirstNonWhitespaceCharacter
                    |> SnapshotPointUtil.GetColumn
                    |> Some } |> Some

    interface ITextViewMotionUtil with
        member x.TextView = _textView
        member x.CharSearch c count charSearch direction = 
            match charSearch, direction with
            | CharSearch.ToChar, Direction.Forward -> x.ForwardCharMotionCore c count TssUtil.FindNextOccurranceOfCharOnLine
            | CharSearch.TillChar, Direction.Forward -> x.ForwardCharMotionCore c count TssUtil.FindTillNextOccurranceOfCharOnLine
            | CharSearch.ToChar, Direction.Backward -> x.BackwardCharMotionCore c count TssUtil.FindPreviousOccurranceOfCharOnLine 
            | CharSearch.TillChar, Direction.Backward -> x.BackwardCharMotionCore c count TssUtil.FindTillPreviousOccurranceOfCharOnLine
        member x.Mark c = x.Mark c
        member x.MarkLine c = x.MarkLine c
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

            // Create the appropriate MotionData structure with the provided SnapshotSpan
            let withSpan span = {
                Span=span 
                IsForward=true 
                MotionKind=MotionKind.Inclusive 
                OperationKind=OperationKind.CharacterWise 
                Column=None} 

            // Move forward until we find the first non-blank and hence a word character 
            let point =
                SnapshotPointUtil.GetPoints x.StartPoint SearchKind.Forward
                |> Seq.skipWhile (fun point -> point.GetChar() |> CharUtil.IsWhiteSpace)
                |> SeqUtil.tryHeadOnly
            match point with 
            | None -> 
                SnapshotSpanUtil.CreateFromBounds x.StartPoint (SnapshotUtil.GetEndPoint _textView.TextSnapshot) |> withSpan
            | Some(point) -> 

                // Have a point and we are on a word.  There is a special case to consider where
                // we started on the last character of a word.  In that case we start searching from
                // the next point 
                let searchPoint, count = 
                    match TssUtil.FindCurrentWordSpan point kind with 
                    | None -> point, count
                    | Some(span) -> 
                        if SnapshotSpanUtil.IsLastIncludedPoint span x.StartPoint then (span.End, count)
                        else (span.End, count - 1)

                if count = 0 then 
                    // Getting the search point moved the 1 word count we had.  Done
                    SnapshotSpanUtil.CreateFromBounds x.StartPoint searchPoint |> withSpan
                else 
                    // Need to skip (count - 1) remaining words to get to the one we're looking for
                    let wordSpan = 
                        TssUtil.GetWordSpans searchPoint kind SearchKind.Forward
                        |> SeqUtil.skipMax (count - 1)
                        |> SeqUtil.tryHeadOnly
    
                    match wordSpan with
                    | Some(span) -> 
                        SnapshotSpanUtil.Create x.StartPoint span.End |> withSpan
                    | None -> 
                        let endPoint = SnapshotUtil.GetEndPoint _textView.TextSnapshot
                        SnapshotSpanUtil.Create x.StartPoint endPoint |> withSpan

        member x.EndOfLine count = 
            let start = x.StartPoint
            let span = SnapshotPointUtil.GetLineRangeSpan start count
            {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        member x.FirstNonWhitespaceOnLine () =
            let start = x.StartPoint
            let line = start.GetContainingLine()
            let target = 
                SnapshotLineUtil.GetPoints line
                |> Seq.tryFind (fun x -> not (CharUtil.IsWhiteSpace (x.GetChar())) )
                |> OptionUtil.getOrDefault line.End
            let startPoint,endPoint,isForward = 
                if start.Position <= target.Position then start,target,true
                else target,start,false
            let span = SnapshotSpanUtil.CreateFromBounds startPoint endPoint
            {Span=span; IsForward=isForward; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None} 
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
            let span = SnapshotSpan(line.Start, endLine.EndIncludingLineBreak)
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
            match TextViewUtil.GetCaretPointKind _textView with 
            | PointKind.Normal (point) -> 
                let span = 
                    MotionUtil.GetSentences point Direction.Forward
                    |> Seq.truncate count
                    |> SnapshotSpanUtil.CreateCombined 
                    |> Option.get   // GetSentences must return at least one
                {Span=span; IsForward=true; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None}
            | PointKind.EndPoint(_,point) -> MotionData.CreateEmptyFromPoint point MotionKind.Inclusive OperationKind.CharacterWise
            | PointKind.ZeroLength(point) -> MotionData.CreateEmptyFromPoint point MotionKind.Inclusive OperationKind.CharacterWise
        member x.SentenceBackward count = 
            let caretPoint = TextViewUtil.GetCaretPoint _textView
            let span = 
                MotionUtil.GetSentences caretPoint Direction.Backward
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
                    MotionUtil.GetSentences startPoint Direction.Forward 
                    |> Seq.truncate count
                    |> SnapshotSpanUtil.CreateCombinedOrEmpty caretPoint.Snapshot
            {Span=span; IsForward=true; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.CharacterWise; Column=None}
        member x.ParagraphForward count =
            match TextViewUtil.GetCaretPointKind _textView with
            | PointKind.Normal(point) -> x.GetParagraphs point Direction.Forward count
            | PointKind.EndPoint(point,_) -> x.GetParagraphs point Direction.Forward count
            | PointKind.ZeroLength(point) -> MotionData.CreateEmptyFromPoint point MotionKind.Inclusive OperationKind.CharacterWise
        member x.ParagraphBackward count =
            x.GetParagraphs (TextViewUtil.GetCaretPoint _textView) Direction.Backward count
        member x.ParagraphFullForward count =
            let caretPoint = TextViewUtil.GetCaretPoint _textView
            let span = MotionUtil.GetFullParagraph caretPoint
            x.GetParagraphs span.Start Direction.Forward count 
        member x.SectionForward context count =
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
                            match context with
                            | MotionContext.AfterOperator ->
                                let line = SnapshotPointUtil.GetContainingLine nextPoint
                                line.EndIncludingLineBreak
                            | MotionContext.Movement -> getNext nextPoint
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
        member x.QuotedString () = 
            match x.GetQuotedStringData() with
            | None -> None 
            | Some(data) -> 
                let span = 
                    if not data.TrailingWhiteSpace.IsEmpty then SnapshotSpanUtil.Create data.LeadingQuote data.TrailingWhiteSpace.End
                    else SnapshotSpanUtil.Create data.LeadingWhiteSpace.Start data.TrailingWhiteSpace.Start
                {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.CharacterWise; Column=None} |> Some
        member x.QuotedStringContents () = 
            match x.GetQuotedStringData() with
            | None -> None 
            | Some(data) ->
                let span = data.Contents
                {Span=span; IsForward=true; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.CharacterWise; Column=None} |> Some

    