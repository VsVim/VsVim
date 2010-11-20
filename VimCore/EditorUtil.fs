#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module SnapshotUtil = 
    
    /// Get the last line in the ITextSnapshot.  Avoid pulling the entire buffer into memory
    /// slowly by using the index
    let GetLastLine (tss:ITextSnapshot) =
        let lastIndex = tss.LineCount - 1
        tss.GetLineFromLineNumber(lastIndex)   

    /// Get the line for the specified number
    let GetLine (tss:ITextSnapshot) lineNumber = tss.GetLineFromLineNumber lineNumber

    /// Get the first line in the snapshot
    let GetFirstLine tss = GetLine tss 0

    let GetLastLineNumber (tss:ITextSnapshot) = tss.LineCount - 1 

    /// Get the end point of the snapshot
    let GetEndPoint (tss:ITextSnapshot) = SnapshotPoint(tss, tss.Length)

    /// Get the start point of the snapshot
    let GetStartPoint (tss:ITextSnapshot) = SnapshotPoint(tss, 0)

    /// Get the full span of the buffer 
    let GetFullSpan tss = 
        let startPoint = GetStartPoint tss
        let endPoint = GetEndPoint tss
        SnapshotSpan(startPoint,endPoint)

    /// Is the Line Number valid
    let IsLineNumberValid (tss:ITextSnapshot) lineNumber = lineNumber >= 0 && lineNumber < tss.LineCount

    /// Is the Span valid in this ITextSnapshot
    let IsSpanValid (tss:ITextSnapshot) (span:Span) = 
        let length = tss.Length
        span.Start < tss.Length && span.End <= tss.Length

    /// Get a valid line for the specified number if it's valid and the last line if it's
    /// not
    let GetLineOrLast tss lineNumber =
        let lineNumber = if IsLineNumberValid tss lineNumber then lineNumber else GetLastLineNumber tss 
        tss.GetLineFromLineNumber(lineNumber)

    /// Get a valid line for the specified number if it's valid and the last line if it's
    /// not
    let GetLineOrFirst tss lineNumber =
        let lineNumber = if IsLineNumberValid tss lineNumber then lineNumber else 0
        tss.GetLineFromLineNumber(lineNumber)

    /// Get the lines in the ITextSnapshot as a seq in forward fashion
    let private GetLinesForwardCore tss startLine wrap =
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
    let private GetLinesBackwardCore (tss : ITextSnapshot) startLine wrap =
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

    /// Get the lines in the buffer with the specified direction
    let GetLines tss startLine kind =
        match kind with 
        | SearchKind.Forward -> GetLinesForwardCore tss startLine false
        | SearchKind.ForwardWithWrap -> GetLinesForwardCore tss startLine true
        | SearchKind.Backward -> GetLinesBackwardCore tss startLine false
        | SearchKind.BackwardWithWrap -> GetLinesBackwardCore tss startLine true
        | _ -> failwith "Invalid enum value"

    /// Get the lines in the specified range
    let GetLineRange snapshot startLineNumber endLineNumber = 
        let count = endLineNumber - startLineNumber + 1
        GetLines snapshot startLineNumber SearchKind.Forward
        |> Seq.truncate count


/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module SnapshotSpanUtil =

    /// Get the start point
    let GetStartPoint (span:SnapshotSpan) = span.Start

    /// Get the start position
    let GetStartPosition (span:SnapshotSpan) = span.Start.Position

    /// Get the end point
    let GetEndPoint (span:SnapshotSpan) = span.End

    /// Get the end position
    let GetEndPosition (span:SnapshotSpan) = span.End.Position

    /// Get the text of the span
    let GetText (span:SnapshotSpan) = span.GetText()

    /// Get the length of the span
    let GetLength (span:SnapshotSpan) = span.Length

    /// Get the raw Span
    let GetSpan (span:SnapshotSpan) = span.Span

    /// Get the Snapshot
    let GetSnapshot (span:SnapshotSpan) = span.Snapshot

    /// Get all of the points on the specified SnapshotSpan.  Will not return the End point
    let GetPoints (span:SnapshotSpan) = 
        let tss = span.Snapshot 
        let startPos = span.Start.Position
        if span.Length = 0 then Seq.empty 
        else 
            let max = span.Length-1
            seq { for i in 0 .. max do yield SnapshotPoint(tss, startPos+i) }

    /// Get all of the points on the specified SnapshotSpan backwards.  Will not return 
    /// the End point
    let GetPointsBackward (span:SnapshotSpan) =
        let tss = span.Snapshot
        let startPos = span.Start.Position
        let length = span.Length
        seq { for i in 1 .. length do 
                let offset = length - i
                yield SnapshotPoint(tss, startPos + offset) }

    /// Get the first line in the SnapshotSpan
    let GetStartLine (span:SnapshotSpan) = span.Start.GetContainingLine()

    /// Get the end line in the SnapshotSpan.  Remember that End is not a part of the Span
    /// but instead the first point after the Span.  This is important when the Span is 
    /// ITextSnapshotLine.ExtentIncludingLineBreak as it is in Visual Mode
    let GetEndLine (span:SnapshotSpan) = 
        let doNormal() = 
            if span.Length > 0 then span.End.Subtract(1).GetContainingLine()
            else GetStartLine span

        let snapshot = span.Snapshot
        if SnapshotUtil.GetEndPoint snapshot = span.End then 
            let line = span.End.GetContainingLine()
            if line.Length = 0 then line
            else doNormal()
        else doNormal()

    /// Get the start and end line of the SnapshotSpan.  Remember that End is not a part of
    /// the span but instead the first point after the span
    let GetStartAndEndLine span = GetStartLine span,GetEndLine span

    /// Get the number of lines in this SnapshotSpan
    let GetLineCount span = 
        let startLine,endLine = GetStartAndEndLine span
        (endLine.LineNumber - startLine.LineNumber) + 1

    /// Is this a multiline SnapshotSpan
    let IsMultiline span = 
        let startLine,endLine = GetStartAndEndLine span
        startLine.LineNumber < endLine.LineNumber

    /// Gets the last point which is actually included in the span.  This is different than
    /// EndPoint which is the first point after the span
    let GetLastIncludedPoint (span:SnapshotSpan) =
        if span.Length = 0 then None
        else span.End.Subtract(1) |> Some

    /// Extend the SnapshotSpan count lines downwards.  If the count exceeds the end of the
    /// Snapshot it will extend to the end
    let ExtendDown span lineCount = 
        let startLine,endLine = GetStartAndEndLine span
        let endLine = SnapshotUtil.GetLineOrLast span.Snapshot (endLine.LineNumber+lineCount)
        SnapshotSpan(startLine.Start, endLine.End)

    /// Extend the SnapshotSpan count lines downwards.  If the count exceeds the end of the
    /// Snapshot it will extend to the end.  The resulting Span will include the line break
    /// of the last line
    let ExtendDownIncludingLineBreak span lineCount = 
        let span = ExtendDown span lineCount
        let endLine = GetEndLine span
        SnapshotSpan(span.Start, endLine.EndIncludingLineBreak)

    /// Extend the SnapshotSpan to be the full line at both the start and end points
    let ExtendToFullLine span =
        let startLine,endLine = GetStartAndEndLine span
        SnapshotSpan(startLine.Start, endLine.End)

    /// Extend the SnapshotSpan to be the full line at both the start and end points
    let ExtendToFullLineIncludingLineBreak span =
        let startLine,endLine = GetStartAndEndLine span
        SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak)

    /// Reduces the SnapshotSpan to the subspan of the first line
    let ReduceToStartLine span = 
        if IsMultiline span then 
            let line = GetStartLine span
            SnapshotSpan(span.Start, line.EndIncludingLineBreak)
        else span

    /// Reduces the SnapshotSpan to the subspan of the last line
    let ReduceToEndLine span = 
        if IsMultiline span then 
            let line = GetEndLine span
            SnapshotSpan(line.Start, span.End)
        else span

    /// Get the ITextSnapshotLines included in this SnasphotSpan 
    let GetAllLines span = 
        let startLine = GetStartLine span
        let count = GetLineCount span
        SnapshotUtil.GetLines span.Snapshot startLine.LineNumber SearchKind.Forward
        |> Seq.take count

    /// Break the SnapshotSpan into 3 separate parts.  The middle is the ITextSnapshotLine seq
    /// for the full lines in the middle and the two edge SnapshotSpan's
    let GetLinesAndEdges span = 

        // Calculate the lead edge and the remaining span 
        let leadEdge,span = 
            let startLine = GetStartLine span
            if span.Start = SnapshotUtil.GetEndPoint span.Snapshot then 
                // Special case for a 0 length span at the end of a Snapshot.  Just return 
                // None.  Returning points or spans which start at the End point just causes
                // problems as it forces special cases everywhere
                None,span
            elif span.IsEmpty then 
                Some span,span
            elif span.Start = startLine.Start && span.Length >= startLine.LengthIncludingLineBreak then
                None,span
            else 
                let length = min span.Length (startLine.EndIncludingLineBreak.Position - span.Start.Position)
                let lead = SnapshotSpan(span.Start, length)
                Some lead, SnapshotSpan(lead.End, span.End)

        // Calculate the trailing edge and finish off the middle span
        let trailingEdge,span= 
            if not span.IsEmpty then 
                let endPointLine = span.End.GetContainingLine()
                if span.End = endPointLine.Start then None,span
                else Some(SnapshotSpan(endPointLine.Start, span.End)), SnapshotSpan(span.Start, endPointLine.Start)
            else None,span

        let lines = 
            if span.IsEmpty then Seq.empty
            else GetAllLines span

        (leadEdge, lines, trailingEdge)

    /// Is this an empty line.  That does this span represent the Extent or ExtentIncludingLineBreak of an 
    /// ITextSnapshotLine which has 0 length.  Lines with greater than 0 length which contain all blanks
    /// are not included (they are blank lines which is very different)
    let IsEmptyLineSpan span = 
        let line = GetStartLine span
        line.Start = span.Start
        && line.Length = 0 
        && (span.End.Position >= span.End.Position && span.End.Position <= line.EndIncludingLineBreak.Position)

    /// Create an empty span at the given point
    let Create (startPoint:SnapshotPoint) (endPoint:SnapshotPoint) = SnapshotSpan(startPoint,endPoint)

    /// Create an empty span at the given point
    let CreateEmpty point = SnapshotSpan(point, 0)

    /// Create a SnapshotSpan from the given bounds. 
    /// TODO: Delete this
    let CreateFromBounds (startPoint:SnapshotPoint) (endPoint:SnapshotPoint) = SnapshotSpan(startPoint,endPoint)

    /// Create a span from the given point with the specified length
    let CreateWithLength (startPoint:SnapshotPoint) (length:int) = SnapshotSpan(startPoint, length)

    /// Create a span which is just a combination the provided spans.  It will be the 
    /// overarching span
    let CreateCombined seq = 
        let inner state span = 
            match state with
            | None -> Some span
            | Some(state) ->
                let startPos = min (GetStartPosition state) (GetStartPosition span)
                let endPos = max (GetEndPosition state) (GetEndPosition span)
                SnapshotSpan((GetSnapshot state), startPos,endPos) |> Some
        seq |> Seq.fold inner None

    /// Creates a combined span.  In the case the provided enumeration is empty will 
    /// return an empty span for the Snapshot 
    let CreateCombinedOrEmpty snapshot seq = 
        seq
        |> CreateCombined 
        |> OptionUtil.getOrDefault (SnapshotUtil.GetStartPoint snapshot |> CreateEmpty)

    /// Create a span form the given start point to the end of the snapshot
    let CreateFromProvidedStartToEnd (startPoint:SnapshotPoint) =
        let endPoint = SnapshotUtil.GetEndPoint startPoint.Snapshot
        SnapshotSpan(startPoint, endPoint)

    /// Create a span from the start of the snapshot to the given end point
    let CreateFromStartToProvidedEnd (endPoint:SnapshotPoint) = 
        let startPoint = SnapshotPoint(endPoint.Snapshot, 0)
        SnapshotSpan(startPoint,endPoint)

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module NormalizedSnapshotSpanCollectionUtil =

    /// Get the first item 
    let GetFirst (col:NormalizedSnapshotSpanCollection) = col.[0]

    /// Get the first item 
    let GetLast (col:NormalizedSnapshotSpanCollection) = col.[col.Count-1]

    /// Get the inclusive span 
    let GetCombinedSpan col =
        let first = GetFirst col
        let last = GetLast col
        SnapshotSpan(first.Start,last.End) 

    let OfSeq (s:SnapshotSpan seq) = new NormalizedSnapshotSpanCollection(s)

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module VirtualSnapshotSpanUtil = 

    /// Get the span 
    let GetSnapshotSpan (span:VirtualSnapshotSpan) = span.SnapshotSpan

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module SnapshotLineUtil =

    /// Length of the line
    let GetLength (line:ITextSnapshotLine) = line.Length

    /// Get the start point
    let GetStart (line:ITextSnapshotLine) = line.Start

    /// Get the end point
    let GetEnd (line:ITextSnapshotLine) = line.End

    /// Get the end point including the line break
    let GetEndIncludingLineBreak (line:ITextSnapshotLine) = line.EndIncludingLineBreak

    /// Get the line number
    let GetLineNumber (line:ITextSnapshotLine) = line.LineNumber

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

    /// Get the length of the line break
    let GetLineBreakLength (line:ITextSnapshotLine) = line.LengthIncludingLineBreak - line.Length

    /// Get the line break span 
    let GetLineBreakSpan line = 
        let point = GetEnd line
        let length = GetLineBreakLength line
        SnapshotSpan(point,length)

[<RequireQualifiedAccess>]
type PointKind =
    /// Normal valid point within the ITextSnapshot.  Point in question is the argument
    | Normal of SnapshotPoint
    /// End point of a non-zero length buffer.  Data is a tuple of the last valid
    /// point in the Snapshot and the end point
    | EndPoint of SnapshotPoint * SnapshotPoint
    /// This is a zero length buffer.  Point in question is the argument
    | ZeroLength of SnapshotPoint

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module SnapshotPointUtil =

    /// Get the position
    let GetPosition (point:SnapshotPoint) = point.Position
   
    /// Get the ITextSnapshotLine containing the specified SnapshotPoint
    let GetContainingLine (point:SnapshotPoint) = point.GetContainingLine()

    /// Get the ITextSnapshot containing the SnapshotPoint
    let GetSnapshot (point:SnapshotPoint) = point.Snapshot

    /// Get the ITextBuffer containing the SnapshotPoint
    let GetBuffer (point:SnapshotPoint) = point.Snapshot.TextBuffer

    /// Is the passed in SnapshotPoint inside the line break portion of the line
    let IsInsideLineBreak point = 
        let line = GetContainingLine point
        point.Position >= line.End.Position

    /// Is this the start of the containing line?
    let IsStartOfLine point =
        let line = GetContainingLine point
        line.Start.Position = point.Position

    /// Is this the end of the Snapshot
    let IsEndPoint point = 
        let snapshot = GetSnapshot point
        point = SnapshotUtil.GetEndPoint snapshot

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
        let last = SnapshotUtil.GetLineOrLast tss (startLine.LineNumber+(count-1))
        new SnapshotSpan(start, last.End)

    /// Functions exactly line GetLineRangeSpan except it will include the final line up until
    /// the end of the line break
    let GetLineRangeSpanIncludingLineBreak (start:SnapshotPoint) count =
        let tss = start.Snapshot
        let startLine = start.GetContainingLine()
        let last = SnapshotUtil.GetLineOrLast tss (startLine.LineNumber+(count-1))
        new SnapshotSpan(start, last.EndIncludingLineBreak)

    /// Get the line and column information for a given SnapshotPoint
    let GetLineColumn point = 
        let line = GetContainingLine point
        let column = point.Position - line.Start.Position
        (line.LineNumber,column)

    /// Get the column number 
    let GetColumn point = 
        let _,column = GetLineColumn point 
        column
    
    /// Get the lines of the containing ITextSnapshot as a seq 
    let GetLines point kind =
        let tss = GetSnapshot point
        let startLine = point.GetContainingLine().LineNumber
        SnapshotUtil.GetLines tss startLine kind 

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
    let GetPoints point kind =
        let mapFunc = 
            if SearchKindUtil.IsForward kind then SnapshotSpanUtil.GetPoints
            else SnapshotSpanUtil.GetPointsBackward 
        GetSpans point kind 
        |> Seq.map mapFunc
        |> Seq.concat       

    /// Divide the ITextSnapshot into at most 2 SnapshotSpan instances at the provided
    /// SnapshotPoint.  If there is an above span it will be exclusive to the provided
    /// value
    let GetDividedSnapshotSpans point kind = 
        let above = SnapshotSpanUtil.CreateFromStartToProvidedEnd point
        let below = SnapshotSpanUtil.CreateFromProvidedStartToEnd point
        match kind with
        | SearchKind.Forward -> [below]
        | SearchKind.ForwardWithWrap -> [below; above] 
        | SearchKind.Backward -> [above] 
        | SearchKind.BackwardWithWrap -> [above; below] 
        | _ -> failwith ""

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

    /// Get the characeter associated with the point.  Will throw for the End point in the Snapshot
    let GetChar (point:SnapshotPoint) = point.GetChar()

    /// Get the points on the containing line starting at the passed in value.  If the passed in start
    /// point is inside the line break, an empty sequence will be returned
    let GetPointsOnContainingLineFrom startPoint = 
        if IsInsideLineBreak startPoint then Seq.empty
        else 
            let line = GetContainingLine startPoint
            SnapshotSpan(startPoint, line.End) |> SnapshotSpanUtil.GetPoints

    /// Get the points on the containing line start starting at the passed in value in reverse order.  If the
    /// passed in point is inside the line break then the points of the entire line will be returned
    let GetPointsOnContainingLineBackwardsFrom startPoint = 
        let line = GetContainingLine startPoint
        let span = 
            if IsInsideLineBreak startPoint then SnapshotLineUtil.GetExtent line 
            else 
                // Adding 1 is safe here.  The End position is always a valid SnapshotPoint and since we're
                // not in the line break startPoint must be < End and hence startPoint.Add(1) <= End
                SnapshotSpan(line.Start, startPoint.Add(1)) 
        span |> SnapshotSpanUtil.GetPointsBackward

    /// Try and get the previous point on the same line.  If this is at the start of the line 
    /// None will be returned
    let TryGetPreviousPointOnLine point = 
        let line = GetContainingLine point
        let start = line.Start
        if point.Position > start.Position then point.Subtract(1) |> Some
        else None

    /// Get the previous point on the same line.  If this is the start of the line then the
    /// original input will be returned
    let GetPreviousPointOnLine point count = 
        let rec inner point count = 
            if 0 = count then point
            else 
                match TryGetPreviousPointOnLine point with 
                | Some(previous) -> inner previous (count-1)
                | None -> point
        inner point count

    /// Get the span of going "count" previous points on the line with the original "point"
    /// value being the End (exclusive) point on the Span
    let GetPreviousPointOnLineSpan point count = 
        let start = GetPreviousPointOnLine point count
        SnapshotSpan(start, point)

    /// Is this the last point on the line?
    let IsLastPointOnLine point = 
        let line = GetContainingLine point
        if line.Length = 0 then point = line.Start
        else point.Position + 1 = line.End.Position

    /// Try and get the next point on the same line.  If this is the end of the line or if
    /// the point is within the line break then None will be returned
    let TryGetNextPointOnLine point =
        let line = GetContainingLine point
        let endPoint = line.End
        if point.Position + 1 < endPoint.Position then point.Add(1) |> Some
        else None

    /// Get the next point on the same line.  If this is the end of the line or if the 
    /// point is within the line break then the original value will be returned
    let GetNextPointOnLine point count =
        let rec inner point count = 
            if 0 = count then point
            else
                match TryGetNextPointOnLine point with 
                | Some(next) -> inner next (count-1)
                | None -> point
        inner point count

    /// Get the span of going "count" next points on the line with the passed in
    /// point being the Start of the returned Span
    let GetNextPointOnLineSpan point count =
        let line = GetContainingLine point
        if point.Position >= line.End.Position then SnapshotSpan(point,point)
        else
            let endPosition = min line.End.Position (point.Position+count)
            let endPoint = SnapshotPoint(line.Snapshot, endPosition)
            SnapshotSpan(point, endPoint)

    /// Add the given coun to the SnapshotPoint
    let Add count (point:SnapshotPoint) = point.Add(count)

    /// Add 1 to the given SnapshotPoint
    let AddOne (point:SnapshotPoint) = point.Add(1)

    /// Try and add count to the SnapshotPoint.  Will return None if this causes
    /// the point to go past the end of the Snapshot
    let TryAdd point count = 
        let pos = (GetPosition point) + count
        let snapshot = GetSnapshot point
        if pos > snapshot.Length then None
        else point.Add(count) |> Some

    /// Maybe add 1 to the given point.  Will return the original point
    /// if it's the end of the Snapshot
    let TryAddOne point = TryAdd point 1

    /// Subtract the count from the SnapshotPoint
    let SubtractOne (point:SnapshotPoint) =  point.Subtract(1)

    /// Maybe subtract the count from the SnapshotPoint
    let TrySubtractOne (point:SnapshotPoint) =  
        if point.Position = 0 then None
        else point |> SubtractOne |> Some

    /// Used to order two SnapshotPoint's in ascending order.  
    let OrderAscending (left:SnapshotPoint) (right:SnapshotPoint) = 
        if left.Position < right.Position then left,right
        else right,left

    /// Get the PointKind information about this SnapshotPoint
    let GetPointKind point = 
        if IsEndPoint point then 
            match TrySubtractOne point with 
            | Some(lastPoint) -> PointKind.EndPoint(lastPoint,point)
            | None -> PointKind.ZeroLength(point)
        else PointKind.Normal point

module VirtualSnapshotPointUtil =
    
    let OfPoint (point:SnapshotPoint) = VirtualSnapshotPoint(point)

    let GetPoint (point:VirtualSnapshotPoint) = point.Position

    let GetPosition point = 
        let point = GetPoint point
        point.Position

    let GetContainingLine (point:VirtualSnapshotPoint) = SnapshotPointUtil.GetContainingLine point.Position

    let IsInVirtualSpace (point:VirtualSnapshotPoint) = point.IsInVirtualSpace

    /// Incremental the VirtualSnapshotPoint by one keeping it on the same 
    /// line
    let AddOneOnSameLine point =
        if IsInVirtualSpace point then VirtualSnapshotPoint(point.Position, point.VirtualSpaces + 1)
        else
            let line = GetContainingLine point
            if point.Position = line.EndIncludingLineBreak then VirtualSnapshotPoint(point.Position, 1)
            else VirtualSnapshotPoint(point.Position.Add(1))

    /// Used to order two SnapshotPoint's in ascending order.  
    let OrderAscending (left:VirtualSnapshotPoint) (right:VirtualSnapshotPoint) = 
        if left.CompareTo(right) < 0 then left,right 
        else right,left

module SnapshotLineSpanUtil = 

    let CreateForSingleLine snapshot lineNumber = 
        SnapshotLineSpan(snapshot, lineNumber, 1)

    let CreateForStartAndCount snapshot lineNumber count = 
        SnapshotLineSpan(snapshot, lineNumber, count)

    let CreateForStartAndEndLine snapshot startLineNumber endLineNumber =
        SnapshotLineSpan(snapshot, startLineNumber, (endLineNumber - startLineNumber) + 1)

    let CreateForSpan span = 
        let startLine,endLine = SnapshotSpanUtil.GetStartAndEndLine span
        CreateForStartAndEndLine span.Snapshot startLine.LineNumber endLine.LineNumber

    let CreateForNormalizedSnapshotSpanCollection col = 
        col |> NormalizedSnapshotSpanCollectionUtil.GetCombinedSpan |> CreateForSpan

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module TextViewUtil =

    let GetSnapshot (textView:ITextView) = textView.TextSnapshot

    let GetCaret (textView:ITextView) = textView.Caret

    let GetCaretPoint (textView:ITextView) = textView.Caret.Position.BufferPosition

    let GetCaretPointKind textView = textView |> GetCaretPoint |> SnapshotPointUtil.GetPointKind

    let GetCaretLine textView = GetCaretPoint textView |> SnapshotPointUtil.GetContainingLine

    let GetCaretLineSpan textView count = 
        let lineNumber,_ = textView |> GetCaretPoint |> SnapshotPointUtil.GetLineColumn
        SnapshotLineSpanUtil.CreateForStartAndCount textView.TextSnapshot lineNumber count

    let GetCaretPointAndLine textView = (GetCaretPoint textView),(GetCaretLine textView)

    /// Returns a sequence of ITextSnapshotLines representing the visible lines in the buffer
    let GetVisibleSnapshotLines (textView:ITextView) =
        if textView.InLayout then Seq.empty
        else 
            let lines = textView.TextViewLines
            let startNumber = lines.FirstVisibleLine.Start.GetContainingLine().LineNumber
            let endNumber = lines.LastVisibleLine.End.GetContainingLine().LineNumber
            let snapshot = textView.TextSnapshot
            seq {
                for i = startNumber to endNumber do
                    yield SnapshotUtil.GetLine snapshot i
            }

    /// Ensure the caret is currently on the visible screen
    let EnsureCaretOnScreen textView = 
        let caret = GetCaret textView
        caret.EnsureVisible()

    /// Ensure the text pointed to by the caret is currently expanded
    let EnsureCaretTextExpanded textView (outliningManager:IOutliningManager) = 
        let point = GetCaretPoint textView
        outliningManager.ExpandAll(SnapshotSpan(point,0), fun _ -> true) |> ignore

    /// Ensure that the caret is both on screen and not in the middle of any outlining region
    let EnsureCaretOnScreenAndTextExpanded textView outliningManager =
        EnsureCaretOnScreen textView
        EnsureCaretTextExpanded textView outliningManager

    /// Move the caret to the given point and ensure it is on screen.  Will not expand any outlining regions
    let MoveCaretToPoint textView (point:SnapshotPoint) = 
        let caret = GetCaret textView
        caret.MoveTo(point) |> ignore
        EnsureCaretOnScreen textView 

    /// Move the caret to the given point and ensure it is on screen.  Will not expand any outlining regions
    let MoveCaretToVirtualPoint textView (point:VirtualSnapshotPoint) = 
        let caret = GetCaret textView
        caret.MoveTo(point) |> ignore
        EnsureCaretOnScreen textView 

    /// Move the caret to the given position and ensure it is on screen.  Will not expand any outlining regions
    let MoveCaretToPosition textView (pos:int) = 
        let tss = GetSnapshot textView
        let point = SnapshotPoint(tss, pos)
        MoveCaretToPoint textView point 

module TextSelectionUtil = 

    /// Returns the SnapshotSpan which represents the total of the selection.  This is a SnapshotSpan of the left
    /// most and right most point point in any of the selected spans 
    /// TODO: Delete this
    let GetOverarchingSelectedSpan (selection:ITextSelection) = 
        if selection.IsEmpty || 0 = selection.SelectedSpans.Count then None
        else
            let spans = selection.SelectedSpans
            let min = spans |> Seq.map SnapshotSpanUtil.GetStartPosition |> Seq.min
            let max = spans |> Seq.map SnapshotSpanUtil.GetEndPosition |> Seq.max
            let span = Span.FromBounds(min,max)
            let snapshot = spans.Item(0).Snapshot
            SnapshotSpan(snapshot, span) |> Some

    /// Gets the selection of the editor
    let GetStreamSelectionSpan (selection:ITextSelection) = selection.StreamSelectionSpan

module EditorOptionsUtil =

    /// Get the option value if it exists
    let GetOptionValue (opts:IEditorOptions) (key:EditorOptionKey<'a>) =
        try
            if opts.IsOptionDefined(key, false) then opts.GetOptionValue(key) |> Some
            else None
        with
            | :? System.ArgumentException-> None
            | :? System.InvalidOperationException -> None

    let GetOptionValueOrDefault opts key defaultValue = 
        match GetOptionValue opts key with
        | Some(value) -> value
        | None -> defaultValue

module TrackingPointUtil =
    
    let GetPoint (snapshot:ITextSnapshot) (point:ITrackingPoint) =
        try
            point.GetPoint(snapshot) |> Some
        with
            | :? System.ArgumentException -> None

module TrackingSpanUtil =

    let GetSpan (snapshot:ITextSnapshot) (span:ITrackingSpan) =
        try 
            span.GetSpan(snapshot) |> Some
        with
            | :? System.ArgumentException -> None


