#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module internal SnapshotUtil = 
    
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

    /// Is the Line Number valid
    let IsLineNumberValid (tss:ITextSnapshot) lineNumber = lineNumber >= 0 && lineNumber < tss.LineCount

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
    
/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module internal SnapshotSpanUtil =

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

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module internal SnapshotLineUtil =

    /// Length of the line
    let GetLength (line:ITextSnapshotLine) = line.Length

    /// Get the start point
    let GetStart (line:ITextSnapshotLine) = line.Start

    /// Get the end point
    let GetEnd (line:ITextSnapshotLine) = line.End

    /// Get the end point including the line break
    let GetEndIncludingLineBreak (line:ITextSnapshotLine) = line.EndIncludingLineBreak

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

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module internal TextViewUtil =

    let GetSnapshot (textView:ITextView) = textView.TextSnapshot

    let GetCaret (textView:ITextView) = textView.Caret

    let GetCaretPoint (textView:ITextView) = textView.Caret.Position.BufferPosition

    let GetCaretLine textView = GetCaretPoint textView |> SnapshotPointUtil.GetContainingLine

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

