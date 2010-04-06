#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module internal SnapshotUtil = 
    
    /// Get the last line in the ITextSnapshot.  Avoid pulling the entire buffer into memory
    /// slowly by using the index
    let GetLastLine (tss:ITextSnapshot) =
        let lastIndex = tss.LineCount - 1
        tss.GetLineFromLineNumber(lastIndex)   

    let GetLastLineNumber (tss:ITextSnapshot) = tss.LineCount - 1 
        
    /// Get the end point of the snapshot
    let GetEndPoint (tss:ITextSnapshot) =
        let line = GetLastLine tss
        line.End
        
    /// Get the start point of the snapshot
    let GetStartPoint (tss:ITextSnapshot) =
        let first = tss.GetLineFromLineNumber(0)
        first.Start

    /// Get the line number back if it's valid and if not the last line in the snapshot
    let GetValidLineNumberOrLast (tss:ITextSnapshot) lineNumber = 
        if lineNumber >= tss.LineCount then tss.LineCount-1 else lineNumber

    /// Get a valid line for the specified number if it's valid and the last line if it's
    /// not
    let GetValidLineOrLast (tss:ITextSnapshot) lineNumber =
        let lineNumber = GetValidLineNumberOrLast tss lineNumber
        tss.GetLineFromLineNumber(lineNumber)

    /// Get the lines in the ITextSnapshot as a seq in forward.  
    let GetLinesForward tss startLine wrap =
        let endLine = GetLastLineNumber tss
        let endLine = tss.LineCount - 1
        let forward = seq { for i in startLine .. endLine -> i }
        let range = 
            match wrap with 
                | false -> forward
                | true -> 
                    let front = seq { for i in 0 .. (startLine-1) -> i}
                    Seq.append forward front
        range |> Seq.map (fun x -> tss.GetLineFromLineNumber(x))  
        
    /// Get the lines in the ITextSnapshot as a seq in reverse order
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

    
/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module internal SnapshotSpanUtil =

    let GetPoints (span:SnapshotSpan) = 
        let tss = span.Snapshot 
        let startPos = span.Start.Position
        if span.Length = 0 then Seq.empty 
        else 
            let max = span.Length-1
            seq { for i in 0 .. max do yield SnapshotPoint(tss, startPos+i) }

    let GetPointsBackward (span:SnapshotSpan) =
        let tss = span.Snapshot
        let startPos = span.Start.Position
        let length = span.Length
        seq { for i in 1 .. length do 
                let offset = length - i
                yield SnapshotPoint(tss, startPos + offset) }


/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module internal SnapshotLineUtil =

    let GetExtent (line:ITextSnapshotLine) = line.Extent

    let GetExtentIncludingLineBreak (line:ITextSnapshotLine) = line.ExtentIncludingLineBreak

    /// Get the points on the particular line in order 
    let GetPoints line = GetExtent line |> SnapshotSpanUtil.GetPoints

    /// Get the points on the particular line including the line break
    let GetPointsIncludingLineBreak line = GetExtentIncludingLineBreak line |> SnapshotSpanUtil.GetPoints

    /// Get the points on the particular line in reverse
    let GetPointsBackward line = GetExtent line |> SnapshotSpanUtil.GetPointsBackward

    /// Get the points on the particular line including the line break in reverse
    let GetPointsIncludingLineBreakBackward line = GetExtentIncludingLineBreak line |> SnapshotSpanUtil.GetPointsBackward

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module internal SnapshotPointUtil =
   
    /// Get the ITextSnapshotLine containing the specified SnapshotPoint
    let GetContainingLine (point:SnapshotPoint) = point.GetContainingLine()

    /// Get the ITextSnapshot containing the SnapshotPoin
    let GetSnapshot (point:SnapshotPoint) = point.Snapshot

    /// Get the line range passed in.  If the count of lines exceeds the amount of lines remaining
    /// in the buffer, the span will be truncated to the final line
    let GetLineSpan point =
        let line = GetContainingLine point
        line.Extent

    /// Functions exactly line GetLineRangeSpan except it will include the final line up until
    /// the end of the line break
    let GetLineSpanIncludingLineBreak point =
        let line = GetContainingLine point
        line.ExtentIncludingLineBreak

    // Get the span of the character which is pointed to by the point.  Normally this is a 
    // trivial operation.  The only difficulty if the Point exists on an empty line.  In that
    // case it is the extent of the line
    let GetCharacterSpan point = 
        let line = GetContainingLine point
        let endSpan = new SnapshotSpan(line.End, line.EndIncludingLineBreak)
        match endSpan.Contains(point) with
            | true -> endSpan
            | false -> new SnapshotSpan(point,1)

    /// Get the next point in the buffer without wrap.  Will throw if you run off the end of 
    /// the ITextSnapshot
    let GetNextPoint point = 
        let tss = GetSnapshot point
        let line = GetContainingLine point
        if point.Position >= line.End.Position then
            let num = line.LineNumber+1
            if num = tss.LineCount then point
            else tss.GetLineFromLineNumber(num).Start
        else
            point.Add(1)    

    /// Get the next point in the buffer with wrap
    let GetNextPointWithWrap point = 
        let tss = GetSnapshot point
        let line = GetContainingLine point
        if point.Position >= line.End.Position then
            let num = line.LineNumber+1
            if num = tss.LineCount then SnapshotUtil.GetStartPoint tss
            else tss.GetLineFromLineNumber(num).Start
        else
            point.Add(1)                    

    /// Get the previous point in the buffer with wrap
    let GetPreviousPointWithWrap point = 
        let tss = GetSnapshot point
        let line = GetContainingLine point
        if point.Position = line.Start.Position then
            if line.LineNumber = 0 then SnapshotUtil.GetEndPoint tss
            else tss.GetLineFromLineNumber(line.LineNumber-1).End
        else
            point.Subtract(1)

    /// Get the line range passed in.  If the count of lines exceeds the amount of lines remaining
    /// in the buffer, the span will be truncated to the final line
    let GetLineRangeSpan start count = 
        let startLine = GetContainingLine start
        let tss = startLine.Snapshot
        let last = SnapshotUtil.GetValidLineOrLast tss (startLine.LineNumber+(count-1))
        new SnapshotSpan(start, last.End)

    /// Functions exactly line GetLineRangeSpan except it will include the final line up until
    /// the end of the line break
    let GetLineRangeSpanIncludingLineBreak (start:SnapshotPoint) count =
        let tss = start.Snapshot
        let startLine = start.GetContainingLine()
        let last = SnapshotUtil.GetValidLineOrLast tss (startLine.LineNumber+(count-1))
        new SnapshotSpan(start, last.EndIncludingLineBreak)

    /// Get the line and column information for a given SnapshotPoint
    let GetLineColumn point = 
        let line = GetContainingLine point
        let column = point.Position - line.Start.Position
        (line.LineNumber,column)
    
    /// Get the lines of the containing ITextSnapshot as a seq 
    let GetLines point kind =
        let tss = GetSnapshot point
        let startLine = point.GetContainingLine().LineNumber
        match kind with 
            | SearchKind.Forward -> SnapshotUtil.GetLinesForward tss startLine false
            | SearchKind.ForwardWithWrap -> SnapshotUtil.GetLinesForward tss startLine true
            | SearchKind.Backward -> SnapshotUtil.GetLinesBackward tss startLine false
            | SearchKind.BackwardWithWrap -> SnapshotUtil.GetLinesBackward tss startLine true
            | _ -> failwith "Invalid enum value"

    /// Start searching the snapshot at the given point and return the buffer as a 
    /// sequence of SnapshotSpans.  One will be returned per line in the buffer.  The
    /// only exception is the start line which will be divided at the given start
    /// point
    let GetSpans point kind = 
        let tss = GetSnapshot point
        let startLine = GetContainingLine point
        let inLineBreak = point.Position >= startLine.End.Position 
        let middle = GetLines point kind |> Seq.skip 1 |> Seq.map SnapshotLineUtil.GetExtent

        let getForward wrap = seq {
            if point.Position < startLine.End.Position then yield SnapshotSpan(point, startLine.End)
            yield! middle
            if wrap && point.Position <> startLine.Start.Position  then 
                let endPoint = if inLineBreak then startLine.End else point
                yield SnapshotSpan(startLine.Start, endPoint)
        }
        let getBackward wrap = seq {

            // First line. Don't forget it can be a 0 length line
            if inLineBreak then yield startLine.Extent
            elif startLine.Length > 0 then yield SnapshotSpan(startLine.Start, point.Add(1))

            yield! middle

            if point.Position + 1 < startLine.End.Position then
                let lastSpan = SnapshotSpan(point.Add(1), startLine.End)
                if wrap && lastSpan.Length > 0 then yield lastSpan
        } 
        
        match kind with
            | SearchKind.Forward -> getForward false
            | SearchKind.ForwardWithWrap -> getForward true
            | SearchKind.Backward -> getBackward false
            | SearchKind.BackwardWithWrap -> getBackward true
            | _ -> failwith "Invalid enum value"

    /// Start searching the snapshot at the given point and return the buffer as a 
    /// sequence of SnapshotPoints.  The first point returned will be the point passed
    /// in
    let GetPoints point wrap =
        let kind = if wrap then SearchKind.ForwardWithWrap else SearchKind.Forward
        GetSpans point kind 
        |> Seq.map SnapshotSpanUtil.GetPoints
        |> Seq.concat       

    /// Start searching the snapshot at the given point and return the buffer as a 
    /// sequence of SnapshotPoints in reverse.  The first point returned will be 
    /// the point passed in
    let GetPointsBackward point wrap =
        let kind = if wrap then SearchKind.BackwardWithWrap else SearchKind.Backward
        GetSpans point kind
        |> Seq.map SnapshotSpanUtil.GetPointsBackward 
        |> Seq.concat

    /// Get the character associated with the current point.  Returns None for the last character
    /// in the buffer which has no representable value
    let TryGetChar point = 
        let tss = GetSnapshot point
        if point = SnapshotUtil.GetEndPoint tss then None
        else point.GetChar() |> Some

    /// Try and get the character associated with the current point.  If the point does not point to
    /// a valid character in the buffer then the defaultValue will be returned
    let GetCharOrDefault point defaultValue =
        match TryGetChar point with
        | Some(c) -> c
        | None -> defaultValue

    /// Get points on the containing line starting from the passed in value.  Will return 
    /// "count" points or the number of remaining points on the line, whichever is fewer
    let GetPointsOnContainingLine point count =
        let line = GetContainingLine point
        let length = line.End.Position - point.Position
        let length = min count length
        let span = SnapshotSpan(point, length)
        SnapshotSpanUtil.GetPoints span
