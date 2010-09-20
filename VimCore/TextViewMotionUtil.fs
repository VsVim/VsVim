#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

/// Used for the paragraph motion.  
[<RequireQualifiedAccess>]
type Paragraph =
    /// Actual content of the paragraph.  
    | Content of SnapshotSpan

    /// The ITextSnapshotLine which is a boundary item for the paragraph
    | Boundary of int * SnapshotSpan

    with 

    /// What is the Span of this paragraph 
    member x.Span = 
        match x with 
        | Paragraph.Content(span) -> span
        | Paragraph.Boundary(_,span) -> span

    member x.ReduceToSpan span =
        match x with
        | Paragraph.Content(_) -> Paragraph.Content(span)
        | Paragraph.Boundary(line,_) -> Paragraph.Boundary(line,span)

/// Contains motion actions which are generally useful to components
module MotionUtil = 

    /// Get the paragraphs starting at the given SnapshotPoint
    let GetParagraphs point kind = 

        // Get the paragraphs for the given span in a forward fashion
        let forSpanForward span = 
            seq { 
                let leadingEdge,lines,trailingEdge = SnapshotSpanUtil.GetLinesAndEdges span
                let startPoint = ref span.Start
                for line in lines do
                    if line.Length = 0 then 
                        // Guard against back to back newlines 
                        if !startPoint <> line.Start then 
                            yield SnapshotSpan(!startPoint, line.Start) |> Paragraph.Content

                        yield Paragraph.Boundary (line.LineNumber, line.ExtentIncludingLineBreak)
                        startPoint := line.EndIncludingLineBreak
                if !startPoint <> span.End then 
                    yield SnapshotSpan(!startPoint, span.End) |> Paragraph.Content
            }

        // Get the sentences for the given span in a backward fashion
        let forSpanBackward span = 
            seq { 
                let leadingEdge,lines,trailingEdge = SnapshotSpanUtil.GetLinesAndEdges span
                let endPoint = ref span.End
                for line in lines |> List.ofSeq |> List.rev do
                    if line.Length = 0 then 
                        // Guard against back to back newlines 
                        if !endPoint <> line.EndIncludingLineBreak then
                            yield SnapshotSpan(line.EndIncludingLineBreak, !endPoint) |> Paragraph.Content

                        yield Paragraph.Boundary (line.LineNumber, line.ExtentIncludingLineBreak)
                        endPoint := line.Start
                if !endPoint <> span.Start then 
                    yield SnapshotSpan(span.Start, !endPoint) |> Paragraph.Content
            }

        let mapSpan = 
            if SearchKindUtil.IsForward kind then forSpanForward
            else forSpanBackward 
        SnapshotPointUtil.GetDividedSnapshotSpans point kind
        |> Seq.map mapSpan
        |> Seq.concat

    /// Get the paragraphs which are contained within the specified SnapshotSpan
    let GetParagraphsInSpan span kind = 
        let kind = SearchKindUtil.RemoveWrap kind
        if SearchKindUtil.IsForward kind then 
            seq {
                let start = SnapshotSpanUtil.GetStartPoint span
                let spans = 
                    GetParagraphs start kind 
                    |> Seq.takeWhile (fun p -> p.Span.Start.Position < span.End.Position)
                for item in spans do
                    if item.Span.End.Position > span.End.Position then 
                        yield item.ReduceToSpan (SnapshotSpan(item.Span.Start, span.End))
                    else yield item
            }
        else 
            seq {
                let endPoint = SnapshotSpanUtil.GetEndPoint span
                let spans = 
                    GetParagraphs endPoint kind 
                    |> Seq.takeWhile (fun p -> p.Span.End.Position > span.Start.Position)
                for item in spans do
                    if item.Span.Start.Position < span.Start.Position then
                        yield item.ReduceToSpan (SnapshotSpan(span.Start, item.Span.End))
                    else yield item
            }

    /// Get the full paragrah.  This operation doesn't distinguish boundaries so we
    /// just return the span
    let GetFullParagraph (point:SnapshotPoint) =

        let isContent p =
            match p with 
            | Paragraph.Content(_) -> true
            | Paragraph.Boundary(_) -> false

        let isBoundary p =
            match p with 
            | Paragraph.Content(_) -> false
            | Paragraph.Boundary(_) -> true

        // Get the span when the point is not inside or at the end
        // of a content portion.  It is instead inside of a boundary.  The
        // 'start' param points to the end of the previous content boundary 
        let findFromBoundary start = 
            let endPoint = 
                GetParagraphs start SearchKind.Forward
                |> Seq.skipWhile isBoundary
                |> Seq.map ( fun p -> p.Span.End )
                |> SeqUtil.tryHeadOnly
                |> OptionUtil.getOrDefault (SnapshotUtil.GetEndPoint point.Snapshot)
            SnapshotSpan(start,endPoint)

        // Get the span when the point is at the end of a Content portion 
        // of a paragraph. 
        let findFromContentEnd () = findFromBoundary point

        // Get the span when the point is within a Content portion of a 
        // paragraph.  Take the content regino and all of the trailing Bonudary
        // until the next Content portion.  The 'start' param points to the 
        // start of the Content span
        let findFromContentBody start =
            let endPoint =
                GetParagraphs start SearchKind.Forward 
                |> Seq.skip 1 // The Content 
                |> Seq.skipWhile isBoundary
                |> Seq.map (fun p -> p.Span.Start )
                |> SeqUtil.tryHeadOnly
                |> OptionUtil.getOrDefault (SnapshotUtil.GetEndPoint point.Snapshot)
            SnapshotSpan(start, endPoint)

        // Deteremine where the point is so we can do the proper calculation
        match GetParagraphs point SearchKind.Backward |> SeqUtil.tryHeadOnly with
        | None -> findFromBoundary (SnapshotUtil.GetStartPoint point.Snapshot)
        | Some(p)->
            match p with
            | Paragraph.Boundary(_,span) -> 
                let start = 
                    GetParagraphs point SearchKind.Backward
                    |> Seq.skipWhile isBoundary
                    |> Seq.map (fun p -> p.Span.End)
                    |> SeqUtil.tryHeadOnly
                    |> OptionUtil.getOrDefault (SnapshotUtil.GetStartPoint point.Snapshot)
                findFromBoundary start
            | Paragraph.Content(span) -> 
                let fullSpan = 
                    GetParagraphs span.Start SearchKind.Forward
                    |> Seq.map (fun p -> p.Span)
                    |> SeqUtil.tryHeadOnly
                    |> Option.get
                if fullSpan.End = point then findFromContentEnd()
                else findFromContentBody fullSpan.Start

    /// Set of characters which represent the end of a sentence. 
    let private SentenceEndChars = ['.'; '!'; '?']

    /// Set of characters which can validly follow a sentence 
    let private SentenceTrailingChars = [')';']'; '"'; '\'']

    /// Set of characters which must be after the End to truly (and finally) end
    /// the sentence. 
    let private SentenceAfterEndChars = [' '; '\t'; '\r'; '\n']

    /// Get the sentences starting at the given SnapshotPoint
    let GetSentences point kind = 

        // Get the sentences for the given span in a forward fashion
        let forSpanForward span = 
            seq {
                let startPoint = ref (SnapshotSpanUtil.GetStartPoint span)
                let inEnd = ref false
                for point in SnapshotSpanUtil.GetPoints span do
                    let char = SnapshotPointUtil.GetChar point
                    if not !inEnd then 
                        if ListUtil.contains char SentenceEndChars then inEnd := true
                        else ()
                    else 
                        if ListUtil.contains char SentenceTrailingChars then ()
                        elif ListUtil.contains char SentenceAfterEndChars then 
                            yield SnapshotSpan(!startPoint,point)
                            startPoint := point
                            inEnd := false
                        else
                            inEnd := false

                // Catch the remainder of the span which may not end in a sentence delimeter
                if !startPoint <> span.End then yield SnapshotSpan(!startPoint,span.End)
            }

        // Get the sentences for the given span in a backward fashion
        let forSpanBackward span = 
            seq {
                // Start of the previous sentence point.  Should be the end point
                // of the next returned SnapshotSpan
                let endPoint = ref (SnapshotSpanUtil.GetEndPoint span)

                // If we see a tail character while enumerating backwards this will
                // note the point before the first tail character
                let tailPoint : SnapshotPoint option ref = ref None

                for point in SnapshotSpanUtil.GetPointsBackward span do
                    let char = SnapshotPointUtil.GetChar point

                    // Is the character to the right a char which truly terminates the 
                    // sentence
                    let isRightAfterEndChar = 
                        let right = SnapshotPointUtil.AddOne point
                        if SnapshotPointUtil.IsEndPoint right then false
                        else 
                            let rightChar = SnapshotPointUtil.GetChar right
                            ListUtil.contains rightChar SentenceAfterEndChars

                    // If this is an end character and the character to the right is a terminating
                    // character or we have tail point then it's a sentence end
                    if ListUtil.contains char SentenceEndChars && (isRightAfterEndChar || Option.isSome !tailPoint) then
                        let startPoint = 
                            match !tailPoint with
                            | None -> point.Add(1) // don't include the end char
                            | Some(t) -> t
                        if startPoint <> !endPoint then
                            yield SnapshotSpan(startPoint, !endPoint)
                            endPoint := startPoint
                            tailPoint := None
                    elif ListUtil.contains char SentenceTrailingChars && Option.isNone !tailPoint && isRightAfterEndChar then
                        tailPoint := Some (point.Add(1))
                    else 
                        tailPoint := None

                // Get the begining of the span
                if !endPoint <> span.Start then yield SnapshotSpan(span.Start, !endPoint)
            }

        seq {
            let mapSpan = 
                if SearchKindUtil.IsForward kind then forSpanForward
                else forSpanBackward 

            let items = 
                SnapshotPointUtil.GetDividedSnapshotSpans point kind
                |> Seq.map (fun span -> GetParagraphsInSpan span kind)
                |> Seq.concat

            for item in items do
                match item with
                | Paragraph.Boundary(_,span) -> yield span
                | Paragraph.Content(span) -> yield! mapSpan span 
        }

    /// Get the full sentence on which the given point resides
    let GetSentenceFull point = 
        GetSentences point SearchKind.Backward
        |> Seq.tryFind (fun x -> x.Contains(point))
        |> OptionUtil.getOrDefault (SnapshotSpanUtil.CreateFromStartToProvidedEnd point)


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
            |> Option.get
        let isForward = SearchKindUtil.IsForward kind
        {Span=span; IsForward=isForward; MotionKind=MotionKind.Exclusive; OperationKind=OperationKind.LineWise; Column=None}

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
