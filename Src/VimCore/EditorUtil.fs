#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Projection
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Utilities
open System
open System.Collections.Generic
open System.Diagnostics
open System.Text
open StringBuilderExtensions
open System.Linq

/// This module exists purely to break type dependency issues created below.  
module internal EditorCoreUtil =

    let IsEndPoint (point: SnapshotPoint) = 
        point.Position = point.Snapshot.Length

    let AddOneOrCurrent (point: SnapshotPoint) =
        if IsEndPoint point then
            point
        else
            point.Add(1)

    let SubtractOneOrCurrent (point: SnapshotPoint) = 
        if point.Position = 0 then
            point
        else
            point.Subtract(1)

    /// Get the span of the character which is pointed to by the point.  Normally this is a 
    /// trivial operation.  The only difficulty is if the point exists at the end of a line
    /// (in which case the span covers the linebreak) or if the character is part of a
    /// surrogate pair.
    let GetCharacterSpan (line: ITextSnapshotLine) (point: SnapshotPoint) = 

        // We require that the point belong to the line.
        Contract.Requires(point.Snapshot = line.Snapshot)
        Contract.Requires(point.Position >= line.Start.Position)
        Contract.Requires(point.Position <= line.EndIncludingLineBreak.Position)

        let snapshot = point.Snapshot
        let endSpan = new SnapshotSpan(line.End, line.EndIncludingLineBreak)
        if endSpan.Contains(point) then
            endSpan
        elif point.Position = snapshot.Length then
            new SnapshotSpan(point, 0)
        else
            let c = point.GetChar()
            let snapshot = point.Snapshot
            if System.Char.IsHighSurrogate(c) then
                let nextPoint = point.Add(1)
                if IsEndPoint nextPoint then
                    new SnapshotSpan(point, 1)
                elif System.Char.IsLowSurrogate(nextPoint.GetChar()) then
                    new SnapshotSpan(point, 2)
                else
                    new SnapshotSpan(point, 1)
            elif System.Char.IsLowSurrogate(c) then
                if point.Position = 0 then
                    new SnapshotSpan(point, 1)
                else
                    let previousPoint = point.Subtract(1)
                    if System.Char.IsHighSurrogate(previousPoint.GetChar()) then
                        new SnapshotSpan(previousPoint, 2)
                    else
                        new SnapshotSpan(point, 1)
            else
                new SnapshotSpan(point, 1)

    let GetCharacterWidth (point: SnapshotPoint) tabStop = 
        if IsEndPoint point then 
            0
        else
            let c = point.GetChar()
            CharUtil.GetCharacterWidth c tabStop

    let IsInsideLineBreak (point: SnapshotPoint) (line: ITextSnapshotLine) = 
        point.Position >= line.End.Position && not (IsEndPoint point)

    /// The snapshot ends with a linebreak if there is more more than one
    /// line and the last line of the snapshot (which doesn't have a linebreak)
    /// is empty.
    let EndsWithLineBreak (snapshot: ITextSnapshot) = 
        let lineNumber = snapshot.LineCount - 1 
        lineNumber > 0 && snapshot.GetLineFromLineNumber(lineNumber).Length = 0

    /// THe normalized line count is one fewer than the snapshot line count
    /// when the snapshot ends in a linebreak
    ///
    /// Example:
    /// Buffer Contents    Snapshot Line Count     Normalized LineCount
    /// ''                 1                       1
    /// 'foo'              1                       1
    /// 'foo\r\n'          2                       1 <- ends with a linebreak
    /// 'foo\r\nbar'       2                       2
    /// 'foo\r\nbar\r\n'   3                       2 <- ends with a linebreak
    let GetNormalizedLineCount (snapshot: ITextSnapshot) =
        if EndsWithLineBreak snapshot then snapshot.LineCount - 1 else snapshot.LineCount

    /// The last normalized line is either:
    /// - The first line of a completely empty snapshot
    /// - The last non-empty line without a linebreak
    /// - The line containing the linebreak at the end of a snapshot
    let GetLastNormalizedLineNumber snapshot =
        (GetNormalizedLineCount snapshot) - 1

module TrackingSpanUtil =

    let Create (span: SnapshotSpan) spanTrackingMode =
        span.Snapshot.CreateTrackingSpan(span.Span, spanTrackingMode)

    let GetSpan (snapshot: ITextSnapshot) (span: ITrackingSpan) =
        try 
            span.GetSpan(snapshot) |> Some
        with
            | :? System.ArgumentException -> None

module PropertyCollectionUtil = 

    let ContainsKey (key: obj)  (propertyCollection: PropertyCollection) = propertyCollection.ContainsProperty(key)

    /// Get the property value for the givne key
    let GetValue<'T> (key: obj) (propertyCollection: PropertyCollection) = 
        try
            let succeeded, value = propertyCollection.TryGetProperty<'T>(key)
            if succeeded then
                Some value
            else
                None
        with 
            // If the value exists but is not convertible to the provided type then
            // an exception will be thrown.  Collapse this into an empty option.  
            // Helps guard against cases where other extensions override our values
            // with ones of unexpected types
            | _ -> None

[<StructuralEquality>]
[<NoComparison>]
[<Struct>]
[<DebuggerDisplay("{ToString()}")>]
type LineRange
    (
        _startLine: int,
        _count: int
    ) = 

    member x.StartLineNumber = _startLine

    member x.LastLineNumber = _startLine + (_count - 1)

    member x.Count = _count

    member x.LineNumbers = Enumerable.Range(_startLine, _count)

    member x.ContainsLineNumber lineNumber = lineNumber >= _startLine && lineNumber <= x.LastLineNumber

    member x.Contains (lineRange: LineRange) = 
        x.StartLineNumber <= lineRange.StartLineNumber &&
        x.LastLineNumber >= lineRange.LastLineNumber

    member x.Intersects (lineRange: LineRange) = 
        x.ContainsLineNumber(lineRange.StartLineNumber) ||
        x.ContainsLineNumber(lineRange.LastLineNumber) ||
        x.LastLineNumber + 1 = lineRange.StartLineNumber ||
        x.StartLineNumber = lineRange.LastLineNumber + 1;

    override x.ToString() = sprintf "[%d - %d]" x.StartLineNumber x.LastLineNumber

    static member op_Equality(this: LineRange, other) = this = other;
    static member op_Inequality(this: LineRange, other) = this <> other;

    static member CreateFromBounds (startLineNumber: int) (lastLineNumber: int) = 
        if (lastLineNumber < startLineNumber) then
            raise (new ArgumentOutOfRangeException("lastLineNumber", "Must be greater than startLineNmuber"))

        let count = (lastLineNumber - startLineNumber) + 1;
        new LineRange(startLineNumber, count)

    static member CreateOverarching (left: LineRange) (right: LineRange) =
        let startLineNumber =  min left.StartLineNumber right.StartLineNumber
        let lastLineNumber = max left.LastLineNumber right.LastLineNumber
        LineRange.CreateFromBounds startLineNumber lastLineNumber

/// Represents a range of lines in an ITextSnapshot.  Different from a SnapshotSpan
/// because it declaratively supports lines instead of a position range
[<StructuralEquality>]
[<NoComparison>]
[<Struct>]
[<DebuggerDisplay("{ToString()}")>]
type SnapshotLineRange  =

    val private _snapshot: ITextSnapshot
    val private _startLine: int
    val private _count: int

    member x.Snapshot = x._snapshot

    member x.StartLineNumber = x._startLine;

    member x.StartLine = x._snapshot.GetLineFromLineNumber(x.StartLineNumber)

    member x.Start = x.StartLine.Start

    member x.Count = x._count

    member x.LastLineNumber = x._startLine + (x._count - 1)

    member x.LastLine = x._snapshot.GetLineFromLineNumber(x.LastLineNumber)

    member x.LineRange = new LineRange(x._startLine, x._count)

    member x.End = x.LastLine.End

    member x.EndIncludingLineBreak = x.LastLine.EndIncludingLineBreak

    member x.Extent = new SnapshotSpan(x.Start, x.End)

    member x.ExtentIncludingLineBreak = new SnapshotSpan(x.Start, x.EndIncludingLineBreak)

    member x.Lines = 
        let snapshot = x._snapshot
        let start = x.StartLineNumber
        let last = x.LastLineNumber
        seq { for i = start to last do yield snapshot.GetLineFromLineNumber(i) }

    new (snapshot: ITextSnapshot, startLine: int, count: int) =
        let lineCount = snapshot.LineCount
        if startLine >= lineCount then
            raise (new ArgumentException("startLine", "Invalid Line Number"))

        if (startLine + (count - 1) >= lineCount || count < 1) then
            raise (new ArgumentException("count", "Invalid Line Number"))

        { _snapshot = snapshot; _startLine = startLine; _count = count; }

    member x.GetText() = x.Extent.GetText()

    member x.GetTextIncludingLineBreak() = x.ExtentIncludingLineBreak.GetText()

    static member op_Equality(left: SnapshotLineRange, right) = left = right

    static member op_Inequality(left: SnapshotLineRange, right) = left <> right

    override x.ToString() = sprintf "[%d - %d] %O" x.StartLineNumber x.LastLineNumber x.Snapshot

    /// Create for the entire ITextSnapshot
    static member CreateForExtent (snapshot: ITextSnapshot) =
        let lineCount = EditorCoreUtil.GetNormalizedLineCount snapshot
        new SnapshotLineRange(snapshot, 0, lineCount)

    /// Create for a single ITextSnapshotLine
    static member CreateForLine (snapshotLine: ITextSnapshotLine) = new SnapshotLineRange(snapshotLine.Snapshot, snapshotLine.LineNumber, 1)

    static member CreateForSpan (span: SnapshotSpan) =
        let startLine = span.Start.GetContainingLine()
        // TODO use GetLastLine
        let lastLine = 
            if span.Length > 0 then span.End.Subtract(1).GetContainingLine()
            else span.Start.GetContainingLine()
        SnapshotLineRange.CreateForLineRange startLine lastLine

    /// Create a range for the provided ITextSnapshotLine and with at most count 
    /// length.  If count pushes the range past the end of the buffer then the 
    /// span will go to the end of the buffer
    static member CreateForLineAndMaxCount (snapshotLine: ITextSnapshotLine) (count: int) = 
        let maxCount = (snapshotLine.Snapshot.LineCount - snapshotLine.LineNumber)
        let count = Math.Min(count, maxCount)
        new SnapshotLineRange(snapshotLine.Snapshot, snapshotLine.LineNumber, count)

    /// Create a SnapshotLineRange which includes the 2 lines
    static member CreateForLineRange (startLine: ITextSnapshotLine) (lastLine: ITextSnapshotLine) =
        Contract.Requires(startLine.Snapshot = lastLine.Snapshot)
        let count = (lastLine.LineNumber - startLine.LineNumber) + 1
        new SnapshotLineRange(startLine.Snapshot, startLine.LineNumber, count)

    /// <summary>
    /// Create a SnapshotLineRange which includes the 2 lines
    /// </summary>
    static member CreateForLineNumberRange (snapshot: ITextSnapshot) (startLine: int) (lastLine: int): Nullable<SnapshotLineRange> =
        Contract.Requires(startLine <= lastLine)
        if (startLine >= snapshot.LineCount || lastLine >= snapshot.LineCount) then
            Nullable<SnapshotLineRange>()
        else
            let range = SnapshotLineRange(snapshot, startLine, (lastLine - startLine) + 1)
            Nullable<SnapshotLineRange>(range)


/// This is the representation of a point within a particular line.  It's common
/// to represent a column in vim and using a SnapshotPoint isn't always the best
/// representation.  Finding the containing ITextSnapshotLine for a given 
/// SnapshotPoint is an allocating operation and often shows up as a critical 
/// metric in profiling.  This structure pairs the two together in a type safe fashion
[<Struct>]
[<NoEquality>]
[<NoComparison>]
type SnapshotColumn 
    (
        _snapshotLine: ITextSnapshotLine,
        _column: int
    ) =

    new (point: SnapshotPoint) = 
        let line = point.GetContainingLine()
        let column = point.Position - line.Start.Position
        SnapshotColumn(line, column)

    member x.IsStartOfLine = _column = 0

    member x.IsInsideLineBreak = EditorCoreUtil.IsInsideLineBreak x.Point x.Line

    member x.Line = _snapshotLine

    member x.LineNumber = _snapshotLine.LineNumber

    member x.Snapshot = _snapshotLine.Snapshot

    member x.Point = _snapshotLine.Start.Add(_column)

    member x.Column = _column

    member x.Add count = 
        let column = _column + count
        if column > 0 && column < _snapshotLine.LengthIncludingLineBreak then
            SnapshotColumn(_snapshotLine, _column + count)
        else
            let point = x.Point.Add count
            SnapshotColumn(point)

    member x.Subtract count = 
        x.Add -count

    override x.ToString() = 
        x.Point.ToString()

/// Conceptually, a character span corresponds to a single logical character:
/// - a normal character
/// - a line break (which may consist of one or more physical characters)
/// - a UTF32 character (represented by two UTF16 characters called the "surrogate pair")
/// Alternatively, a character span represents the places where it is valid to set the caret.
[<Struct>]
[<NoEquality>]
[<NoComparison>]
type SnapshotCharacterSpan =

    val private _line: ITextSnapshotLine
    val private _positions: int array
    val private _columnNumber: int

    /// Constructor for a character span corresponding to a point
    new (point: SnapshotPoint) =
        let line = point.GetContainingLine();
        SnapshotCharacterSpan(line, point)

    /// Constructor for a character span corresponding to a point within a line
    /// that looks up the column number by searching the positions array
    new (line: ITextSnapshotLine, point: SnapshotPoint) =

        let startColumn = SnapshotCharacterSpan(line)
        let positions = startColumn._positions

        // Scan the positions array for the column corresponding to point.
        let position = point.Position
        let columnNumber =
            let mutable columnNumber = positions.Length - 1
            for i = 0 to positions.Length - 2 do
                if position >= positions.[i] && position < positions.[i + 1] then
                    columnNumber <- i
            columnNumber

        { _line = line; _positions = positions; _columnNumber = columnNumber }

    /// Constructor for a character span corresponding to the first
    /// column of a line that "parses" the line and builds a lookup
    /// table of physical positions corresponding to logical columns
    new (line: ITextSnapshotLine) =
        let mutable currentPoint = line.Start
        let endPoint = line.EndIncludingLineBreak

        // Build an array of positions (including an extra final position)
        // that we can use to efficiently translate from column to position.
        /// Once determined, the snapshot line and the positions array
        /// are reused for other character span within the line.
        let positions =
            seq {
                while currentPoint.Position < endPoint.Position do
                    yield currentPoint.Position
                    let span =EditorCoreUtil.GetCharacterSpan line currentPoint
                    currentPoint <- span.End
                yield endPoint.Position
            }
            |> Seq.toArray

        { _line = line; _positions = positions; _columnNumber = 0 }

    /// Constructor for a new character span from the same line as another character span
    /// (Warning: requires that you have validated the column number)
    new (column: SnapshotCharacterSpan, columnNumber: int) =
        Contract.Requires(columnNumber >= 0 && columnNumber <= column.ColumnCountIncludingLineBreak)
        { _line = column._line; _positions = column._positions; _columnNumber = columnNumber }

    /// The snapshot line containing the column
    member x.Line = x._line

    /// The column number of the column
    /// (Warning: don't use the column number as a buffer position offset)
    member x.ColumnNumber = x._columnNumber

    /// The position or text buffer offset of the column
    member x.Position = x._positions.[x._columnNumber]

    /// The point where the column begins
    member x.Point = new SnapshotPoint(x.Snapshot, x.Position)

    /// The position offset of the column relative the beginning of the line
    member x.Offset = x.Position - x._positions.[0]

    /// The width of the column in terms of position
    member x.Width = x._positions.[x._columnNumber + 1] - x.Position

    /// The snapshot span covering the logical character at the column
    member x.Span = new SnapshotSpan(x.Snapshot, x.Position, x.Width)

    // The number of columns in the line containing the column including the linebreak
    member x.ColumnCountIncludingLineBreak = x._positions.Length - 1

    // The number of columns in the line containing the column
    member x.ColumnCount =
        if x._line.LineBreakLength = 0 then x._positions.Length - 1 else x._positions.Length - 2

    /// Whether the "character" at column is a linebreak
    member x.IsLineBreak = x.Position = x._line.End.Position

    /// Whether this is the first column of the line
    member x.IsStartOfLine = x._columnNumber = 0

    /// The line number of the line containing the column
    member x.LineNumber = x._line.LineNumber

    /// The snapshot corresponding to the column
    member x.Snapshot = x._line.Snapshot

    /// Go forward (positive) or backward (negative) by the specified number of
    /// columns stopping at the beginning or end of the buffer
    /// (note that linebreaks and surrogate pairs count as a single column)
    member x.Add count =

        // Start with the line of the current column
        // and adjust the column number by the specified amount.
        let mutable column = x
        let mutable columnNumber = x._columnNumber + count

        // While the column number is negative and there is a preceding line,
        // move to that line and add its column count.
        while columnNumber < 0 && column.LineNumber > 0 do
            let previousLine = column.Snapshot.GetLineFromLineNumber(column.LineNumber - 1)
            column <- SnapshotCharacterSpan(previousLine)
            columnNumber <- columnNumber + column.ColumnCountIncludingLineBreak
        columnNumber <- max 0 columnNumber

        // While the column number is past the end of the current line and there
        // is a following line, subtract the current line's column count and
        // move to that line.
        while columnNumber >= column.ColumnCountIncludingLineBreak && column.LineNumber < column.Snapshot.LineCount - 1 do
            let nextLine = column.Snapshot.GetLineFromLineNumber(column.LineNumber + 1)
            columnNumber <- columnNumber - column.ColumnCountIncludingLineBreak
            column <- SnapshotCharacterSpan(nextLine)
        columnNumber <- min columnNumber column.ColumnCountIncludingLineBreak

        // Return a new snapshot column.
        SnapshotCharacterSpan(column, columnNumber)

    /// Go backward (positive) or forward (negative) by the specified number of
    /// columns
    member x.Subtract count =
        x.Add -count

    /// Get the text corresponding to the column
    member x.GetText () =
        x.Span.GetText()

    /// Debugger display
    override x.ToString() =
        sprintf "Point: %s Line: %d Column: %d" (x.Point.ToString()) x.LineNumber x.ColumnNumber

/// The Text Editor interfaces only have granularity down to the character in the 
/// ITextBuffer.  However Vim needs to go a bit deeper in certain scenarios like 
/// BlockSpan's.  It needs to understand spaces within a single SnapshotPoint when
/// there are multiple logical characters (like tabs).  This structure represents
/// a value within a SnapshotPoint
[<StructuralEquality>]
[<NoComparison>]
[<Struct>]
[<DebuggerDisplay("{ToString()}")>]
type SnapshotOverlapPoint =

    val private _point: SnapshotPoint

    /// The number of spaces into the point where this overlap point occurs
    val private _beforeSpaces: int

    /// The number of spaces the point occupies in the editor. 
    ///
    /// An interesting case to consider here is tabs.  They will not always occupy 
    /// 'tabstop' spaces.  It can occupy less if there is a character in front of the 
    /// tab which occurs on a 'tabstop' boundary. 
    val private _totalSpaces: int

    /// !!!Do not call this directly!!!
    ///
    /// This constructor is meant for internal usage only.  If friend types existed this would employ
    /// a friend type to protect it.  It's far too easy to get the 'totalSpaces' parameter incorrect.  Instead
    /// go through a supported API for creating them
    internal new (point: SnapshotPoint, beforeSpaces: int, totalSpaces: int) = 
        if totalSpaces < 0 then
            invalidArg "totalSpaces" "totalSpaces must be positive"
        { _point = point; _beforeSpaces = beforeSpaces; _totalSpaces = totalSpaces }

    /// Create a SnapshotOverlapPoint over a SnapshotPoint value.  Even if the character underneath
    /// the SnapshotPoint is wide this will treate it as a width 0 character.  It will never see it
    /// as an overlap 
    ///
    /// TODO: This API is fundamentally incorrect because it treats all points as a one space item
    /// even if the underlying character is spaces wide.  
    new (point: SnapshotPoint) =
        let width = 
            if EditorCoreUtil.IsEndPoint point then
                0
            else
                1
        { _point = point; _beforeSpaces = 0; _totalSpaces = width }

    /// The number of spaces in the overlap point before this space
    member x.SpacesBefore = x._beforeSpaces

    /// The number of spaces in the overlap point after this space 
    member x.SpacesAfter = max 0 ((x._totalSpaces - 1) - x._beforeSpaces)

    /// The SnapshotPoint in which this overlap occurs
    member x.Point = x._point

    /// Number of spaces this SnapshotOverlapPoint occupies
    member x.Spaces = x._totalSpaces

    member x.Snapshot = x._point.Snapshot

    static member op_Equality(this,other) = System.Collections.Generic.EqualityComparer<SnapshotOverlapPoint>.Default.Equals(this,other)
    static member op_Inequality(this,other) = not (System.Collections.Generic.EqualityComparer<SnapshotOverlapPoint>.Default.Equals(this,other))

    override x.ToString() = 
        sprintf "Point: %s Spaces: %d Before: %d After: %d" (x.Point.ToString()) x.Spaces x.SpacesBefore x.SpacesAfter

[<StructuralEquality>] 
[<NoComparison>] 
[<Struct>] 
[<DebuggerDisplay("{ToString()}")>] 
type SnapshotOverlapSpan = 

    val private _start: SnapshotOverlapPoint
    val private _end: SnapshotOverlapPoint 

    new (startPoint: SnapshotOverlapPoint, endPoint: SnapshotOverlapPoint) = 
        if startPoint.Point.Position + startPoint.SpacesBefore > endPoint.Point.Position + endPoint.SpacesBefore then
            invalidArg "endPoint" "End cannot be before the start"
        { _start = startPoint; _end = endPoint }

    new (span: SnapshotSpan) =
        let startPoint = SnapshotOverlapPoint(span.Start)
        let endPoint = SnapshotOverlapPoint(span.End)
        { _start = startPoint; _end = endPoint }

    member x.Start = x._start

    member x.End = x._end

    /// Does this structure have any overlap
    member x.HasOverlap = x.HasOverlapStart || x.HasOverlapEnd

    /// Does this structure have any overlap at the start
    member x.HasOverlapStart = x.Start.SpacesBefore > 0 

    /// Does this structure have any overlap at the end 
    member x.HasOverlapEnd = x.End.SpacesBefore > 0 

    member x.OverarchingStart = x._start.Point

    member x.OverarchingEnd = 
        if x.End.SpacesBefore = 0 then
           x.End.Point
        else
            EditorCoreUtil.AddOneOrCurrent x.End.Point

    /// A SnapshotSpan which fully encompasses this overlap span 
    member x.OverarchingSpan = SnapshotSpan(x.OverarchingStart, x.OverarchingEnd)

    /// This is the SnapshotSpan which contains the SnapshotPoint values which have 
    /// full coverage.  The edges which have overlap are excluded from this span
    member x.InnerSpan =    
        let startPoint = 
            if x.Start.SpacesBefore = 0 then
                x.Start.Point
            else
                EditorCoreUtil.AddOneOrCurrent x.Start.Point
        let endPoint = 
            if x.End.SpacesBefore = 0 then
                x.End.Point
            else
                EditorCoreUtil.SubtractOneOrCurrent x.End.Point
        if startPoint.Position <= endPoint.Position then
            SnapshotSpan(startPoint, endPoint)
        else
            SnapshotSpan(startPoint, startPoint)

    member x.Snapshot = x._start.Snapshot

    /// Get the text contained in this SnapshotOverlapSpan.  All overlap points are expressed
    /// with the appropriate number of spaces 
    member x.GetText() = 

        let builder = StringBuilder()

        if x.Start.Point.Position = x.End.Point.Position then
            // Special case the scenario where the span is within a single SnapshotPoint
            // value.  Just create the correct number of spaces here 
            let count = x.End.SpacesBefore - x.Start.SpacesBefore 
            for i = 1 to count do 
                builder.AppendChar ' '
        else
            // First add in the spaces for the start if it is an overlap point 
            let mutable position = x.Start.Point.Position
            if x.Start.SpacesBefore > 0 then
                for i = 0 to x.Start.SpacesAfter do
                    builder.AppendChar ' '
                position <- position + 1

            // Next add in the middle SnapshotPoint values which don't have any overlap
            // to consider.  Don't use InnerSpan.GetText() here as it will unnecessarily
            // allocate an extra string 
            while position < x.End.Point.Position do
                let point = SnapshotPoint(x.Snapshot, position)
                let c = point.GetChar()
                builder.AppendChar c
                position <- position + 1

            // Lastly add in the spaces on the end point.  Remember End is exclusive so 
            // only add spaces which come before
            if x.End.SpacesBefore > 0 then
                for i = 0 to (x.End.SpacesBefore - 1) do
                    builder.AppendChar ' '

        builder.ToString()

    override x.ToString() = 
        x.OverarchingSpan.ToString()

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
/// A snapshot may include the phantom line, i.e. the empty line after a final newline.
/// Normalized lines exclude the phantom line but there is always at least one normalized
/// line in a snapshot.
module SnapshotUtil = 

    /// Get the number of lines in the ITextSnapshot
    let GetLineCount (snapshot: ITextSnapshot) = snapshot.LineCount

    /// Get the number of normalized lines in the ITextSnapshot
    let GetNormalizedLineCount (snapshot: ITextSnapshot) = EditorCoreUtil.GetNormalizedLineCount snapshot

    /// Get the number of normalized lines in the ITextSnapshot,
    /// excluding the empty line of an empty buffer
    let GetNormalizedLineCountExcludingEmpty (snapshot: ITextSnapshot) =
        if snapshot.Length = 0 then 0 else EditorCoreUtil.GetNormalizedLineCount snapshot

    /// Get the line for the specified number
    let GetLine (tss:ITextSnapshot) lineNumber = tss.GetLineFromLineNumber lineNumber

    /// Get the length of the ITextSnapshot
    let GetLength (tss:ITextSnapshot) = tss.Length

    /// Get the character at the specified index
    let GetChar index (tss: ITextSnapshot) = tss.[index]

    /// Get the first line in the snapshot
    let GetFirstLine tss = GetLine tss 0

    /// Whether the snapshot ends with a linebreak
    let EndsWithLineBreak (snapshot: ITextSnapshot) = EditorCoreUtil.EndsWithLineBreak snapshot

    /// Get the last line number of the snapshot
    let GetLastLineNumber (snapshot: ITextSnapshot) = snapshot.LineCount - 1

    /// Get the last normmalized line number of the snapshot
    let GetLastNormalizedLineNumber snapshot = EditorCoreUtil.GetLastNormalizedLineNumber snapshot

    /// Get the last line of the snapshot
    let GetLastLine snapshot = GetLastLineNumber snapshot |> GetLine snapshot

    /// Get the last normalized line of the snapshot
    let GetLastNormalizedLine snapshot = GetLastNormalizedLineNumber snapshot |> GetLine snapshot

    /// Get the end point of the snapshot
    let GetEndPoint (tss:ITextSnapshot) = SnapshotPoint(tss, tss.Length)

    /// Get the end point of the last line of the snapshot
    let GetEndPointOfLastLine (snapshot: ITextSnapshot) = 
        let lastLine = GetLastNormalizedLine snapshot
        lastLine.End

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
    let IsLineNumberValid (snapshot: ITextSnapshot) lineNumber =
        let lastLineNumber = GetLastNormalizedLineNumber snapshot
        lineNumber >= 0 && lineNumber <= lastLineNumber

    /// Is the specified number is the number of the phantom line
    let IsPhantomLineNumber (snapshot: ITextSnapshot) lineNumber =
        lineNumber = snapshot.LineCount - 1 && EndsWithLineBreak snapshot

    /// Is the Span valid in this ITextSnapshot
    let IsSpanValid (tss:ITextSnapshot) (span:Span) = 
        let length = tss.Length
        span.Start < tss.Length && span.End <= tss.Length

    /// Whether all lines in the ITextSnapshot have linebreaks
    let AllLinesHaveLineBreaks (snapshot: ITextSnapshot) = 
        let endPoint = GetEndPoint snapshot
        let line = endPoint.GetContainingLine()
        line.Length = 0

    /// Get a valid line for the specified number if it's valid and the last line if it's
    /// not
    let GetLineOrLast tss lineNumber =
        let lineNumber = if IsLineNumberValid tss lineNumber then lineNumber else GetLastNormalizedLineNumber tss 
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
        | SearchPath.Forward, true ->
            let endLineNumber = GetLastLineNumber snapshot
            seq {
                for i = lineNumber to endLineNumber do
                    yield GetLine snapshot i
            }
        | SearchPath.Backward, true ->
            seq {
                for i = 0 to lineNumber do
                    let number = lineNumber - i
                    yield GetLine snapshot number
            }
        | SearchPath.Forward, false -> 
            Seq.empty
        | SearchPath.Backward, false ->
            Seq.empty

    /// Get the lines in the specified range
    let GetLineRange snapshot startLineNumber endLineNumber = 
        let count = endLineNumber - startLineNumber + 1
        GetLines snapshot startLineNumber SearchPath.Forward
        |> Seq.truncate count

    /// Try and get the line at the specified number
    /// (may return the phantom line)
    let TryGetLine (snapshot: ITextSnapshot) number = 
        if IsLineNumberValid snapshot number || IsPhantomLineNumber snapshot number then
            GetLine snapshot number |> Some
        else
            None

    /// Try and get the point on the specified line
    let TryGetPointInLine snapshot lineNumber column = 
        match TryGetLine snapshot lineNumber with
        | None -> None
        | Some snapshotLine ->
            if column >= snapshotLine.Length then
                None
            else
                snapshotLine.Start.Add(column) |> Some

    /// Get the point from the specified position
    let GetPoint (snapshot: ITextSnapshot) position = SnapshotPoint(snapshot, position)

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
            | SearchPath.Forward ->
                let max = span.Length - 1
                seq {
                    for i = 0 to max do
                        yield i + offset
                }
            | SearchPath.Backward ->
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
    let GetLastLine (span: SnapshotSpan) = 
        if span.Length > 0 then span.End.Subtract(1).GetContainingLine()
        else GetStartLine(span)

    /// Get the start and end line of the SnapshotSpan.  Remember that End is not a part of
    /// the span but instead the first point after the span
    let GetStartAndLastLine span = GetStartLine span, GetLastLine span

    /// Get the number of lines in this SnapshotSpan
    let GetLineCount span = 
        let startLine, lastLine = GetStartAndLastLine span
        (lastLine.LineNumber - startLine.LineNumber) + 1

    /// Is this a multiline SnapshotSpan
    let IsMultiline span = 
        let startLine, lastLine = GetStartAndLastLine span
        startLine.LineNumber < lastLine.LineNumber

    /// Gets the last point which is actually included in the span.  This is different than
    /// EndPoint which is the first point after the span
    let GetLastIncludedPoint (span:SnapshotSpan) =
        if span.Length = 0 then None
        else span.End.Subtract(1) |> Some

    /// Gets the last point which is actually included in the span.  This is different than
    /// EndPoint which is the first point after the span
    let GetLastIncludedPointOrStart (span:SnapshotSpan) =
        if span.Length = 0 then span.Start
        else span.End.Subtract(1)

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
        let startLine, lastLine = GetStartAndLastLine span
        let endLine = SnapshotUtil.GetLineOrLast span.Snapshot (lastLine.LineNumber+lineCount)
        SnapshotSpan(startLine.Start, lastLine.End)

    /// Extend the SnapshotSpan count lines downwards.  If the count exceeds the end of the
    /// Snapshot it will extend to the end.  The resulting Span will include the line break
    /// of the last line
    let ExtendDownIncludingLineBreak span lineCount = 
        let span = ExtendDown span lineCount
        let lastLine = GetLastLine span
        SnapshotSpan(span.Start, lastLine.EndIncludingLineBreak)

    /// Extend the SnapshotSpan to be the full line at both the start and end points
    let ExtendToFullLine span =
        let startLine, lastLine = GetStartAndLastLine span
        SnapshotSpan(startLine.Start, lastLine.End)

    /// Extend the SnapshotSpan to be the full line at both the start and end points
    let ExtendToFullLineIncludingLineBreak span =
        let startLine, lastLine = GetStartAndLastLine span
        SnapshotSpan(startLine.Start, lastLine.EndIncludingLineBreak)

    /// Reduces the SnapshotSpan to the subspan of the first line
    let ReduceToStartLine span = 
        if IsMultiline span then 
            let line = GetStartLine span
            SnapshotSpan(span.Start, line.EndIncludingLineBreak)
        else span

    /// Reduces the SnapshotSpan to the subspan of the last line
    let ReduceToEndLine span = 
        if IsMultiline span then 
            let line = GetLastLine span
            SnapshotSpan(line.Start, span.End)
        else span

    /// Get the ITextSnapshotLines included in this SnasphotSpan 
    let GetAllLines span = 
        let startLine = GetStartLine span
        let count = GetLineCount span
        SnapshotUtil.GetLines span.Snapshot startLine.LineNumber SearchPath.Forward
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
    let GetOverarchingSpan (col: NonEmptyCollection<SnapshotSpan>) =
        let startPoint = col |> Seq.map (fun span -> span.Start) |> Seq.minBy (fun p -> p.Position)
        let endPoint = col |> Seq.map (fun span -> span.End) |> Seq.maxBy (fun p -> p.Position)
        SnapshotSpan(startPoint, endPoint)

    /// Create an span going from startPoint to endpoint
    let Create (startPoint:SnapshotPoint) (endPoint:SnapshotPoint) = SnapshotSpan(startPoint,endPoint)

    /// Create an empty span at the given point
    let CreateEmpty point = SnapshotSpan(point, 0)

    /// Create a span from the given point with the specified length
    let CreateWithLength (startPoint: SnapshotPoint) (length: int) = SnapshotSpan(startPoint, length)

    /// Create a span which is the overarching span of the two provided SnapshotSpan values
    let CreateOverarching (leftSpan: SnapshotSpan) (rightSpan: SnapshotSpan) = 
        Contract.Requires (leftSpan.Snapshot = rightSpan.Snapshot)
        let snapshot = leftSpan.Snapshot
        let startPoint = 
            let position = min leftSpan.Start.Position rightSpan.Start.Position
            SnapshotPoint(snapshot, position)
        let endPoint = 
            let position = max leftSpan.End.Position rightSpan.End.Position
            SnapshotPoint(snapshot, position)
        SnapshotSpan(startPoint, endPoint)

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
    let GetOverarchingSpan col =
        let first = GetFirst col
        let last = GetLast col
        SnapshotSpan(first.Start,last.End) 

    /// Get the first item 
    let TryGetFirst (col: NormalizedSnapshotSpanCollection) = if col.Count = 0 then None else Some (col.[0])

    let OfSeq (s:SnapshotSpan seq) = new NormalizedSnapshotSpanCollection(s)

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module SnapshotLineUtil =

    /// ITextSnapshot the ITextSnapshotLine is associated with
    let GetSnapshot (line: ITextSnapshotLine) = line.Snapshot

    /// Length of the line
    let GetLength (line: ITextSnapshotLine) = line.Length

    /// Length of the line including the line break
    let GetLengthIncludingLineBreak (line: ITextSnapshotLine) = line.LengthIncludingLineBreak

    /// Get the length of the line break
    let GetLineBreakLength (line:ITextSnapshotLine) = line.LengthIncludingLineBreak - line.Length

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

    // Get the span of the character which is pointed to by the point
    let GetCharacterSpan line point = EditorCoreUtil.GetCharacterSpan line point

    /// Get the points on the particular line in order 
    let GetPoints path line = line |> GetExtent |> SnapshotSpanUtil.GetPoints path

    /// Get the points on the particular line including the line break
    let GetPointsIncludingLineBreak path line = line |> GetExtentIncludingLineBreak |> SnapshotSpanUtil.GetPoints path

    /// Get the columns in the line in the path
    let private GetColumnsCore path includeLineBreak line = 
        let length = 
            if includeLineBreak then
                GetLengthIncludingLineBreak line
            else 
                GetLength line
        let max = length - 1
        match path with 
        | SearchPath.Forward ->
            seq { 
                for i = 0 to max do
                    yield SnapshotColumn(line, i)
            }
        | SearchPath.Backward ->
            seq { 
                for i = 0 to max do
                    let column = (length - 1) - i
                    yield SnapshotColumn(line, column)
            }

    /// Get the columns in the specified direction 
    let GetColumns path line = GetColumnsCore path false line

    /// Get the columns in the specified direction including the line break
    let GetColumnsIncludingLineBreak path line = GetColumnsCore path true line

    /// Get the character spans in the line in the path
    let private GetCharacterSpansCore path includeLineBreak line =
        let startColumn = SnapshotCharacterSpan(GetStart line)
        let columnCount =
            if includeLineBreak then startColumn.ColumnCountIncludingLineBreak
            else startColumn.ColumnCount
        let max = columnCount - 1
        match path with
        | SearchPath.Forward ->
            seq {
                for i = 0 to max do
                    yield SnapshotCharacterSpan(startColumn, i)
            }
        | SearchPath.Backward ->
            seq {
                for i = 0 to max do
                    yield SnapshotCharacterSpan(startColumn, max - i)
            }

    /// Get the character spans in the specified direction
    let GetCharacterSpans path line = GetCharacterSpansCore path false line

    /// Get the character spans in the specified direction including the line break
    let GetCharacterSpansIncludingLineBreak path line = GetCharacterSpansCore path true line

    /// Get the line break span 
    let GetLineBreakSpan line = 
        let point = GetEnd line
        let length = GetLineBreakLength line
        SnapshotSpan(point,length)

    /// Get the indent point of the ITextSnapshotLine
    let GetIndentPoint line =
        line 
        |> GetPoints SearchPath.Forward
        |> Seq.skipWhile (fun point -> point.GetChar() |> CharUtil.IsBlank)
        |> SeqUtil.tryHeadOnly
        |> OptionUtil.getOrDefault (GetEnd line)

    /// Get the indentation span of the ITextSnapshotLine
    let GetIndentSpan line = 
        let point = GetIndentPoint line
        SnapshotSpan(line.Start, point)

    /// Get the indentation text of the ITextSnapshotLine
    let GetIndentText line = 
        let span = GetIndentSpan line
        span.GetText()

    /// Get the text of the ITextSnapshotLine 
    let GetText (line: ITextSnapshotLine) = line.GetText()

    /// Get the text of the ITextSnapshotLine including the line break
    let GetTextIncludingLineBreak (line: ITextSnapshotLine) = line.GetTextIncludingLineBreak()

    /// Get the last point which is included in this line not including the line 
    /// break.  Can be None if this is a 0 length line 
    let GetLastIncludedPoint line = 
        let span = GetExtent line
        if span.Length = 0 then None
        else span.End.Subtract(1) |> Some

    let GetLastIncludedPointOrStart line = 
        match GetLastIncludedPoint line with
        | Some p -> p 
        | None -> line.Start

    /// Is this the last included point on the ITextSnapshotLine
    let IsLastPoint (line: ITextSnapshotLine) point = 
        if line.Length = 0 then point = line.Start
        else point.Position + 1 = line.End.Position

    /// Is this the last point on the line including the line break
    let IsLastPointIncludingLineBreak (line: ITextSnapshotLine) (point: SnapshotPoint) = 
        point.Position + 1 = line.EndIncludingLineBreak.Position

    /// Whether this is the last line
    let IsLastLine (line: ITextSnapshotLine) = 
        line.LineNumber = SnapshotUtil.GetLastLineNumber line.Snapshot

    /// Whether this is the last normalized line
    let IsLastNormalizedLine (line: ITextSnapshotLine) = 
        line.LineNumber = SnapshotUtil.GetLastNormalizedLineNumber line.Snapshot

    /// Whether the specified line is the phantom line
    let IsPhantomLine (line: ITextSnapshotLine) =
        SnapshotUtil.IsPhantomLineNumber line.Snapshot line.LineNumber

    /// Whether this line has a line break
    let HasLineBreak (line: ITextSnapshotLine) = 
        line.LineBreakLength <> 0

    /// Is the line empty
    let IsEmpty line = 
        GetLength line = 0 

    /// Is the line empty or consisting of only blank characters
    let IsBlankOrEmpty line = 
        line
        |> GetExtent
        |> SnapshotSpanUtil.GetPoints SearchPath.Forward
        |> Seq.forall (fun point -> CharUtil.IsBlank (point.GetChar()))

    /// Does the line have at least 1 character all of which are blank?
    let IsBlank line = GetLength line > 0 && IsBlankOrEmpty line

    /// Get the first non-blank character on the line
    let GetFirstNonBlank line = 
        line
        |> GetExtent
        |> SnapshotSpanUtil.GetPoints SearchPath.Forward
        |> Seq.skipWhile (fun point -> CharUtil.IsBlank (point.GetChar()))
        |> SeqUtil.tryHeadOnly

    // Checks if the point is the first non blank character of the line
    let IsFirstNonBlank (point: SnapshotPoint) =
        point.GetContainingLine()
        |> GetFirstNonBlank = Some(point)

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

    /// Get the SnapshotSpan for the given column in length within the extent of the
    /// line.  If the column or length exceeds the length of the line then an
    /// End will be used in it's place
    let GetSpanInLine (line: ITextSnapshotLine) column length =
        let startPoint = 
            if column >= line.Length then
                line.End
            else
                line.Start.Add column
        let endPoint = 
            let offset = column + length
            if offset >= line.Length then
                line.End
            else
                line.Start.Add offset
        SnapshotSpan(startPoint, endPoint)

    /// Get a SnapshotPoint representing the nth characters into the line or the 
    /// End point of the line.  This is done using positioning 
    let GetColumnOrEnd column (line: ITextSnapshotLine) = 
        if line.Start.Position + column >= line.End.Position then line.End
        else line.Start.Add(column)

    /// Get a SnapshotPoint representing 'offset' characters into the line or it's
    /// line break or the EndIncludingLineBreak of the line
    let GetColumnOrEndIncludingLineBreak column (line: ITextSnapshotLine) = 
        if line.Start.Position + column >= line.EndIncludingLineBreak.Position then line.EndIncludingLineBreak
        else line.Start.Add(column)

    // Get the point in the given line which is just before the character that 
    // overlaps the specified column into the line, as well as the position of 
    // that column inside the character. Returns End if it goes beyond the last 
    // point in the string
    let GetSpaceWithOverlapOrEnd line spacesCount tabStop = 

        let snapshot = GetSnapshot line
        let endPoint = GetEnd line

        let mutable point = line.Start
        let mutable spaces = 0 
        let mutable value: SnapshotOverlapPoint option = None

        if point = endPoint then
            value <- Some (SnapshotOverlapPoint(point, 0, 0))

        while Option.isNone value do
            let currentWidth = 
                let current = point.GetChar()
                if current = '\t' then 
                    // A tab takes up the remaining spaces on a tabstop increment.
                    let remainder = tabStop - (spaces % tabStop)
                    if remainder = 0 then tabStop
                    else remainder
                else
                    CharUtil.GetCharacterWidth current tabStop

            if spaces = spacesCount then
                // Landed at the SnapshotPoint in question
                value <- Some (SnapshotOverlapPoint(point, 0, currentWidth))
            elif (spaces + currentWidth) > spacesCount then
                // The space is a slice of a SnapshotPoint value.  Have to determine the
                // offset
                let before = spacesCount - spaces
                value <- Some (SnapshotOverlapPoint(point, before, currentWidth))
            else
                point <- SnapshotPoint(snapshot, point.Position + 1)
                spaces <- spaces + currentWidth

                if point = endPoint then
                    value <- Some (SnapshotOverlapPoint(point, 0, 0))

        Option.get value

    // Get the point in the given line which is just before the character that 
    // overlaps the specified column into the line. Returns End if it goes 
    // beyond the last point in the string
    let GetSpaceOrEnd line spacesCount tabStop = 
        let overlapPoint = GetSpaceWithOverlapOrEnd line spacesCount tabStop
        overlapPoint.Point

    /// Get the count of spaces to get to the specified absolute column offset.  This will count
    /// tabs as counting for 'tabstop' spaces.  Note though that tabs which don't occur on a 'tabstop'
    /// boundary only count for the number of spaces to get to the next tabstop boundary
    let GetSpacesToColumn (line: ITextSnapshotLine) column tabStop = 
        let mutable spaces = 0
        let mutable current = SnapshotColumn(line.Start)
        let maxColumn = min column line.Length
        while current.Column < maxColumn do
            let c = current.Point.GetChar()
            if c = '\t' then
                let remainder = spaces % tabStop 
                spaces <- spaces + (tabStop - remainder)
            else
                spaces <- spaces + EditorCoreUtil.GetCharacterWidth current.Point tabStop
            current <- current.Add 1
        
        spaces

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module SnapshotPointUtil =

    /// Get the position
    let GetPosition (point:SnapshotPoint) = point.Position
   
    /// Get the ITextSnapshotLine containing the specified SnapshotPoint
    /// (may return the phantom line)
    let GetContainingLine (point:SnapshotPoint) = point.GetContainingLine()

    /// Get the ITextSnapshotLine containing the specified SnapshotPoint or the last line
    /// (will not return the phantom line)
    let GetContainingLineOrLast (point: SnapshotPoint) =
        if EditorCoreUtil.IsEndPoint point then
            SnapshotUtil.GetLastNormalizedLine point.Snapshot
        else
            point.GetContainingLine()

    /// Get the ITextSnapshot containing the SnapshotPoint
    let GetSnapshot (point:SnapshotPoint) = point.Snapshot

    /// Get the ITextBuffer containing the SnapshotPoint
    let GetBuffer (point:SnapshotPoint) = point.Snapshot.TextBuffer

    /// Is this the start of the containing line?
    let IsStartOfLine point =
        let line = GetContainingLine point
        line.Start.Position = point.Position

    /// Is this the end of the containing line?
    let IsEndOfLine point =
        let line = GetContainingLine point
        line.End.Position = point.Position

    /// Is this the start of th eSnapshot
    let IsStartPoint point = 0 = GetPosition point

    /// Is this the end of the Snapshot
    let IsEndPoint point = 
        EditorCoreUtil.IsEndPoint point

    /// Is this the end of the last line of the Snapshot
    let IsEndPointOfLastLine (point: SnapshotPoint) = 
        let endPointOfLastLine = SnapshotUtil.GetEndPointOfLastLine point.Snapshot
        point.Position = endPointOfLastLine.Position

    /// Is the passed in SnapshotPoint inside the line break portion of the line
    let IsInsideLineBreak point = 
        let line = GetContainingLine point
        EditorCoreUtil.IsInsideLineBreak point line 

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

    /// Is this point a blank or inside the line break?
    let IsBlankOrInsideLineBreak point = 
        IsBlank point || IsInsideLineBreak point

    /// Is this point not a blank 
    let IsNotBlank point =
        not (IsBlank point)

    /// Is this point white space or inside the line break?
    let IsWhiteSpaceOrInsideLineBreak point = 
        IsWhiteSpace point || IsInsideLineBreak point

    /// Try and add count to the SnapshotPoint.  Will return None if this causes
    /// the point to go past the end of the Snapshot
    let TryAdd count point = 
        let pos = (GetPosition point) + count
        let snapshot = GetSnapshot point
        if pos > snapshot.Length then None
        else point.Add(count) |> Some

    /// Maybe add 1 to the given point.  Will return the original point
    /// if it's the end of the Snapshot
    let TryAddOne point = TryAdd 1 point

    /// Try and add a value to the point. If it's past the end of the snapshot then return
    /// the end
    let TryAddOrEnd count point = 
        match TryAdd count point with
        | Some p -> p
        | None -> SnapshotUtil.GetEndPoint point.Snapshot

    /// Add the given count to the SnapshotPoint
    let Add count (point:SnapshotPoint) = point.Add(count)

    /// Add 1 to the given SnapshotPoint
    let AddOne (point:SnapshotPoint) = point.Add(1)

    /// Add 1 to the given snapshot point unless it's the end of the buffer in which case just
    /// return the passed in value
    let AddOneOrCurrent point =
        EditorCoreUtil.AddOneOrCurrent point

    /// Subtract 1 from the SnapshotPoint
    let SubtractOne (point:SnapshotPoint) =  point.Subtract(1)

    /// Subtract the count from the SnapshotPoint
    let Subtract count (point:SnapshotPoint) =  point.Subtract(count)

    let TrySubtract (point:SnapshotPoint) count =  
        let position = point.Position - count
        if position >= 0 then SnapshotPoint(point.Snapshot, position) |> Some
        else None

    /// Maybe subtract the count from the SnapshotPoint
    let TrySubtractOne (point:SnapshotPoint) =  
        TrySubtract point 1

    /// Try and subtract 1 from the given point unless it's the start of the buffer in which
    /// case return the passed in value
    let SubtractOneOrCurrent point = 
        EditorCoreUtil.SubtractOneOrCurrent point

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

    // Get the span of the character which is pointed to by the point
    let GetCharacterSpan point =
        let line = GetContainingLine point
        SnapshotLineUtil.GetCharacterSpan line point

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

    /// Get the next character span in the buffer with wrap
    let GetNextCharacterSpanWithWrap point =
        let snapshot = GetSnapshot point
        let nextPoint =
            GetCharacterSpan point
            |> SnapshotSpanUtil.GetEndPoint
        if EditorCoreUtil.IsEndPoint nextPoint then
            SnapshotUtil.GetStartPoint snapshot
        else
            nextPoint

    /// Get the previous character span in the buffer with wrap
    let GetPreviousCharacterSpanWithWrap point =
        let snapshot = GetSnapshot point
        let currentPoint =
            GetCharacterSpan point
            |> SnapshotSpanUtil.GetStartPoint
        if currentPoint.Position = 0 then
            SnapshotUtil.GetEndPoint snapshot
        else
            SubtractOne currentPoint
            |> GetCharacterSpan
            |> SnapshotSpanUtil.GetStartPoint

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
        let _, column = GetLineColumn point 
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
        | SearchPath.Forward ->

            seq {
                // Return the rest of the start line if we are not in a line break
                if not (IsInsideLineBreak point) && not (IsEndPoint point) then
                    yield SnapshotSpan(point, startLine.End)

                // Return the rest of the line extents
                let lines = 
                    SnapshotUtil.GetLines snapshot startLine.LineNumber SearchPath.Forward
                    |> SeqUtil.skipMax 1
                    |> Seq.map SnapshotLineUtil.GetExtent
                yield! lines
            }

        | SearchPath.Backward ->
            seq {
                // Return the beginning of the start line if this is not the start
                if point <> startLine.Start then
                    yield SnapshotSpan(startLine.Start, point)

                // Return the rest of the line extents
                let lines =
                    SnapshotUtil.GetLines snapshot startLine.LineNumber SearchPath.Backward
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
            | SearchPath.Forward -> SnapshotSpan(point, SnapshotUtil.GetEndPoint snapshot)
            | SearchPath.Backward -> SnapshotSpan(SnapshotUtil.GetStartPoint snapshot, AddOneOrCurrent point)
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
    let GetPointsOnLineForward startPoint = 
        if IsInsideLineBreak startPoint then Seq.empty
        else 
            let line = GetContainingLine startPoint
            SnapshotSpan(startPoint, line.End) |> SnapshotSpanUtil.GetPoints SearchPath.Forward

    /// Get the points on the containing line start starting at the passed in value in reverse order.  If the
    /// passed in point is inside the line break then the points of the entire line will be returned
    let GetPointsOnLineBackward startPoint = 
        let line = GetContainingLine startPoint
        let span = 
            if IsInsideLineBreak startPoint then SnapshotLineUtil.GetExtent line 
            else 
                // Adding 1 is safe here.  The End position is always a valid SnapshotPoint and since we're
                // not in the line break startPoint must be < End and hence startPoint.Add(1) <= End
                SnapshotSpan(line.Start, startPoint.Add(1)) 
        span |> SnapshotSpanUtil.GetPoints SearchPath.Backward

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

    /// Try and get the previous character span on the same line.  If this is at the start of the line
    /// None will be returned (note this handles surrogate pairs)
    let TryGetPreviousCharacterSpanOnLine (point: SnapshotPoint) count =
        let column = SnapshotCharacterSpan(point)
        if column.ColumnNumber >= count then
            let previousColumn = column.Subtract count
            Some previousColumn.Point
        else
            None

    /// Try and get the next character span on the same line.  If this is the end of the line or if
    /// the point is within the line break then None will be returned (note this handles
    /// surrogate pairs)
    let TryGetNextCharacterSpanOnLine (point: SnapshotPoint) count =
        let column = SnapshotCharacterSpan(point)
        if column.ColumnNumber + count <= column.ColumnCount then
            let nextColumn = column.Add count
            Some nextColumn.Point
        else
            None

    /// Get a point relative to a starting point backward or forward
    /// 'count' characters skipping line breaks if 'skipLineBreaks' is
    /// specified.  Goes as far as possible in the specified direction
    let GetRelativePoint startPoint count skipLineBreaks =

        /// Get the relative column in 'direction' using predicate 'isEnd'
        /// to stop the motion
        let GetRelativeColumn direction (isEnd: SnapshotPoint -> bool) =

            /// Adjust 'column' backward or forward if it is in the
            /// middle of a line break
            let AdjustLineBreak (column: SnapshotColumn) =
                if column.Column <= column.Line.Length then
                    column
                else if direction = -1 then
                    SnapshotColumn(column.Line, column.Line.Length)
                else
                    SnapshotColumn(column.Line.EndIncludingLineBreak)

            let mutable column = SnapshotColumn(startPoint)
            let mutable remaining = abs count
            while remaining > 0 && not (isEnd column.Point) do
                column <- column.Add direction |> AdjustLineBreak
                remaining <- remaining -
                    if skipLineBreaks then
                        if column.Line.Length = 0 || not column.IsInsideLineBreak then
                            1
                        else
                            0
                    else
                        1
            column

        let column =
            if count < 0 then
                GetRelativeColumn -1 IsStartPoint
            else
                GetRelativeColumn 1 IsEndPointOfLastLine

        column.Point

    /// Get a character span relative to a starting point backward or forward
    /// 'count' characters skipping line breaks if 'skipLineBreaks' is
    /// specified.  Goes as far as possible in the specified direction
    let GetRelativeCharacterSpan (startPoint: SnapshotPoint) count skipLineBreaks =

        /// Get the relative column in 'direction' using predicate 'isEnd'
        /// to stop the motion
        let getRelativeColumn direction (isEnd: SnapshotPoint -> bool) =
            let mutable column = SnapshotCharacterSpan(startPoint)
            let mutable remaining = abs count
            while remaining > 0 && not (isEnd column.Point) do
                column <- column.Add direction
                remaining <- remaining -
                    if skipLineBreaks then
                        if column.Line.Length = 0 || not column.IsLineBreak then
                            1
                        else
                            0
                    else
                        1
            column

        let column =
            if count < 0 then
                getRelativeColumn -1 IsStartPoint
            else
                getRelativeColumn 1 IsEndPointOfLastLine

        column.Point

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

    /// Get the count of spaces to get to the specified point in its line when tabs are expanded
    let GetSpacesToPoint point tabStop = 
        let column = SnapshotColumn(point)
        SnapshotLineUtil.GetSpacesToColumn column.Line column.Column tabStop

    let GetCharacterWidth point tabStop = 
        EditorCoreUtil.GetCharacterWidth point tabStop

    /// Get the point or the end point of the last line, whichever is first
    let GetPointOrEndPointOfLastLine (point: SnapshotPoint) =
        let endPointOfLastLine = SnapshotUtil.GetEndPointOfLastLine point.Snapshot
        match OrderAscending point endPointOfLastLine with
        | (firstPoint, _) -> firstPoint

module VirtualSnapshotPointUtil =
    
    let OfPoint (point:SnapshotPoint) = VirtualSnapshotPoint(point)

    /// Convert the SnapshotPoint into a VirtualSnapshotPoint taking into account the editors
    /// view that SnapshotPoint values in the line break should be represented as 
    /// VirtualSnapshotPoint values
    let OfPointConsiderLineBreak point = 
        let line = SnapshotPointUtil.GetContainingLine point
        let  difference = point.Position - line.End.Position
        if difference > 0 then
            VirtualSnapshotPoint(line.End, difference)
        else
            VirtualSnapshotPoint(point)

    let GetPoint (point:VirtualSnapshotPoint) = point.Position

    let GetPosition point = 
        let point = GetPoint point
        point.Position

    let GetContainingLine (point:VirtualSnapshotPoint) = SnapshotPointUtil.GetContainingLine point.Position

    let IsInVirtualSpace (point:VirtualSnapshotPoint) = point.IsInVirtualSpace

    let GetLineColumn (point: VirtualSnapshotPoint) =
        let line = GetContainingLine point
        let realColumn = point.Position.Position - line.Start.Position
        line.LineNumber, realColumn + point.VirtualSpaces

    let GetColumnNumber (point: VirtualSnapshotPoint) =
        let _, columnNumber = GetLineColumn point
        columnNumber

    /// Add count to the VirtualSnapshotPoint keeping it on the same line
    let AddOnSameLine count point =
        let line = GetContainingLine point
        let columnNumber = GetColumnNumber point
        VirtualSnapshotPoint(line, columnNumber + count)

    /// Subtract count to the VirtualSnapshotPoint keeping it on the same line
    let SubtractOnSameLine count point = AddOnSameLine -count point

    /// Add one to the VirtualSnapshotPoint keeping it on the same line
    let AddOneOnSameLine point = AddOnSameLine 1 point

    /// Try and subtract 1 from the given point unless it's the start of the buffer in which
    /// case return the passed in value
    let SubtractOneOrCurrent (point: VirtualSnapshotPoint) =
        if point.IsInVirtualSpace then
            AddOnSameLine -1 point
        else
            point.Position
            |> SnapshotPointUtil.SubtractOneOrCurrent
            |> OfPoint

    /// Put two VirtualSnapshotPoint's in ascending order
    let OrderAscending (left: VirtualSnapshotPoint) (right: VirtualSnapshotPoint) =
        if left.CompareTo(right) < 0 then left,right
        else right,left

    /// Get the count of spaces to get to the specified point in its line when tabs are expanded
    let GetSpacesToPoint (point: VirtualSnapshotPoint) tabStop =
        let spaces = SnapshotPointUtil.GetSpacesToPoint point.Position tabStop
        spaces + point.VirtualSpaces

/// Contains operations that act on snapshot lines but return virtual snapshot points
module VirtualSnapshotLineUtil =

    // Get the virtual point in the given line which is just before the character that
    // overlaps the specified column into the line
    let GetSpace line spacesCount tabStop =
        let overlapPoint = SnapshotLineUtil.GetSpaceWithOverlapOrEnd line spacesCount tabStop
        let point = VirtualSnapshotPointUtil.OfPoint overlapPoint.Point
        if point.Position = line.End then
            let realSpaces = SnapshotLineUtil.GetSpacesToColumn line line.Length tabStop
            let virtualSpaces = spacesCount - realSpaces
            VirtualSnapshotPointUtil.AddOnSameLine virtualSpaces point
        else
            point

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module VirtualSnapshotSpanUtil =

    /// Get the span
    let GetSnapshotSpan (span:VirtualSnapshotSpan) = span.SnapshotSpan

    /// Get the start point
    let GetStartPoint (span:VirtualSnapshotSpan) = span.Start

    /// Get the end point
    let GetEndPoint (span:VirtualSnapshotSpan) = span.End

    /// Get a virtual snapshot span from a snapshot span
    let OfSpan (span: SnapshotSpan) =
        let startPoint = VirtualSnapshotPointUtil.OfPoint span.Start
        let endPoint = VirtualSnapshotPointUtil.OfPoint span.End
        VirtualSnapshotSpan(startPoint, endPoint)

/// Contains operations to make it easier to use SnapshotLineRange from a type inference
/// context
module SnapshotLineRangeUtil = 

    /// Create a range for the entire ItextSnapshot
    let CreateForSnapshot (snapshot: ITextSnapshot) = 
        SnapshotLineRange.CreateForExtent snapshot

    /// Create a range for the provided ITextSnapshotLine
    let CreateForLine (line: ITextSnapshotLine) =
        SnapshotLineRange.CreateForLine line

    /// Create a range for the provided ITextSnapshotLine and with count length
    let CreateForLineAndCount (line:ITextSnapshotLine) count = 
        let snapshot = line.Snapshot
        if count < 0 || line.LineNumber + (count-1) >= snapshot.LineCount then None
        else SnapshotLineRange(snapshot, line.LineNumber, count) |> Some

    /// Create a range for the provided ITextSnapshotLine and with at most count 
    /// length.  If count pushes the range past the end of the buffer then the 
    /// span will go to the end of the buffer
    let CreateForLineAndMaxCount (line:ITextSnapshotLine) count = 
        SnapshotLineRange.CreateForLineAndMaxCount line count

    /// Create a line range which covers the start and end line of the provided span
    let CreateForSpan span = 
        let startLine, lastLine = SnapshotSpanUtil.GetStartAndLastLine span
        let count = (lastLine.LineNumber - startLine.LineNumber) + 1
        SnapshotLineRange(span.Snapshot, startLine.LineNumber, count)

    /// Create a line range for the combined span 
    let CreateForNormalizedSnapshotSpanCollection col = 
        col |> NormalizedSnapshotSpanCollectionUtil.GetOverarchingSpan |> CreateForSpan

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
    let CreateForLineRange (startLine: ITextSnapshotLine) (endLine: ITextSnapshotLine) = 
        SnapshotLineRange.CreateForLineRange startLine endLine

    /// Create a line range for the provided start and end line 
    let CreateForLineNumberRange (snapshot:ITextSnapshot) startNumber lastNumber = 
        let startLine = snapshot.GetLineFromLineNumber(startNumber)
        let lastLine = snapshot.GetLineFromLineNumber(lastNumber)
        CreateForLineRange startLine lastLine

module BufferGraphUtil = 

    /// Map the point up to the given ITextSnapshot.  Returns None if the mapping is not 
    /// possible
    let MapPointUpToSnapshot (bufferGraph: IBufferGraph) point snapshot trackingMode affinity =
        try
            bufferGraph.MapUpToSnapshot(point, trackingMode, affinity, snapshot)
            |> OptionUtil.ofNullable
        with
            | :? System.ArgumentException-> None
            | :? System.InvalidOperationException -> None

    /// Map the point up to the given ITextSnapshot.  Returns None if the mapping is not 
    /// possible
    let MapPointUpToSnapshotStandard (bufferGraph: IBufferGraph) point snapshot =
        MapPointUpToSnapshot bufferGraph point snapshot PointTrackingMode.Negative PositionAffinity.Predecessor

    /// Map the point down to the given ITextSnapshot.  Returns None if the mapping is not 
    /// possible
    let MapPointDownToSnapshot (bufferGraph: IBufferGraph) point snapshot trackingMode affinity =
        try
            bufferGraph.MapDownToSnapshot(point, trackingMode, snapshot, affinity)
            |> OptionUtil.ofNullable
        with
            | :? System.ArgumentException-> None
            | :? System.InvalidOperationException -> None

    /// Map the point down to the given ITextSnapshot.  Returns None if the mapping is not 
    /// possible
    let MapPointDownToSnapshotStandard (bufferGraph: IBufferGraph) point snapshot =
        MapPointDownToSnapshot bufferGraph point snapshot PointTrackingMode.Negative PositionAffinity.Predecessor

    /// Map the SnapshotSpan up to the given ITextSnapshot.  Returns None if the mapping is
    /// not possible
    let MapSpanUpToSnapshot (bufferGraph: IBufferGraph) span trackingMode snapshot =
        try
            bufferGraph.MapUpToSnapshot(span, trackingMode, snapshot) |> Some
        with
            | :? System.ArgumentException-> None
            | :? System.InvalidOperationException -> None

    /// Map the SnapshotSpan down to the given ITextSnapshot.  Returns None if the mapping is
    /// not possible
    let MapSpanDownToSnapshot (bufferGraph: IBufferGraph) span trackingMode snapshot =
        try
            bufferGraph.MapDownToSnapshot(span, trackingMode, snapshot) |> Some
        with
            | :? System.ArgumentException-> None
            | :? System.InvalidOperationException -> None

    /// Map the SnapshotSpan down to the given ITextSnapshot by the Start and End points
    /// instead of by the mapped Spans
    let MapSpanDownToSingle (bufferGraph: IBufferGraph) (span: SnapshotSpan) snapshot = 
        let startPoint = MapPointDownToSnapshot bufferGraph span.Start snapshot PointTrackingMode.Negative PositionAffinity.Predecessor
        let endPoint = MapPointDownToSnapshot bufferGraph span.End snapshot PointTrackingMode.Positive PositionAffinity.Successor
        match startPoint, endPoint with
        | Some startPoint, Some endPoint -> SnapshotSpan(startPoint, endPoint) |> Some
        | None, Some _ -> None
        | Some _, None -> None
        | None, None -> None

/// The common pieces of information about an ITextSnapshot which are used
/// to calculate items like motions
type SnapshotData = {

    /// VirtualSnapshotPoint for the Caret
    CaretVirtualPoint: VirtualSnapshotPoint

    /// ITextSnapshotLine on which the caret resides
    CaretLine: ITextSnapshotLine

    /// The current ITextSnapshot on which this data is based
    CurrentSnapshot: ITextSnapshot
} with

    member x.CaretPoint = x.CaretVirtualPoint.Position
    member x.CaretColumn = SnapshotColumn(x.CaretPoint)

[<System.Flags>]
type MoveCaretFlags =
    | None = 0x0
    | EnsureOnScreen = 0x1
    | ClearSelection = 0x2
    | All = 0xffffffff

module EditorOptionsUtil =

    /// Get the option value if it exists
    let GetOptionValue (opts: IEditorOptions) (key: EditorOptionKey<'a>) =
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

    let SetOptionValue (opts: IEditorOptions) (key: EditorOptionKey<'a>) value =
        opts.SetOptionValue(key, value)

module ProjectionBufferUtil =

    let GetSourceBuffersRecursive (projectionBuffer: IProjectionBuffer) =
        let toVisit = new Queue<IProjectionBuffer>()
        toVisit.Enqueue projectionBuffer

        let found = new HashSet<ITextBuffer>()
        while toVisit.Count > 0 do
            let current = toVisit.Dequeue()
            if found.Add current then
                for sourceBuffer in current.SourceBuffers do 
                    match sourceBuffer with
                    | :? IProjectionBuffer as p -> toVisit.Enqueue p
                    | _ -> found.Add sourceBuffer |> ignore

        found 
        |> Seq.filter (fun x -> match x with | :? IProjectionBuffer -> false | _ -> true)

module TextBufferUtil =
    
    /// Delete the specified span and return the latest ITextSnapshot after the
    /// entire delete operation completes vs. the one which is returned for 
    /// the specific delete operation
    let DeleteAndGetLatest (textBuffer: ITextBuffer) (deleteSpan: Span) = 
        textBuffer.Delete(deleteSpan) |> ignore
        textBuffer.CurrentSnapshot

    /// Any ITextBuffer instance is possibly an IProjectionBuffer (which is a text buffer composed 
    /// of parts of other ITextBuffers).  This will return all of the real ITextBuffer buffers 
    /// composing the provided ITextBuffer
    let GetSourceBuffersRecursive (textBuffer: ITextBuffer) =
        match textBuffer with
        | :? IProjectionBuffer as p -> ProjectionBufferUtil.GetSourceBuffersRecursive p
        | _ -> Seq.singleton textBuffer

module TextEditUtil = 

    /// Apply the change and return the latest ITextSnapshot after the edit
    /// operation completes vs. the one which is returned for this specific
    /// edit operation.
    let ApplyAndGetLatest (textEdit: ITextEdit) = 
        textEdit.Apply() |> ignore
        textEdit.Snapshot.TextBuffer.CurrentSnapshot

/// Contains operations to help fudge the Editor APIs to be more F# friendly.  Does not
/// include any Vim specific logic
module TextViewUtil =

    let GetSnapshot (textView: ITextView) = textView.TextSnapshot

    let GetCaret (textView: ITextView) = textView.Caret

    let GetCaretPoint (textView: ITextView) = textView.Caret.Position.BufferPosition

    let GetCaretVirtualPoint (textView: ITextView) = textView.Caret.Position.VirtualBufferPosition

    let GetCaretLine textView = GetCaretPoint textView |> SnapshotPointUtil.GetContainingLine

    let GetCaretLineIndent textView = textView |> GetCaretLine |> SnapshotLineUtil.GetIndentPoint

    let GetCaretLineRange textView count = 
        let line = GetCaretLine textView
        SnapshotLineRangeUtil.CreateForLineAndMaxCount line count

    let GetCaretPointAndLine textView = (GetCaretPoint textView),(GetCaretLine textView)

    /// Get the set of ITextViewLines for the ITextView
    ///
    /// Be aware when using GetTextViewLineContainingYCoordinate, may need to add the
    /// _textView.ViewportTop to the y coordinate
    let GetTextViewLines (textView: ITextView) =
        if textView.IsClosed || textView.InLayout then

            // TextViewLines can throw if the view is being laid out.  Highly unlikely we'd hit
            // that inside of Vim but need to be careful
            None
        else 
            try
                let textViewLines = textView.TextViewLines
                if textViewLines <> null && textViewLines.IsValid then
                    Some textViewLines
                else
                    None
            with 
            | _ -> None

    /// Get the count of Visible lines in the ITextView
    let GetVisibleLineCount textView = 
        match GetTextViewLines textView with
        | None -> 50
        | Some textViewLines -> textViewLines.Count

    /// Return the overarching SnapshotLineRange for the visible lines in the ITextView
    let GetVisibleSnapshotLineRange (textView: ITextView) =

        // Get the first line not clipped or wrapped from above.
        let getFirstFullyVisibleLine (textViewLines: ITextViewLineCollection) =
            let line =
                textViewLines
                |> Seq.where (fun textViewLine ->
                    textViewLine.VisibilityState = Formatting.VisibilityState.FullyVisible &&
                        textViewLine.Start = textViewLine.Start.GetContainingLine().Start)
                |> Seq.tryHead
            match line with
            | None -> textViewLines.FirstVisibleLine
            | Some line -> line

        // Get the last line not clipped or wrapped to below.
        let getLastFullyVisibleLine (textViewLines: ITextViewLineCollection) =
            let line =
                textViewLines
                |> Seq.rev
                |> Seq.where (fun textViewLine ->
                    textViewLine.VisibilityState = Formatting.VisibilityState.FullyVisible &&
                        textViewLine.End = textViewLine.End.GetContainingLine().End)
                |> Seq.tryHead
            match line with
            | None -> textViewLines.LastVisibleLine
            | Some line -> line

        match GetTextViewLines textView with
        | None -> None
        | Some textViewLines ->
            let startLine = (getFirstFullyVisibleLine textViewLines).Start.GetContainingLine().LineNumber
            let lastLine = (getLastFullyVisibleLine textViewLines).End.GetContainingLine().LineNumber
            SnapshotLineRange.CreateForLineNumberRange textView.TextSnapshot startLine lastLine |> NullableUtil.ToOption

    /// Returns a sequence of ITextSnapshotLine values representing the visible lines in the buffer
    let GetVisibleSnapshotLines (textView: ITextView) =
        match GetVisibleSnapshotLineRange textView with
        | Some lineRange -> lineRange.Lines
        | None -> Seq.empty

    /// Returns the overarching SnapshotLineRange for the visible lines in the ITextView on the
    /// Visual snapshot.
    let GetVisibleVisualSnapshotLineRange (textView: ITextView) = 
        match GetVisibleSnapshotLineRange textView with
        | None -> NullableUtil.CreateNull<SnapshotLineRange>()
        | Some range ->
            match BufferGraphUtil.MapSpanUpToSnapshot textView.BufferGraph range.ExtentIncludingLineBreak SpanTrackingMode.EdgeInclusive textView.TextViewModel.VisualBuffer.CurrentSnapshot with
            | Some collection -> 
                collection 
                |> NormalizedSnapshotSpanCollectionUtil.GetOverarchingSpan 
                |> SnapshotLineRange.CreateForSpan
                |> NullableUtil.Create
            | None -> NullableUtil.CreateNull<SnapshotLineRange>()

    /// Returns a sequence of ITextSnapshotLine values representing the visible lines in the buffer
    /// on the Visual snapshot
    let GetVisibleVisualSnapshotLines (textView: ITextView) =
        match GetVisibleVisualSnapshotLineRange textView with
        | NullableUtil.HasValue lineRange -> lineRange.Lines
        | NullableUtil.Null -> Seq.empty

    /// Ensure the caret is currently on the visible screen
    let EnsureCaretOnScreen textView = 
        let caret = GetCaret textView
        caret.EnsureVisible()

    /// Check whether and how we should scroll to put a point on-screen
    let CheckScrollToPoint (textView : ITextView) point =

        // Emulate how vim scrolls when moving off-screen.
        let doScrollToPoint (textViewLines: ITextViewLineCollection) =

            let pointLine = SnapshotPointUtil.GetContainingLine point
            let pointLineNumber = pointLine.LineNumber
            let firstVisibleLine = SnapshotPointUtil.GetContainingLine textViewLines.FirstVisibleLine.Start
            let firstLineNumber = firstVisibleLine.LineNumber
            let lastVisibleLine = SnapshotPointUtil.GetContainingLine textViewLines.LastVisibleLine.Start
            let lastLineNumber = lastVisibleLine.LineNumber
            let endLine = SnapshotUtil.GetLastNormalizedLine textView.TextSnapshot
            let endLineNumber = endLine.LineNumber
            let scrollLimit = int(ceil(textView.ViewportHeight / textView.LineHeight / 2.0))
            if pointLineNumber >= firstLineNumber - scrollLimit && pointLineNumber <= firstLineNumber then

                // The point prececedes the top of the screen by
                // less than half a screen. Scroll up.
                let relativeTo = Editor.ViewRelativePosition.Top
                textView.DisplayTextLineContainingBufferPosition(point, 0.0, relativeTo) |> ignore
            elif pointLineNumber >= lastLineNumber && pointLineNumber <= lastLineNumber + scrollLimit then

                // The point follows the bottom of the screen by
                // less than half a screen. Scroll down.
                let relativeTo = Editor.ViewRelativePosition.Bottom
                textView.DisplayTextLineContainingBufferPosition(point, 0.0, relativeTo) |> ignore
            elif pointLineNumber >= endLineNumber - scrollLimit && pointLineNumber <= endLineNumber then

                // The point is less than half a screen from the
                // bottom of the file. Scroll the bottom of the
                // file to the bottom of the screen.
                let relativeTo = Editor.ViewRelativePosition.Bottom
                textView.DisplayTextLineContainingBufferPosition(endLine.End, 0.0, relativeTo) |> ignore
            else

                // Otherwise, position point in the middle of the screen.
                let span = pointLine.ExtentIncludingLineBreak
                let option = Editor.EnsureSpanVisibleOptions.AlwaysCenter
                textView.ViewScroller.EnsureSpanVisible(span, option) |> ignore

        // Be careful because using ITextViewLines can result in an exception.
        try
            let textViewLine = textView.GetTextViewLineContainingBufferPosition point
            let onScreen = textViewLine.VisibilityState = Formatting.VisibilityState.FullyVisible
            if not onScreen then

                // The line the point is on is off-screen.
                match GetTextViewLines textView with
                | Some textViewLines -> doScrollToPoint textViewLines
                | _ -> ()
        with
        | _ -> ()

    /// Clear out the selection
    let ClearSelection (textView: ITextView) =
        textView.Selection.Clear()

    let private MoveCaretToCommon textView flags = 
        if Util.IsFlagSet flags MoveCaretFlags.ClearSelection then
            ClearSelection textView

        if Util.IsFlagSet flags MoveCaretFlags.EnsureOnScreen then
            let point = GetCaretPoint textView
            CheckScrollToPoint textView point
            EnsureCaretOnScreen textView

    /// Move the caret to the given point
    let MoveCaretToPointRaw textView (point: SnapshotPoint) flags = 
        let caret = GetCaret textView
        caret.MoveTo(point) |> ignore
        MoveCaretToCommon textView flags

    /// Move the caret to the given point, ensure it is on screen and clear out the previous 
    /// selection.  Will not expand any outlining regions
    let MoveCaretToPoint textView point =
        MoveCaretToPointRaw textView point MoveCaretFlags.All

    /// Move the caret to the given point and ensure it is on screen.  Will not expand any outlining regions
    let MoveCaretToVirtualPointRaw textView (point: VirtualSnapshotPoint) flags = 
        let caret = GetCaret textView
        caret.MoveTo(point) |> ignore
        MoveCaretToCommon textView flags

    /// Move the caret to the given point, ensure it is on screen and clear out the previous 
    /// selection.  Will not expand any outlining regions
    let MoveCaretToVirtualPoint textView point = 
        MoveCaretToVirtualPointRaw textView point MoveCaretFlags.All

    /// Move the caret to the given point and ensure it is on screen.  Will not expand any outlining regions
    let MoveCaretToPositionRaw textView (position: int) flags = 
        let snapshot = GetSnapshot textView
        let point = SnapshotPoint(snapshot, position)
        MoveCaretToPointRaw textView point flags

    /// Move the caret to the given point, ensure it is on screen and clear out the previous 
    /// selection.  Will not expand any outlining regions
    let MoveCaretToPosition textView position = 
        MoveCaretToPositionRaw textView position MoveCaretFlags.All

    /// Get the SnapshotData value for the edit buffer.  Unlike the SnapshotData for the Visual Buffer this 
    /// can always be retrieved because the caret point is presented in terms of the edit buffer
    let GetEditSnapshotData (textView: ITextView) = 
        let caretPoint = GetCaretVirtualPoint textView
        let caretLine = SnapshotPointUtil.GetContainingLine caretPoint.Position
        { 
            CaretVirtualPoint = caretPoint
            CaretLine = caretLine
            CurrentSnapshot = caretLine.Snapshot }

    /// Get the SnapshotData value for the visual buffer.  Can return None if the information is not mappable
    /// to the visual buffer.  Really this shouldn't ever happen unless the IProjectionBuffer was incorrectly
    /// hooked up though
    let GetVisualSnapshotData (textView: ITextView) = 

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
            let editCaretPoint = GetCaretVirtualPoint textView
            match BufferGraphUtil.MapPointUpToSnapshot bufferGraph editCaretPoint.Position visualSnapshot PointTrackingMode.Negative PositionAffinity.Predecessor with
            | Some point ->
                point
                |> VirtualSnapshotPointUtil.OfPoint
                |> VirtualSnapshotPointUtil.AddOnSameLine editCaretPoint.VirtualSpaces
                |> Some
            | None -> None

        match caretPoint with
        | None ->
            // If the caret can't be mapped up to the visual buffer then there is no way to get the 
            // visual SnapshotData information.  This should represent a rather serious issue with the 
            // ITextView though
            None
        | Some caretPoint ->
            let caretLine = SnapshotPointUtil.GetContainingLine caretPoint.Position
            { 
                CaretVirtualPoint = caretPoint
                CaretLine = caretLine
                CurrentSnapshot = caretLine.Snapshot } |> Some

    /// Get the SnapshotData for the visual buffer if available.  If it's not available then fall back
    /// to the edit buffer
    let GetVisualSnapshotDataOrEdit textView = 
        match GetVisualSnapshotData textView with
        | Some snapshotData -> snapshotData
        | None -> GetEditSnapshotData textView

    /// Is word wrap enabled for this ITextView
    let IsWordWrapEnabled (textView: ITextView) = 
        let editorOptions = textView.Options
        match EditorOptionsUtil.GetOptionValue editorOptions DefaultTextViewOptions.WordWrapStyleId with
        | None -> false
        | Some wordWrapStyle -> Util.IsFlagSet wordWrapStyle WordWrapStyles.WordWrap

    /// Insert final newline
    let InsertFinalNewLine (textView: ITextView) =
        let snapshot = textView.TextSnapshot
        let allLinesHaveLineBreaks = SnapshotUtil.AllLinesHaveLineBreaks snapshot
        if not allLinesHaveLineBreaks then
            let textBuffer = textView.TextBuffer
            let endPoint = SnapshotUtil.GetEndPoint snapshot
            let newLine = DefaultOptionExtensions.GetNewLineCharacter textView.Options
            textBuffer.Insert(endPoint.Position, newLine) |> ignore

    /// Remove final newline
    let RemoveFinalNewLine (textView: ITextView) =
        let snapshot = textView.TextSnapshot
        let allLinesHaveLineBreaks = SnapshotUtil.AllLinesHaveLineBreaks snapshot
        if allLinesHaveLineBreaks then
            let textBuffer = textView.TextBuffer
            let lastLine = SnapshotUtil.GetLastNormalizedLine snapshot
            let span = SnapshotSpan(lastLine.End, lastLine.EndIncludingLineBreak)
            textBuffer.Delete(span.Span) |> ignore

module TextSelectionUtil = 

    /// Returns the SnapshotSpan which represents the total of the selection.  This is a SnapshotSpan of the left
    /// most and right most point point in any of the selected spans 
    let GetOverarchingSelectedSpan (selection: ITextSelection) = 
        if selection.IsEmpty then 
            None
        else
            match NonEmptyCollectionUtil.OfSeq selection.SelectedSpans with
            | None -> None
            | Some col -> SnapshotSpanUtil.GetOverarchingSpan col |> Some

    /// Gets the selection of the editor
    let GetStreamSelectionSpan (selection:ITextSelection) = selection.StreamSelectionSpan

module TrackingPointUtil =

    let GetPoint (snapshot: ITextSnapshot) (point: ITrackingPoint) =
        try
            point.GetPoint(snapshot) |> Some
        with
            | :? System.ArgumentException -> None

    let GetPointInSnapshot point mode (newSnapshot: ITextSnapshot) =
        let oldSnapshot = SnapshotPointUtil.GetSnapshot point
        if oldSnapshot.Version.VersionNumber = newSnapshot.Version.VersionNumber then
            Some point
        else
            let trackingPoint = oldSnapshot.CreateTrackingPoint(point.Position, mode)
            GetPoint newSnapshot trackingPoint

    let GetVirtualPointInSnapshot (point: VirtualSnapshotPoint) mode (newSnapshot: ITextSnapshot) =
        let oldSnapshot = SnapshotPointUtil.GetSnapshot point.Position
        if oldSnapshot.Version.VersionNumber = newSnapshot.Version.VersionNumber then
            Some point
        else
            let trackingPoint = oldSnapshot.CreateTrackingPoint(point.Position.Position, mode)
            match GetPoint newSnapshot trackingPoint with
            | Some newPoint ->
                let virtualSpaces = point.VirtualSpaces
                newPoint
                |> VirtualSnapshotPointUtil.OfPoint
                |> VirtualSnapshotPointUtil.AddOnSameLine virtualSpaces
                |> Some
            | None ->
                None

/// Abstraction useful for APIs which need to work over a single SnapshotSpan 
/// or collection of SnapshotSpan values
[<RequireQualifiedAccess>]
type EditSpan = 
    /// Common case of an edit operation which occurs over a single SnapshotSpan
    | Single of SnapshotSpan 

    /// Occurs during block edits
    | Block of NonEmptyCollection<SnapshotOverlapSpan>

    with

    /// View the data as a collection.  For Single values this just creates a
    /// collection with a single element
    member x.Spans =
        match x with
        | Single span -> NonEmptyCollection(span, List.empty) 
        | Block col -> col |> NonEmptyCollectionUtil.Map (fun span -> span.OverarchingSpan)

    /// View the data as a collection of overlap spans.  For Single values this just creates a
    /// collection with a single element
    member x.OverlapSpans =
        match x with
        | Single span -> 
            let span = SnapshotOverlapSpan(span) 
            NonEmptyCollection(span, List.empty) 
        | Block col -> col

    /// Returns the overarching span of the entire EditSpan value.  For Single values
    /// this is a 1-1 mapping.  For Block values it will take the min start position
    /// and combine it with the maximum end position
    member x.OverarchingSpan =
        match x with 
        | Single span -> span
        | Block col -> col |> NonEmptyCollectionUtil.Map (fun span -> span.OverarchingSpan) |> SnapshotSpanUtil.GetOverarchingSpan 

    /// Provide an implicit conversion from SnapshotSpan.  Useful from C# code
    static member op_Implicit span = EditSpan.Single span

    /// Provide an implicit conversion from NormalizedSnapshotSpan.  Useful from C# code
    static member op_Implicit block = EditSpan.Block block

module EditUtil = 

    /// NewLine to use for the ITextBuffer
    let NewLine (options: IEditorOptions) = DefaultOptionExtensions.GetNewLineCharacter options

    /// Get the text for a tab character based on the given options
    let GetTabText (options: IEditorOptions) = 
        if DefaultOptionExtensions.IsConvertTabsToSpacesEnabled options then
            StringUtil.RepeatChar (DefaultOptionExtensions.GetTabSize options) ' '
        else
            "\t"

    /// Get the length of the line break at the given index 
    let GetLineBreakLength (str: string) index =
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
    let GetLineBreakLengthAtEnd (str: string) =
        if System.String.IsNullOrEmpty str then 
            0
        else
            let index = str.Length - 1
            if str.Length > 1 && str.Chars(index - 1) = '\r' && str.Chars(index) = '\n' then
                2
            else
                GetLineBreakLength str index

    /// Get the count of new lines in the string
    let GetLineBreakCount (str: string) =
        let rec inner index count =
            if index >= str.Length then
                count
            else
                let length = GetLineBreakLength str index 
                if length > 0 then
                    inner (index + length) (count + 1)
                else
                    inner (index + 1) count

        inner 0 0

    /// Get the indentation level given the context line (the line above the line which is 
    /// being indented)
    let GetAutoIndent (contextLine: ITextSnapshotLine) =
        contextLine 
        |> SnapshotLineUtil.GetIndentPoint 
        |> SnapshotPointUtil.GetColumn 

    /// Does the specified string end with a valid newline string 
    let EndsWithNewLine value = 0 <> GetLineBreakLengthAtEnd value

    /// Does this text have a new line character inside of it?
    let HasNewLine (text: string) = 
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

    /// Normalize the new line values in the string to the specified value
    let NormalizeNewLines (text: string) (newLine: string) = 
        let builder = System.Text.StringBuilder()
        let rec inner index = 
            if index >= text.Length then
                builder.ToString()
            else
                let length = GetLineBreakLength text index
                if 0 = length then
                    builder.AppendChar text.[index]
                    inner (index + 1)
                else
                    builder.AppendString newLine
                    inner (index + length)
        inner 0

    /// Split a string into lines of text
    let SplitLines (text: string) =
        let text = RemoveEndingNewLine text
        let text = NormalizeNewLines text "\n"
        StringUtil.Split '\n' text

/// In some cases we need to break a complete string into a series of text representations
/// and new lines.  It's easiest to view this as a sequence of text values with their 
/// associated line breaks
type TextLine = {

    /// The text of the line
    Text: string

    /// The string for the new line 
    NewLine: string

} with

    member x.HasNewLine = x.NewLine.Length = 0

    /// Create a string back from the provided TextLine values
    static member CreateString (textLines: TextLine seq) = 
        let builder = System.Text.StringBuilder()
        for textLine in textLines do
            builder.AppendString textLine.Text
            builder.AppendString textLine.NewLine
        builder.ToString()

    /// Break a string representation into a series of TextNode values.  This will 
    /// always return at least a single value for even an empty string so we use 
    /// a NonEmptyCollection
    static member GetTextLines (fullText: string) = 

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
            let rest: TextLine list = Seq.unfold getForIndex index |> List.ofSeq

            NonEmptyCollection(firstLine, rest)

module SnapshotColumnUtil =

    let GetPoint (column: SnapshotColumn) = column.Point

    let GetLine (column: SnapshotColumn) = column.Line
    
    /// Get the columns from the given point in a forward motion 
    let private GetColumnsCore path includeLineBreak point = 
        let snapshot = SnapshotPointUtil.GetSnapshot point
        let startLineNumber = SnapshotPointUtil.GetLineNumber point
        let filter = 
            match path with 
            | SearchPath.Forward -> fun (c: SnapshotColumn) -> c.Point.Position >= point.Position
            | SearchPath.Backward -> fun (c: SnapshotColumn) -> c.Point.Position <= point.Position

        SnapshotUtil.GetLines snapshot startLineNumber path
        |> Seq.collect (fun line -> 
            if includeLineBreak then
                SnapshotLineUtil.GetColumnsIncludingLineBreak path line
            else
                SnapshotLineUtil.GetColumns path line)
        |> Seq.filter filter

    let GetColumns path point = GetColumnsCore path false point

    let GetColumnsIncludingLineBreak path point = GetColumnsCore path true point

    let CreateFromPoint point = SnapshotColumn(point)

module internal ITextEditExtensions =

    type ITextEdit with

        /// Delete the overlapped span from the ITextBuffer.  If there is any overlap then the
        /// remaining spaces will be filed with ' ' 
        member x.Delete (overlapSpan: SnapshotOverlapSpan) = 
            let pre = overlapSpan.Start.SpacesBefore
            let post = 
                if overlapSpan.HasOverlapEnd then
                    overlapSpan.End.SpacesAfter + 1
                else
                    0

            let span = overlapSpan.OverarchingSpan
            match pre + post with
            | 0 -> x.Delete(span.Span) 
            | _ -> x.Replace(span.Span, String.replicate (pre + post) " ") 


