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

    let SentenceEndChars = ['.'; '!'; '?']
    let SentenceTrailingChars = [')';']'; '"'; '\'']

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
                        else
                            yield SnapshotSpan(!startPoint,point)
                            startPoint := point
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
                    if ListUtil.contains char SentenceEndChars then
                        let startPoint = 
                            match !tailPoint with
                            | None -> point.Add(1) // don't include the end char
                            | Some(t) -> t
                        if startPoint <> !endPoint then
                            yield SnapshotSpan(startPoint, !endPoint)
                            endPoint := startPoint
                            tailPoint := None
                    elif ListUtil.contains char SentenceTrailingChars && Option.isNone !tailPoint then
                        tailPoint := Some (point.Add(1))
                    else 
                        tailPoint := None

                // Get the begining of the span
                if !endPoint <> span.Start then yield SnapshotSpan(span.Start, !endPoint)
            }

        let snapshot = SnapshotPointUtil.GetSnapshot point
        let above = SnapshotSpan(SnapshotUtil.GetStartPoint snapshot, point)
        let below = SnapshotSpan(point, SnapshotUtil.GetEndPoint snapshot)
        match kind with
        | SearchKind.Forward -> below |> forSpanForward
        | SearchKind.ForwardWithWrap -> [below; above] |> Seq.map forSpanForward |> Seq.concat
        | SearchKind.Backward -> above |> forSpanBackward
        | SearchKind.BackwardWithWrap -> [above; below] |> Seq.map forSpanBackward |> Seq.concat
        | _ -> failwith ""
            


