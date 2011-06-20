#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Projection
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining

/// Represents a range of lines in an ITextSnapshot.  Different from a SnapshotSpan
/// because it declaratively supports lines instead of a position range
type SnapshotLineRange 
    (   _snapshot : ITextSnapshot,
        _startLine : int,
        _count : int ) =

    do
        if _startLine >= _snapshot.LineCount then
            invalidArg "startLine" Resources.Common_InvalidLineNumber
        if _startLine + (_count - 1) >= _snapshot.LineCount || _count < 1 then
            invalidArg "count" Resources.Common_InvalidLineNumber

    member x.Snapshot = _snapshot
    member x.StartLineNumber = _startLine
    member x.StartLine = _snapshot.GetLineFromLineNumber x.StartLineNumber
    member x.Start = x.StartLine.Start
    member x.Count = _count
    member x.EndLineNumber = _startLine + (_count - 1)
    member x.EndLine = _snapshot.GetLineFromLineNumber x.EndLineNumber
    member x.End = x.EndLine.End
    member x.EndIncludingLineBreak = x.EndLine.EndIncludingLineBreak
    member x.Extent=
        let startLine = x.StartLine
        let endLine = x.EndLine
        SnapshotSpan(startLine.Start, endLine.End)
    member x.ExtentIncludingLineBreak = 
        let startLine = x.StartLine
        let endLine = x.EndLine
        SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak)
    member x.Lines = seq { for i in _startLine .. x.EndLineNumber do yield _snapshot.GetLineFromLineNumber(i) }
    member x.GetText() = x.Extent.GetText()
    member x.GetTextIncludingLineBreak() = x.ExtentIncludingLineBreak.GetText()

    // Equality Functions
    override x.GetHashCode() = _startLine ^^^ _count
    override x.Equals (other:obj) = 
        match other with
        | :? SnapshotLineRange as other -> x.Equals(other)
        | _ -> false
    member x.Equals (other:SnapshotLineRange) = 
        other.Snapshot = _snapshot
        && other.StartLineNumber = _startLine
        && other.Count = _count
    interface System.IEquatable<SnapshotLineRange> with
        member x.Equals other = x.Equals other
    static member op_Equality(this,other) = 
        System.Collections.Generic.EqualityComparer<SnapshotLineRange>.Default.Equals(this,other)
    static member op_Inequality(this,other) = 
        not (System.Collections.Generic.EqualityComparer<SnapshotLineRange>.Default.Equals(this,other))

    // Overrides
    override x.ToString() = sprintf "%d - %d : %s" _startLine x.EndLineNumber (x.Extent.ToString()) 

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
    let GetExtent snapshot = 
        let startPoint = GetStartPoint snapshot
        let endPoint = GetEndPoint snapshot
        SnapshotSpan(startPoint, endPoint)

    /// Get the text of the ITextSnapshot
    let GetText (snapshot:ITextSnapshot) = snapshot.GetText()

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

    /// Get the lines in the buffer with the specified direction.  The specified line number
    /// will be included in the returned sequence unless it's an invalid line
    let GetLines snapshot lineNumber path =
        match path, IsLineNumberValid snapshot lineNumber with
        | Path.Forward, true ->
            let endLineNumber = GetLastLineNumber snapshot
            seq {
                for i = lineNumber to endLineNumber do
                    yield GetLine snapshot i
            }
        | Path.Backward, true ->
            seq {
                for i = 0 to lineNumber do
                    let number = lineNumber - i
                    yield GetLine snapshot number
            }
        | Path.Forward, false -> 
            Seq.empty
        | Path.Backward, false ->
            Seq.empty

    /// Get the lines in the specified range
    let GetLineRange snapshot startLineNumber endLineNumber = 
        let count = endLineNumber - startLineNumber + 1
        GetLines snapshot startLineNumber Path.Forward
        |> Seq.truncate count

    /// Try and get the line at the specified number
    let TryGetLine snapshot number = 
        if IsLineNumberValid snapshot number then
            GetLine snapshot number |> Some
        else
            None

    /// Get the point from the specified position
    let GetPoint (snapshot : ITextSnapshot) position = SnapshotPoint(snapshot, position)

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

    /// Get all of the SnapshotPoint values in the Span.  This will not return the End point
    /// but will return line breaks
    let GetPoints path span =
        let startPoint = GetStartPoint span
        let endPoint = GetEndPoint span
        let positions =
            let offset = startPoint.Position
            match path with 
            | Path.Forward ->
                let max = span.Length - 1
                seq {
                    for i = 0 to max do
                        yield i + offset
                }
            | Path.Backward ->
                let length = span.Length
                seq {
                    for i = 1 to length do
                        yield offset + (length - i)
                }
        positions |> Seq.map (fun p -> SnapshotUtil.GetPoint span.Snapshot p)

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

    /// Is this the last included point in the SnapshotSpan?  
    let IsLastIncludedPoint span point = 
        match GetLastIncludedPoint span with 
        | None -> false
        | Some(p) -> p = point 

    /// Gets the last line which is apart of this Span.  
    let GetLastIncludedLine span = 
        let point = GetLastIncludedPoint span
        match point with
        | Some(point) -> point.GetContainingLine() |> Some
        | None -> None

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
        SnapshotUtil.GetLines span.Snapshot startLine.LineNumber Path.Forward
        |> Seq.take count

    /// Break the SnapshotSpan into 3 separate parts.  The middle is the ITextSnapshotLine seq
    /// for the full lines in the middle and the two edge SnapshotSpan's
    let GetLinesAndEdges span = 

        // Calculate the lead edge and the remaining span 
        let leadEdge, span = 
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
        let trailingEdge, span = 
            if not span.IsEmpty then 
                let endPointLine = span.End.GetContainingLine()
                if span.End = endPointLine.Start then None,span
                else Some(SnapshotSpan(endPointLine.Start, span.End)), SnapshotSpan(span.Start, endPointLine.Start)
            else None,span

        let lines = 
            if span.IsEmpty then 
                None
            else 
                let startLine = span.Start.GetContainingLine()
                SnapshotLineRange(span.Snapshot, startLine.LineNumber, GetLineCount span) |> Some

        (leadEdge, lines, trailingEdge)

    /// Is this an empty line.  That does this span represent the Extent or ExtentIncludingLineBreak of an 
    /// ITextSnapshotLine which has 0 length.  Lines with greater than 0 length which contain all blanks
    /// are not included (they are blank lines which is very different)
    let IsEmptyLineSpan span = 
        let line = GetStartLine span
        line.Start = span.Start
        && line.Length = 0 
        && (span.End.Position >= span.End.Position && span.End.Position <= line.EndIncludingLineBreak.Position)

    /// Given a NonEmptyCollection<SnapshotSpan> return the SnapshotSpan which is the overarching span that
    /// encompasses all of the SnapshotSpan values in the collection.  The Start will be the minimum start of 
    /// all of the SnapshotSpan values and the End will be the maximum
    let GetOverarchingSpan (col : NonEmptyCollection<SnapshotSpan>) =
        let startPoint = col |> Seq.map (fun span -> span.Start) |> Seq.minBy (fun p -> p.Position)
        let endPoint = col |> Seq.map (fun span -> span.End) |> Seq.maxBy (fun p -> p.Position)
        SnapshotSpan(startPoint, endPoint)

    /// Create an empty span at the given point
    let Create (startPoint:SnapshotPoint) (endPoint:SnapshotPoint) = SnapshotSpan(startPoint,endPoint)

    /// Create an empty span at the given point
    let CreateEmpty point = SnapshotSpan(point, 0)

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

    /// Get the first item 
    let TryGetFirst (col : NormalizedSnapshotSpanCollection) = if col.Count = 0 then None else Some (col.[0])

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
    let GetPoints path line = line |> GetExtent |> SnapshotSpanUtil.GetPoints path

    /// Get the points on the particular line including the line break
    let GetPointsIncludingLineBreak path line = line |> GetExtentIncludingLineBreak |> SnapshotSpanUtil.GetPoints path

    /// Get the length of the line break
    let GetLineBreakLength (line:ITextSnapshotLine) = line.LengthIncludingLineBreak - line.Length

    /// Get the line break span 
    let GetLineBreakSpan line = 
        let point = GetEnd line
        let length = GetLineBreakLength line
        SnapshotSpan(point,length)

    /// Get the indent point of the ITextSnapshotLine
    let GetIndent line =
        line 
        |> GetPoints Path.Forward
        |> Seq.skipWhile (fun point -> point.GetChar() |> CharUtil.IsWhiteSpace)
        |> SeqUtil.tryHeadOnly
        |> OptionUtil.getOrDefault (GetEnd line)

    /// Get the text of the ITextSnapshotLine 
    let GetText (line : ITextSnapshotLine) = line.GetText()

    /// Get the text of the ITextSnapshotLine including the line break
    let GetTextIncludingLineBreak (line : ITextSnapshotLine) = line.GetTextIncludingLineBreak()

    /// Get the last point which is included in this line not conspiring the line 
    /// break.  Can be None if this is a 0 length line 
    let GetLastIncludedPoint line = 
        let span = GetExtent line
        if span.Length = 0 then None
        else span.End.Subtract(1) |> Some

    /// Get a SnapshotPoint representing 'offset' characters into the line or the 
    /// End point of the line
    let GetOffsetOrEnd (line : ITextSnapshotLine) offset = 
        if line.Start.Position + offset >= line.End.Position then line.End
        else line.Start.Add(offset)

    /// Is this the last included point on the ITextSnapshotLine
    let IsLastPoint (line : ITextSnapshotLine) point = 
        if line.Length = 0 then point = line.Start
        else point.Position + 1 = line.End.Position

    /// Is this the last point on the line including the line break
    let IsLastPointIncludingLineBreak (line : ITextSnapshotLine) (point : SnapshotPoint) = 
        point.Position + 1 = line.EndIncludingLineBreak.Position

    /// Is this the last line in the ITextBuffer
    let IsLastLine (line : ITextSnapshotLine) = 
        let snapshot = line.Snapshot
        snapshot.LineCount - 1 = line.LineNumber

    /// Does the line consist of only whitespace
    let IsBlank line = 
        line
        |> GetExtent
        |> SnapshotSpanUtil.GetPoints Path.Forward
        |> Seq.forall (fun point -> CharUtil.IsBlank (point.GetChar()))

    /// Get the first non-blank character on the line
    let GetFirstNonBlank line = 
        line
        |> GetExtent
        |> SnapshotSpanUtil.GetPoints Path.Forward
        |> Seq.skipWhile (fun point -> CharUtil.IsBlank (point.GetChar()))
        |> SeqUtil.tryHeadOnly

    /// Get the first non-blank character on the line or the Start point if all 
    /// characters are blank
    let GetFirstNonBlankOrStart line = 
        match GetFirstNonBlank line with
        | None -> line.Start
        | Some point -> point

    /// Get the first non-blank character on the line or the End point if all
    /// characters are blank
    let GetFirstNonBlankOrEnd line = 
        match GetFirstNonBlank line with
        | None -> line.End
        | Some point -> point

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

    /// Is this the start of the containing line?
    let IsStartOfLine point =
        let line = GetContainingLine point
        line.Start.Position = point.Position

    /// Is this the start of th eSnapshot
    let IsStartPoint point = 0 = GetPosition point

    /// Is this the end of the Snapshot
    let IsEndPoint point = 
        let snapshot = GetSnapshot point
        point = SnapshotUtil.GetEndPoint snapshot

    /// Is the passed in SnapshotPoint inside the line break portion of the line
    let IsInsideLineBreak point = 
        let line = GetContainingLine point
        point.Position >= line.End.Position && not (IsEndPoint point)

    /// Is this point white space?
    let IsWhiteSpace point =
        if IsEndPoint point then false
        else CharUtil.IsWhiteSpace (point.GetChar())

    /// Is this point a space or tab
    let IsBlank point = 
        if IsEndPoint point then false
        else CharUtil.IsBlank (point.GetChar())

    /// Is this point a blank or the end point of the ITextSnapshot
    let IsBlankOrEnd point = 
        IsBlank point || IsEndPoint point

    /// Is this point not a blank 
    let IsNotBlank point =
        not (IsBlank point)

    /// Is this point white space or inside the line break?
    let IsWhiteSpaceOrInsideLineBreak point = 
        IsWhiteSpace point || IsInsideLineBreak point

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

    /// Add the given count to the SnapshotPoint
    let Add count (point:SnapshotPoint) = point.Add(count)

    /// Add 1 to the given SnapshotPoint
    let AddOne (point:SnapshotPoint) = point.Add(1)

    /// Add 1 to the given snapshot point unless it's the end of the buffer in which case just
    /// return the passed in value
    let AddOneOrCurrent point =
        match TryAddOne point with
        | None -> point
        | Some(point) -> point

    /// Subtract the count from the SnapshotPoint
    let SubtractOne (point:SnapshotPoint) =  point.Subtract(1)

    /// Maybe subtract the count from the SnapshotPoint
    let TrySubtractOne (point:SnapshotPoint) =  
        if point.Position = 0 then None
        else point |> SubtractOne |> Some

    /// Try and subtract 1 from the given point unless it's the start of the buffer in which
    /// case return the passed in value
    let SubtractOneOrCurrent point = 
        match TrySubtractOne point with
        | Some (point) -> point
        | None -> point


    /// Is the SnapshotPoint the provided char
    let IsChar c point =
        if IsEndPoint point then false
        else (point.GetChar()) = c

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

    /// Get the line number
    let GetLineNumber point =
        let line = GetContainingLine point
        line.LineNumber

    /// Get the lines of the containing ITextSnapshot as a seq 
    let GetLines point kind =
        let tss = GetSnapshot point
        let startLine = point.GetContainingLine().LineNumber
        SnapshotUtil.GetLines tss startLine kind 

    /// Start searching the snapshot at the given point and return the buffer as a 
    /// sequence of SnapshotSpans.  One will be returned per line in the buffer.  The
    /// only exception is the start line which will be divided at the given start
    /// point.  Going forward the point will be included but going reverse it will not. 
    /// The returned spans will not include line breaks in the buffer
    let GetSpans path point = 

        let snapshot = GetSnapshot point
        let startLine = GetContainingLine point
        match path with
        | Path.Forward ->

            seq {
                // Return the rest of the start line if we are not in a line break
                if not (IsInsideLineBreak point) && not (IsEndPoint point) then
                    yield SnapshotSpan(point, startLine.End)

                // Return the rest of the line extents
                let lines = 
                    SnapshotUtil.GetLines snapshot startLine.LineNumber Path.Forward
                    |> SeqUtil.skipMax 1
                    |> Seq.map SnapshotLineUtil.GetExtent
                yield! lines
            }

        | Path.Backward ->
            seq {
                // Return the beginning of the start line if this is not the start
                if point <> startLine.Start then
                    yield SnapshotSpan(startLine.Start, point)

                // Return the rest of the line extents
                let lines =
                    SnapshotUtil.GetLines snapshot startLine.LineNumber Path.Backward
                    |> SeqUtil.skipMax 1
                    |> Seq.map SnapshotLineUtil.GetExtent
                yield! lines
            }

    /// Get all of the SnapshotPoint values on the given path.  The first value returned
    /// will be the passed in SnapshotPoint unless it's <end>
    let GetPointsIncludingLineBreak path point =
        let span = 
            let snapshot = GetSnapshot point
            match path with
            | Path.Forward -> SnapshotSpan(point, SnapshotUtil.GetEndPoint snapshot)
            | Path.Backward -> SnapshotSpan(SnapshotUtil.GetStartPoint snapshot, AddOneOrCurrent point)
        SnapshotSpanUtil.GetPoints path span

    /// Start searching the snapshot at the given point and return the buffer as a 
    /// sequence of SnapshotPoints.  The first point returned will be the point passed
    /// in.
    ///
    /// Note: This will not return SnapshotPoint values for points in the line break
    let GetPoints path point =
        GetPointsIncludingLineBreak path point
        |> Seq.filter (fun point -> not (IsInsideLineBreak point))

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

    /// Get the character associated with the current point.  Returns None for the last character
    /// in the buffer which has no represent able value
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

    /// Get the character associated with the point.  Will throw for the End point in the Snapshot
    let GetChar (point:SnapshotPoint) = point.GetChar()

    /// Get the points on the containing line starting at the passed in value.  If the passed in start
    /// point is inside the line break, an empty sequence will be returned
    let GetPointsOnContainingLineFrom startPoint = 
        if IsInsideLineBreak startPoint then Seq.empty
        else 
            let line = GetContainingLine startPoint
            SnapshotSpan(startPoint, line.End) |> SnapshotSpanUtil.GetPoints Path.Forward

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
        span |> SnapshotSpanUtil.GetPoints Path.Backward

    /// Try and get the previous point on the same line.  If this is at the start of the line 
    /// None will be returned
    let TryGetPreviousPointOnLine point count = 
        let line = GetContainingLine point
        let position = point.Position - count
        if position >= line.Start.Position then
            SnapshotPoint(point.Snapshot, position) |> Some
        else
            None

    /// Try and get the next point on the same line.  If this is the end of the line or if
    /// the point is within the line break then None will be returned
    let TryGetNextPointOnLine point count =
        let line = GetContainingLine point
        let position = point.Position + count
        if position < line.End.Position then
            SnapshotPoint(point.Snapshot, position) |> Some
        else
            None

    /// Is this the last point on the line?
    let IsLastPointOnLine point = 
        let line = GetContainingLine point
        if line.Length = 0 then point = line.Start
        else point.Position + 1 = line.End.Position

    /// Is this the last point on the line including the line break
    let IsLastPointOnLineIncludingLineBreak point = 
        let line = GetContainingLine point
        point.Position + 1 = line.EndIncludingLineBreak.Position

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

/// Contains operations to make it easier to use SnapshotLineRange from a type inference
/// context
module SnapshotLineRangeUtil = 

    /// Create a range for the entire ItextSnapshot
    let CreateForSnapshot (snapshot:ITextSnapshot) = 
        SnapshotLineRange(snapshot, 0, snapshot.LineCount)

    /// Create a range for the provided ITextSnapshotLine
    let CreateForLine (line:ITextSnapshotLine) =
        SnapshotLineRange(line.Snapshot, line.LineNumber, 1)

    /// Create a range for the provided ITextSnapshotLine and with count length
    let CreateForLineAndCount (line:ITextSnapshotLine) count = 
        let snapshot = line.Snapshot
        if count < 0 || line.LineNumber + (count-1) >= snapshot.LineCount then None
        else SnapshotLineRange(snapshot, line.LineNumber, count) |> Some

    /// Create a range for the provided ITextSnapshotLine and with at most count 
    /// length.  If count pushes the range past the end of the buffer then the 
    /// span will go to the end of the buffer
    let CreateForLineAndMaxCount (line:ITextSnapshotLine) count = 
        let maxCount = (line.Snapshot.LineCount - line.LineNumber)
        let count = min count maxCount
        SnapshotLineRange(line.Snapshot, line.LineNumber, count)

    /// Create a line range which covers the start and end line of the provided span
    let CreateForSpan span = 
        let startLine,endLine = SnapshotSpanUtil.GetStartAndEndLine span
        let count = (endLine.LineNumber - startLine.LineNumber) + 1
        SnapshotLineRange(span.Snapshot, startLine.LineNumber, count)

    /// Create a line range for the combined span 
    let CreateForNormalizedSnapshotSpanCollection col = 
        col |> NormalizedSnapshotSpanCollectionUtil.GetCombinedSpan |> CreateForSpan

    /// Create a line range for the start line and extending count total lines
    let CreateForLineNumberAndCount (snapshot:ITextSnapshot) lineNumber count = 
        if count < 0 || lineNumber + (count-1) >= snapshot.LineCount then None
        else SnapshotLineRange(snapshot, lineNumber, count) |> Some

    /// Create a line range for the start line and extending at most conut total lines.  If
    /// the max extends past the end of the buffer it will return to the end
    let CreateForLineNumberAndMaxCount (snapshot:ITextSnapshot) lineNumber count = 
        let line = snapshot.GetLineFromLineNumber(lineNumber)
        CreateForLineAndMaxCount line count

    /// Create a line range for the provided start and end line 
    let CreateForLineRange (startLine:ITextSnapshotLine) (endLine:ITextSnapshotLine) = 
        let count = (endLine.LineNumber - startLine.LineNumber) + 1
        SnapshotLineRange(startLine.Snapshot, startLine.LineNumber, count)

    /// Create a line range for the provided start and end line 
    let CreateForLineNumberRange (snapshot:ITextSnapshot) startNumber endNumber = 
        let startLine = snapshot.GetLineFromLineNumber(startNumber)
        let endLine = snapshot.GetLineFromLineNumber(endNumber)
        CreateForLineRange startLine endLine

module BufferGraphUtil = 

    /// Map the point up to the given ITextSnapshot.  Returns None if the mapping is not 
    /// possible
    let MapPointUpToSnapshot (bufferGraph : IBufferGraph) point trackingMode affinity snapshot =
        try
            bufferGraph.MapUpToSnapshot(point, trackingMode, affinity, snapshot)
            |> OptionUtil.ofNullable
        with
            | :? System.ArgumentException-> None
            | :? System.InvalidOperationException -> None

    /// Map the point down to the given ITextSnapshot.  Returns None if the mapping is not 
    /// possible
    let MapPointDownToSnapshot (bufferGraph : IBufferGraph) point trackingMode affinity snapshot =
        try
            bufferGraph.MapDownToSnapshot(point, trackingMode, affinity, snapshot)
            |> OptionUtil.ofNullable
        with
            | :? System.ArgumentException-> None
            | :? System.InvalidOperationException -> None

    /// Map the SnapshotSpan down to the given ITextSnapshot.  Returns None if the mapping is
    /// not possible
    let MapSpanDownToSnapshot (bufferGraph : IBufferGraph) span trackingMode snapshot =
        try
            bufferGraph.MapDownToSnapshot(span, trackingMode, snapshot) |> Some
        with
            | :? System.ArgumentException-> None
            | :? System.InvalidOperationException -> None

/// The common pieces of information about an ITextSnapshot which are used
/// to calculate items like motions
type SnapshotData = {

    /// SnapshotPoint for the Caret
    CaretPoint : SnapshotPoint

    /// ITextSnapshotLine on which the caret resides
    CaretLine : ITextSnapshotLine

    /// The current ITextSnapshot on which this data is based
    CurrentSnapshot : ITextSnapshot
}

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module TextViewUtil =

    let GetSnapshot (textView:ITextView) = textView.TextSnapshot

    let GetCaret (textView:ITextView) = textView.Caret

    let GetCaretPoint (textView:ITextView) = textView.Caret.Position.BufferPosition

    let GetCaretVirtualPoint (textView:ITextView) = textView.Caret.Position.VirtualBufferPosition

    let GetCaretPointKind textView = textView |> GetCaretPoint |> SnapshotPointUtil.GetPointKind

    let GetCaretLine textView = GetCaretPoint textView |> SnapshotPointUtil.GetContainingLine

    let GetCaretLineIndent textView = textView |> GetCaretLine |> SnapshotLineUtil.GetIndent

    let GetCaretLineRange textView count = 
        let line = GetCaretLine textView
        SnapshotLineRangeUtil.CreateForLineAndMaxCount line count

    let GetCaretPointAndLine textView = (GetCaretPoint textView),(GetCaretLine textView)

    /// Get the count of Visible lines in the ITextView
    let GetVisibleLineCount (textView : ITextView) = 
        try
            textView.TextViewLines.Count
        with 
            // TextViewLines can throw if the view is being laid out.  Highly unlikely we'd hit
            // that inside of Vim but need to be careful
            | _ -> 50

    /// Returns a sequence of ITextSnapshotLine values representing the visible lines in the buffer
    let GetVisibleSnapshotLines (textView : ITextView) =
        if textView.InLayout then
            Seq.empty
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

    /// Get the SnapshotData value for the edit buffer.  Unlike the SnapshotData for the Visual Buffer this 
    /// can always be retrieved because the caret point is presented in terms of the edit buffer
    let GetEditSnapshotData (textView : ITextView) = 
        let caretPoint = GetCaretPoint textView
        let caretLine = SnapshotPointUtil.GetContainingLine caretPoint
        { 
            CaretPoint = caretPoint
            CaretLine = caretLine
            CurrentSnapshot = caretLine.Snapshot }

    /// Get the SnapshotData value for the visual buffer.  Can return None if the information is not mappable
    /// to the visual buffer.  Really this shouldn't ever happen unless the IProjectionBuffer was incorrectly
    /// hooked up though
    let GetVisualSnapshotData (textView : ITextView) = 

        // Get the visual buffer information
        let visualBuffer = textView.TextViewModel.VisualBuffer
        let visualSnapshot = visualBuffer.CurrentSnapshot

        // Map the caret up to the visual buffer from the edit buffer.  The visual buffer will be
        // above the edit buffer.
        //
        // The choice of PointTrackingMode and PositionAffinity is quite arbitrary here and it's very
        // possible there is a better choice for these values.  Since we are going up to a single root
        // ITextBuffer in this case though these shouldn't matter too much
        let caretPoint = 
            let bufferGraph = textView.BufferGraph
            let editCaretPoint = GetCaretPoint textView
            BufferGraphUtil.MapPointUpToSnapshot bufferGraph editCaretPoint PointTrackingMode.Negative PositionAffinity.Predecessor visualSnapshot

        match caretPoint with
        | None ->
            // If the caret can't be mapped up to the visual buffer then there is no way to get the 
            // visual SnapshotData information.  This should represent a rather serious issue with the 
            // ITextView though
            None
        | Some caretPoint ->
            let caretLine = SnapshotPointUtil.GetContainingLine caretPoint
            { 
                CaretPoint = caretPoint
                CaretLine = caretLine
                CurrentSnapshot = caretLine.Snapshot } |> Some

    /// Get the SnapshotData for the visual buffer if available.  If it's not available then fall back
    /// to the edit buffer
    let GetVisualSnapshotDataOrEdit textView = 
        match GetVisualSnapshotData textView with
        | Some snapshotData -> snapshotData
        | None -> GetEditSnapshotData textView

module TextSelectionUtil = 

    /// Returns the SnapshotSpan which represents the total of the selection.  This is a SnapshotSpan of the left
    /// most and right most point point in any of the selected spans 
    let GetOverarchingSelectedSpan (selection : ITextSelection) = 
        if selection.IsEmpty then 
            None
        else
            match NonEmptyCollectionUtil.OfSeq selection.SelectedSpans with
            | None -> None
            | Some col -> SnapshotSpanUtil.GetOverarchingSpan col |> Some

    /// Gets the selection of the editor
    let GetStreamSelectionSpan (selection:ITextSelection) = selection.StreamSelectionSpan

module EditorOptionsUtil =

    /// Get the option value if it exists
    let GetOptionValue (opts : IEditorOptions) (key : EditorOptionKey<'a>) =
        try
            if opts.IsOptionDefined(key, false) then 
                opts.GetOptionValue(key) |> Some
            else 
                None
        with
            | :? System.ArgumentException-> None
            | :? System.InvalidOperationException -> None

    let GetOptionValueOrDefault opts key defaultValue = 
        match GetOptionValue opts key with
        | Some value -> value
        | None -> defaultValue

    let SetOptionValue (opts : IEditorOptions) (key : EditorOptionKey<'a>) value =
        opts.SetOptionValue(key, value)

module TrackingPointUtil =
    
    let GetPoint (snapshot:ITextSnapshot) (point:ITrackingPoint) =
        try
            point.GetPoint(snapshot) |> Some
        with
            | :? System.ArgumentException -> None

    let GetPointInSnapshot point mode newSnapshot =
        let oldSnapshot = SnapshotPointUtil.GetSnapshot point
        let trackingPoint = oldSnapshot.CreateTrackingPoint(point.Position, mode)
        GetPoint newSnapshot trackingPoint

module TrackingSpanUtil =

    let GetSpan (snapshot:ITextSnapshot) (span:ITrackingSpan) =
        try 
            span.GetSpan(snapshot) |> Some
        with
            | :? System.ArgumentException -> None

/// Abstraction useful for APIs which need to work over a single SnapshotSpan 
/// or collection of SnapshotSpan values
[<RequireQualifiedAccess>]
type EditSpan = 
    /// Common case of an edit operation which occurs over a single SnapshotSpan
    | Single of SnapshotSpan 

    /// Occurs during block edits
    | Block of NonEmptyCollection<SnapshotSpan>

    with

    /// View the data as a collection.  For Single values this just creates a
    /// collection with a single element
    member x.Spans =
        match x with
        | Single span -> NonEmptyCollection(span, List.empty) 
        | Block col -> col

    /// Returns the overarching span of the entire EditSpan value.  For Single values
    /// this is a 1-1 mapping.  For Block values it will take the min start position
    /// and combine it with the maximum end position
    member x.OverarchingSpan =
        match x with 
        | Single span -> span
        | Block col -> SnapshotSpanUtil.GetOverarchingSpan col

    /// Provide an implicit conversion from SnapshotSpan.  Useful from C# code
    static member op_Implicit span = EditSpan.Single span

    /// Provide an implicit conversion from NormalizedSnapshotSpan.  Useful from C# code
    static member op_Implicit block = EditSpan.Block block

module EditUtil = 

    /// NewLine to use for the ITextBuffer
    let NewLine (options : IEditorOptions) = DefaultOptionExtensions.GetNewLineCharacter options

    /// Get the length of the line break at the given index 
    let GetLineBreakLength (str : string) index =
        match str.Chars(index) with
        | '\r' ->
            if index + 1 < str.Length && '\n' = str.Chars(index + 1) then
                2
            else
                1
        | '\n' ->
            1
        | '\u2028' ->
            1
        | '\u2029' ->
            1
        | c ->
            if c = char 0x85 then 1
            else 0

    /// Get the length of the line break at the end of the string
    let GetLineBreakLengthAtEnd (str : string) =
        if System.String.IsNullOrEmpty str then 
            0
        else
            let index = str.Length - 1
            if str.Length > 1 && str.Chars(index - 1) = '\r' && str.Chars(index) = '\n' then
                2
            else
                GetLineBreakLength str index

    /// Does the specified string end with a valid newline string 
    let EndsWithNewLine value = 0 <> GetLineBreakLengthAtEnd value

    /// Does this text have a new line character inside of it?
    let HasNewLine (text : string) = 
        { 0 .. (text.Length - 1) }
        |> SeqUtil.any (fun index -> GetLineBreakLength text index > 0)

    /// Remove the NewLine at the beginning of the string.  Returns the original input
    /// if no newline is found
    let RemoveBeginingNewLine value = 
        if System.String.IsNullOrEmpty value then
            value
        else
            let length = GetLineBreakLength value 0
            if 0 = length then
                value
            else
                value.Substring(length)

    /// Remove the NewLine at the end of the string.  Returns the original input
    /// if no newline is found
    let RemoveEndingNewLine value = 
        if System.String.IsNullOrEmpty value then
            value
        else
            let length = GetLineBreakLengthAtEnd value
            if 0 = length then
                value
            else
                value.Substring(0, value.Length - length)

/// In some cases we need to break a complete string into a series of text representations
/// and new lines.  It's easiest to view this as a sequence of text values with their 
/// associated line breaks
type TextLine = {

    /// The text of the line
    Text : string

    /// The string for the new line 
    NewLine : string

} with

    member x.HasNewLine = x.NewLine.Length = 0

    /// Create a string back from the provided TextLine values
    static member CreateString (textLines : TextLine seq) = 
        let builder = System.Text.StringBuilder()
        for textLine in textLines do
            builder.Append(textLine.Text) |> ignore
            builder.Append(textLine.NewLine) |> ignore
        builder.ToString()

    /// Break a string representation into a series of TextNode values.  This will 
    /// always return at least a single value for even an empty string so we use 
    /// a NonEmptyCollection
    static member GetTextLines (fullText : string) = 

        // Get the next new line item from the given index
        let rec getNextNewLine index = 
            if index >= fullText.Length then
                None
            else
                let length = EditUtil.GetLineBreakLength fullText index
                if length = 0 then
                    getNextNewLine (index + 1)
                else
                    Some (index, length)

        // Get the TextLine and next index value for the provided index 
        let getForIndex index =
            match getNextNewLine index with 
            | None -> 
                if index >= fullText.Length then
                    None
                else
                    // There is no more data in the string yet the index is still
                    let textLine = { Text = fullText.Substring(index); NewLine = "" }
                    Some (textLine, fullText.Length + 1)
            | Some (newLineIndex, length) ->
                let text = fullText.Substring(index, (newLineIndex - index))
                let newLine = fullText.Substring(newLineIndex, length)
                let textLine = { Text = text; NewLine = newLine }
                Some (textLine, newLineIndex + length)

        if System.String.IsNullOrEmpty fullText then
            // Corner case.  When provided an empty string just return back an
            // empty TextLine value
            let head = { Text = ""; NewLine = "" }
            NonEmptyCollection(head, [])
        else

            // Calculate the first entry here.  The 'getForIndex' function will return at
            // valid node since we are not dealing with an empty string
            let firstLine, index = getForIndex 0 |> Option.get
    
            // Now calculate the rest 
            let rest : TextLine list = Seq.unfold getForIndex index |> List.ofSeq

            NonEmptyCollection(firstLine, rest)
