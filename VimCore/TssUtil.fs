#light
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open CharUtil

module TssUtil =

    let ValidPos (tss:ITextSnapshot) start = 
        if start < 0 then 0
        else if start >= tss.Length then tss.Length - 1
        else start
            
    let ValidLine (tss:ITextSnapshot) line =
        if line < 0 then 0
        else if line >= tss.LineCount then tss.LineCount-1
        else line

    let VimLineToTssLine line = 
        match line with
            | 0 -> 0
            | _ -> line-1

    /// Wrap the TextUtil functions which operate on String and int locations into 
    /// a SnapshotPoint and SnapshotSpan version
    let WrapTextSearch (func: WordKind -> string -> int -> option<Span> ) = 
        let f kind point = 
            let line = SnapshotPointUtil.GetContainingLine point
            let text = line.GetText()
            let pos = point.Position - line.Start.Position
            match pos >= text.Length with
                | true -> None
                | false ->
                match func kind text pos with
                    | Some s ->
                        let adjusted = new Span(line.Start.Position+s.Start, s.Length)
                        Some (new SnapshotSpan(point.Snapshot, adjusted))
                    | None -> None
        f
        
    let FindCurrentWordSpan point kind = (WrapTextSearch TextUtil.FindCurrentWordSpan) kind point
    let FindCurrentFullWordSpan point kind = (WrapTextSearch TextUtil.FindFullWordSpan) kind point
    
    let FindAnyWordSpan (span:SnapshotSpan) wordKind searchKind = 
        let opt = 
            if SearchKindUtil.IsForward searchKind then
                match FindCurrentWordSpan span.Start wordKind with
                | Some s -> Some s
                | None -> 
                    let pred = WrapTextSearch TextUtil.FindNextWordSpan 
                    pred wordKind span.Start 
            else
                match span.Length > 0 with
                | false -> None
                | true ->
                let point = span.End.Subtract(1)
                match FindCurrentFullWordSpan point wordKind with
                    | Some s -> Some s
                    | None -> 
                        let pred = WrapTextSearch TextUtil.FindPreviousWordSpan
                        pred wordKind point
        match opt with
        | Some(fullSpan) -> 
            let value = span.Intersection(fullSpan)
            if value.HasValue then Some value.Value else None
        | None -> None
    
    let FindNextWordSpan point kind =
        let spans = match FindCurrentWordSpan point kind with
                        | Some s -> SnapshotPointUtil.GetSpans (s.End) SearchKind.Forward
                        | None -> SnapshotPointUtil.GetSpans point SearchKind.Forward
        let found = spans |> Seq.tryPick (fun x -> FindAnyWordSpan x kind SearchKind.Forward)
        match found with
            | Some s -> s
            | None -> SnapshotSpan((SnapshotUtil.GetEndPoint (point.Snapshot)), 0)
                        
            
    let FindPreviousWordSpan (point:SnapshotPoint) kind = 
        let startSpan = new SnapshotSpan(SnapshotUtil.GetStartPoint (point.Snapshot), 0)
        let fullSearch p2 =
             let s = SnapshotPointUtil.GetSpans p2 SearchKind.Backward |> Seq.tryPick (fun x -> FindAnyWordSpan x kind SearchKind.Backward)                                
             match s with 
                | Some span -> span
                | None -> startSpan
        match FindCurrentFullWordSpan point kind with
            | Some s ->
                match s.Start = point with
                    | false -> s
                    | true -> 
                        let line = point.GetContainingLine()
                        let num = line.LineNumber
                        if line.Start <> point then fullSearch (point.Subtract(1))
                        elif num > 0 then fullSearch (point.Snapshot.GetLineFromLineNumber(num-1).End)
                        else startSpan
            | None -> fullSearch point

    let FindNextWordStart point count kind =
        let rec inner point count = 
            if count = 0 then point
            else 
                let span = FindNextWordSpan point kind
                let nextPos = span.Start
                inner nextPos (count-1)
        inner point count 

    let FindPreviousWordStart point count kind  =
        let rec inner point count =
            if count = 0 then point
            else 
                let span = FindPreviousWordSpan point kind 
                let prevPos = span.Start
                inner prevPos (count-1)
        inner point count 

    let FindIndentPosition (line:ITextSnapshotLine) tabSize =
        SnapshotSpanUtil.GetPoints line.Extent
        |> Seq.map (fun p -> p.GetChar())
        |> Seq.takeWhile CharUtil.IsWhiteSpace
        |> Seq.fold (fun acc c -> acc + (if c = '\t' then tabSize else 1)) 0
        
    let GetReverseCharacterSpan (point:SnapshotPoint) count =
        let line = point.GetContainingLine()
        let diff = line.Start.Position - count
        if line.Start.Position = point.Position then new SnapshotSpan(point,point)
        elif diff < 0 then new SnapshotSpan(line.Start, point)
        else new SnapshotSpan(point.Subtract(count), point)

    let GetWordSpans point wordKind searchKind = 
        let getForSpanForward span = 
            span
            |> Seq.unfold (fun cur -> 
                match FindAnyWordSpan cur wordKind searchKind with
                | Some(nextSpan) -> Some(nextSpan, SnapshotSpan(nextSpan.End, cur.End))
                | None -> None )
        let getForSpanBackwards span =
            span
            |> Seq.unfold (fun cur ->
                match FindAnyWordSpan cur wordKind searchKind with
                | Some(prevSpan) -> Some(prevSpan, SnapshotSpan(span.Start, prevSpan.Start))
                | None -> None )

        let getForSpan span = 
            if SearchKindUtil.IsForward searchKind then getForSpanForward span
            else getForSpanBackwards span

        SnapshotPointUtil.GetSpans point searchKind 
            |> Seq.map getForSpan
            |> Seq.concat

    let FindFirstNonWhitespaceCharacter (line:ITextSnapshotLine) = 
        line
        |> SnapshotLineUtil.GetPoints
        |> SeqUtil.tryFindOrDefault (fun p -> not (CharUtil.IsWhiteSpace (p.GetChar()))) (line.Start)

    let FindLastNonWhitespaceCharacter line =
        line
        |> SnapshotLineUtil.GetPointsBackward
        |> SeqUtil.tryFindOrDefault (fun p -> not (CharUtil.IsWhiteSpace (p.GetChar()))) (line.Start)

    let CreateTextStructureNavigator wordKind (baseImpl:ITextStructureNavigator) = 
        { new ITextStructureNavigator with 
            member x.ContentType = baseImpl.ContentType
            member x.GetExtentOfWord point = 
                match FindCurrentFullWordSpan point wordKind with
                | Some(span) -> TextExtent(span, true)
                | None -> TextExtent(SnapshotSpan(point,1),false)
            member x.GetSpanOfEnclosing span = baseImpl.GetSpanOfEnclosing(span)
            member x.GetSpanOfFirstChild span = baseImpl.GetSpanOfFirstChild(span)
            member x.GetSpanOfNextSibling span = baseImpl.GetSpanOfNextSibling(span)
            member x.GetSpanOfPreviousSibling span = baseImpl.GetSpanOfPreviousSibling(span) }

    let FindNextOccurranceOfCharOnLine point targetChar count = 
        match SnapshotPointUtil.TryGetNextPointOnLine point with
        | None -> None
        | Some(point) ->
            let matches =
                SnapshotPointUtil.GetPointsOnContainingLineFrom point
                |> Seq.filter (fun p -> p.GetChar() = targetChar)
                |> List.ofSeq
            let index = count - 1 
            if index < matches.Length then List.nth matches index |> Some
            else None

    let FindTillNextOccurranceOfCharOnLine point targetChar count =
        match FindNextOccurranceOfCharOnLine point targetChar count with
        | None -> None
        | Some(point) -> SnapshotPointUtil.TryGetPreviousPointOnLine point

    let FindPreviousOccurranceOfCharOnLine point targetChar count =
        match SnapshotPointUtil.TryGetPreviousPointOnLine point with
        | None -> None
        | Some(point) ->
            let matches =
                SnapshotPointUtil.GetPointsOnContainingLineBackwardsFrom point
                |> Seq.filter (fun p -> p.GetChar() = targetChar)
                |> List.ofSeq
            let index = count - 1
            if index < matches.Length then List.nth matches index |> Some
            else None

    let FindTillPreviousOccurranceOfCharOnLine point targetChar count =
        match FindPreviousOccurranceOfCharOnLine point targetChar count with
        | None -> None
        | Some(point) -> SnapshotPointUtil.TryGetNextPointOnLine point

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

    let GetFullParagraph point =
        // First move backwards to the end of the previous paragraph 
        let searchStart = 
            let opt = 
                GetParagraphs point SearchKind.Backward 
                |> Seq.skipWhile (fun p -> 
                    match p with
                    | Paragraph.Boundary(_) -> true
                    | Paragraph.Content(_) -> false)
                |> SeqUtil.tryHeadOnly
            match opt with 
            | None -> SnapshotUtil.GetStartPoint point.Snapshot
            | Some(p) -> p.Span.Start

        SnapshotSpan(point,0)
        (*
        // Now find the start and end position 
        let endPoint =
            
            // Get it down to a list so we can do proper inspection without constantly
            // recalculating paragraphs
            let list =
                GetParagraphs start SearchKind.Forward 
                |> SeqUtil.takeWhileWithState false (fun p -> 
                    match p with
                    | Paragraph.Boundary(_) -> true
                    | Paragraph.Content(span) -> span.Contains(point) || span.End == point )
                |> List.ofSeq

            // It's possible point was at the end of a Content boundary.  Skip that here
            (*et list = 
                match list with
                | [] -> list
                | h::t -> list *)

            // |> Seq.map (fun p -> p.Span.End)
            // |> SeqUtil.tryHeadOnly
            // |> OptionUtil.getOrDefault (SnapshotUtil.GetEndPoint point.Snapshot)

        SnapshotSpan(start, endPoint)*)


    /// Set of characters which represent the end of a sentence. 
    let SentenceEndChars = ['.'; '!'; '?']

    /// Set of characters which can validly follow a sentence 
    let SentenceTrailingChars = [')';']'; '"'; '\'']

    /// Set of characters which must be after the End to truly (and finally) end
    /// the sentence. 
    let SentenceAfterEndChars = [' '; '\t'; '\r'; '\n']

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


    let GetSentenceFull point = 
        GetSentences point SearchKind.Backward
        |> Seq.tryFind (fun x -> x.Contains(point))
        |> OptionUtil.getOrDefault (SnapshotSpanUtil.CreateFromStartToProvidedEnd point)

