#light
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations

module internal TssUtil =

    /// Get the last line in the ITextSnapshot.  Avoid pulling the entire buffer into memory
    /// slowly by using the index
    let GetLastLine (tss:ITextSnapshot) =
        let lastIndex = tss.LineCount - 1
        tss.GetLineFromLineNumber(lastIndex)   
        
    /// Get the end point of the snapshot
    let GetEndPoint (tss:ITextSnapshot) =
        let line = GetLastLine tss
        line.End
        
    /// Get the start point of the snapshot
    let GetStartPoint (tss:ITextSnapshot) =
        let first = tss.GetLineFromLineNumber(0)
        first.Start

    let GetLineExtent (line:ITextSnapshotLine) = line.Extent

    let GetLineExtentIncludingLineBreak (line:ITextSnapshotLine) = line.ExtentIncludingLineBreak

    let GetContainingLine (point:SnapshotPoint) = point.GetContainingLine()

    let GetPoints line =
        let span = GetLineExtent line
        seq { for i in 0 .. span.Length do yield span.Start.Add(i) }

    let GetPointsIncludingLineBreak line = 
        let span = GetLineExtentIncludingLineBreak line
        seq { for i in 0 .. span.Length do yield span.Start.Add(i) }

    // Quick and dirty ternary on the general direction of the search (forward or backwards)
    let SearchDirection kind forwardRes backwardRes = 
        match kind with
            | SearchKind.Forward -> forwardRes
            | SearchKind.ForwardWithWrap -> forwardRes
            | SearchKind.Backward -> backwardRes
            | SearchKind.BackwardWithWrap -> backwardRes
            | _ -> failwith "Invalid enum value"
        
    let GetLinesForward (tss : ITextSnapshot) startLine wrap =
        let endLine = tss.LineCount - 1
        let forward = seq { for i in startLine .. endLine -> i }
        let range = 
            match wrap with 
                | false -> forward
                | true -> 
                    let front = seq { for i in 0 .. (startLine-1) -> i}
                    Seq.append forward front
        range |> Seq.map (fun x -> tss.GetLineFromLineNumber(x))  
        
    let GetLinesBackward (tss : ITextSnapshot) startLine wrap =
        let rev s = s |> List.ofSeq |> List.rev |> Seq.ofList
        let endLine = tss.LineCount - 1
        let all = seq { for i in 0 .. endLine -> i }
        let backward = all |> Seq.take (startLine+1) |> rev
        let range =               
            match wrap with 
                | false -> backward 
                | true ->
                    let tail = seq { for i in (startLine+1) .. endLine -> i } |> rev
                    Seq.append backward tail
        range |> Seq.map (fun x -> tss.GetLineFromLineNumber(x))                     
    
    let GetLines (point : SnapshotPoint) kind =
        let tss = point.Snapshot
        let startLine = point.GetContainingLine().LineNumber
        match kind with 
            | SearchKind.Forward -> GetLinesForward tss startLine false
            | SearchKind.ForwardWithWrap -> GetLinesForward tss startLine true
            | SearchKind.Backward -> GetLinesBackward tss startLine false
            | SearchKind.BackwardWithWrap -> GetLinesBackward tss startLine true
            | _ -> failwith "Invalid enum value"
            
    let GetSpans (point: SnapshotPoint) kind =
        let tss = point.Snapshot
        let startLine = point.GetContainingLine()
        
        // If the point is within the exetend of the line push it back to the end 
        // of the line
        let point = if point.Position > startLine.End.Position then startLine.End else point
        
        let middle = GetLines point kind |> Seq.skip 1 |> Seq.map (fun l -> l.Extent)
        let forward = seq {
            yield new SnapshotSpan(point, startLine.End)
            yield! middle
            
            // Be careful not to wrap unless specified
            if startLine.Start <> point  && kind = SearchKind.ForwardWithWrap then
                yield new SnapshotSpan(startLine.Start, point)
            }
        let backward = seq {
            yield new SnapshotSpan(startLine.Start, point)
            yield! middle
            
            // Be careful not to wrap unless specified
            if startLine.End <> point && kind = SearchKind.BackwardWithWrap then
                yield new SnapshotSpan(point, startLine.End)
            }
        SearchDirection kind forward backward
       
    let ValidPos (tss:ITextSnapshot) start = 
        if start < 0 then 
            0
        else if start >= tss.Length then
            tss.Length - 1
        else    
            start
            
    let ValidLine (tss:ITextSnapshot) line =
        if line < 0 then
            0
        else if line >= tss.LineCount then
            tss.LineCount-1
        else
            line

    let VimLineToTssLine line = 
        match line with
            | 0 -> 0
            | _ -> line-1

    let GetValidLineNumberOrLast (tss:ITextSnapshot) lineNumber = 
        if lineNumber >= tss.LineCount then tss.LineCount-1 else lineNumber

    let GetValidLineOrLast (tss:ITextSnapshot) lineNumber =
        let lineNumber = GetValidLineNumberOrLast tss lineNumber
        tss.GetLineFromLineNumber(lineNumber)

    let GetLineRangeSpan start count = 
        let startLine = GetContainingLine start
        let tss = startLine.Snapshot
        let last = GetValidLineOrLast tss (startLine.LineNumber+(count-1))
        new SnapshotSpan(start, last.End)

    let GetLineRangeSpanIncludingLineBreak (start:SnapshotPoint) count =
        let tss = start.Snapshot
        let startLine = start.GetContainingLine()
        let last = GetValidLineOrLast tss (startLine.LineNumber+(count-1))
        new SnapshotSpan(start, last.EndIncludingLineBreak)
            
    /// Wrap the TextUtil functions which operate on String and int locations into 
    /// a SnapshotPoint and SnapshotSpan version
    let WrapTextSearch (func: WordKind -> string -> int -> option<Span> ) = 
        let f kind point = 
            let line = GetContainingLine point
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
                        | Some s -> GetSpans (s.End) SearchKind.Forward
                        | None -> GetSpans point SearchKind.Forward
        let found = spans |> Seq.tryPick (fun x -> FindAnyWordSpan x kind SearchKind.Forward)
        match found with
            | Some s -> s
            | None -> SnapshotSpan((GetEndPoint (point.Snapshot)), 0)
                        
            
    let FindPreviousWordSpan (point:SnapshotPoint) kind = 
        let startSpan = new SnapshotSpan(GetStartPoint (point.Snapshot), 0)
        let fullSearch p2 =
             let s = GetSpans p2 SearchKind.Backward |> Seq.tryPick (fun x -> FindAnyWordSpan x kind SearchKind.Backward)                                
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

    let FindNextWordPosition point kind =
        let span = FindNextWordSpan point kind
        span.Start
           
    let FindPreviousWordPosition point kind  =
        let span = FindPreviousWordSpan point kind 
        span.Start
            
    let FindIndentPosition (line:ITextSnapshotLine) =
        let text = line.GetText()
        match text |> Seq.tryFindIndex (fun c -> not (System.Char.IsWhiteSpace(c))) with
            | Some i -> i
            | None -> 0
        
    let GetCharacterSpan (point:SnapshotPoint) =
        let line = point.GetContainingLine()
        let endSpan = new SnapshotSpan(line.End, line.EndIncludingLineBreak)
        match endSpan.Contains(point) with
            | true -> endSpan
            | false -> new SnapshotSpan(point,1)

    let GetReverseCharacterSpan (point:SnapshotPoint) count =
        let line = point.GetContainingLine()
        let diff = line.Start.Position - count
        if line.Start.Position = point.Position then new SnapshotSpan(point,point)
        elif diff < 0 then new SnapshotSpan(line.Start, point)
        else new SnapshotSpan(point.Subtract(count), point)
            
    let GetNextPoint (point:SnapshotPoint) =
        let tss = point.Snapshot
        let line = point.GetContainingLine()
        if point.Position >= line.End.Position then
            let num = line.LineNumber+1
            if num = tss.LineCount then point
            else tss.GetLineFromLineNumber(num).Start
        else
            point.Add(1)    

    let GetNextPointWithWrap (point:SnapshotPoint) =
        let tss = point.Snapshot
        let line = point.GetContainingLine()
        if point.Position >= line.End.Position then
            let num = line.LineNumber+1
            if num = tss.LineCount then GetStartPoint tss
            else tss.GetLineFromLineNumber(num).Start
        else
            point.Add(1)                    

    let GetPreviousPointWithWrap (point:SnapshotPoint) =
        let tss = point.Snapshot
        let line = point.GetContainingLine()
        if point.Position = line.Start.Position then
            if line.LineNumber = 0 then GetEndPoint tss
            else tss.GetLineFromLineNumber(line.LineNumber-1).End
        else
            point.Subtract(1)

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

        GetSpans point searchKind 
            |> Seq.map getForSpan
            |> Seq.concat

    let FindFirstNonWhitespaceCharacter (line:ITextSnapshotLine) = 
        let rec inner (cur:SnapshotPoint) = 
            if cur.Position >= line.End.Position then cur
            elif not (CharUtil.IsWhiteSpace (cur.GetChar())) then cur
            else inner (cur.Add(1))
        inner line.Start

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

    let GetLineColumn (point:SnapshotPoint) =
        let line = point.GetContainingLine()
        let column = point.Position - line.Start.Position
        (line.LineNumber,column)

    let SafeGetTrackingSpan (trackingSpan:ITrackingSpan) (snapshot:ITextSnapshot) =
        try
            let span = trackingSpan.GetSpan(snapshot)
            Some(span)
        with
            | :? System.ArgumentException -> None
