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
open System.Drawing
open System.Globalization
open Microsoft.VisualStudio.Text.Formatting

[<RequireQualifiedAccess>]
type CodePointInfo = 
    | SimpleCharacter
    | SurrogatePairHighCharacter
    | SurrogatePairLowCharacter
    | BrokenSurrogatePair
    | EndPoint

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

    // Get the code point information for a given point in the ITextSnapshot
    let GetCodePointInfo (point: SnapshotPoint) = 
        if IsEndPoint point then
            CodePointInfo.EndPoint
        else
            let c = point.GetChar()
            if CharUtil.IsHighSurrogate c then
                let nextPoint = point.Add(1)
                if not (IsEndPoint nextPoint) && CharUtil.IsLowSurrogate (nextPoint.GetChar()) then CodePointInfo.SurrogatePairHighCharacter
                else CodePointInfo.BrokenSurrogatePair
            elif CharUtil.IsLowSurrogate c then
                if point.Position = 0 then 
                    CodePointInfo.BrokenSurrogatePair
                else
                    let previousPoint = point.Subtract(1)
                    if CharUtil.IsHighSurrogate (previousPoint.GetChar()) then CodePointInfo.SurrogatePairLowCharacter
                    else CodePointInfo.BrokenSurrogatePair
            else
                CodePointInfo.SimpleCharacter

    let IsInsideLineBreak (point: SnapshotPoint) (line: ITextSnapshotLine) = 
        point.Position >= line.End.Position && not (IsEndPoint point)

    /// Get the span of the character which is pointed to by the point.  Normally this is a 
    /// trivial operation.  The only difficulty is if the point exists at the end of a line
    /// (in which case the span covers the linebreak) or if the character is part of a
    /// surrogate pair.
    let GetCharacterSpan (line: ITextSnapshotLine) (point: SnapshotPoint) = 

        // We require that the point belong to the line.
        Contract.Requires(point.Snapshot = line.Snapshot)
        Contract.Requires(point.Position >= line.Start.Position)
        Contract.Requires(point.Position <= line.EndIncludingLineBreak.Position)

        if IsInsideLineBreak point line then
            new SnapshotSpan(line.End, line.EndIncludingLineBreak)
        else
            match GetCodePointInfo point with
            | CodePointInfo.EndPoint -> SnapshotSpan(point, 0)
            | CodePointInfo.SurrogatePairHighCharacter -> SnapshotSpan(point, 2)
            | CodePointInfo.SurrogatePairLowCharacter -> SnapshotSpan((point.Subtract(1)), 2)
            | _ -> SnapshotSpan(point, 1)

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

/// Conceptually this references a single CodePoint in the snapshot. This can be
/// either:
/// - a normal character
/// - a UTF32 character (represented by two UTF16 characters called the "surrogate pair")
/// - either a high or low surrogate char that doesn't have a proper matching pair.
///
/// In the case this refers to a surrogate pair then the Point will refer to the high
/// surrogate.
///
/// This is similar in structure to SnapshotColumn but a bit more low level. In
/// general code should prefer SnapshotColumn as it deals with legal caret positions
/// and characters. This type is used when a more direct mapping between positions and 
/// code point is needed
[<Struct>]
[<CustomEquality>]
[<NoComparison>]
type SnapshotCodePoint =

    val private _line: ITextSnapshotLine
    val private _offset: int

    /// This is the CodePointInfo for the position in the ITextSnapshot
    val private _codePointInfo: CodePointInfo

    /// Create a SnapshotCodePoint for the given Point. In the case this points to a 
    /// surrogate pair then the instance will point to the high surrogate instance.
    new (point: SnapshotPoint) =
        let line = point.GetContainingLine();
        SnapshotCodePoint(line, point)

    new (line: ITextSnapshotLine) =
        SnapshotCodePoint(line, line.Start)

    new (line: ITextSnapshotLine, offset: int) =
        let point = line.Start.Add(offset)
        SnapshotCodePoint(line, point)

    new (line: ITextSnapshotLine, point: SnapshotPoint) =

        let info = EditorCoreUtil.GetCodePointInfo point
        if info = CodePointInfo.SurrogatePairLowCharacter then
            let point = point.Subtract(1) 
            { _line = line; _offset = point.Position - line.Start.Position; _codePointInfo = CodePointInfo.SurrogatePairHighCharacter }
        else
            { _line = line; _offset = point.Position - line.Start.Position; _codePointInfo = info }

    /// The snapshot line containing the column
    member x.Line = x._line

    /// The offset into the Line that this SnapshotCodePoint occupies
    member x.Offset = x._offset;

    /// The number of positions in the ITextSnapshot this character occupies.
    member x.Length = 
        match x._codePointInfo with
        | CodePointInfo.SurrogatePairHighCharacter -> 2
        | CodePointInfo.EndPoint -> 0
        | _ -> 1

    /// The snapshot corresponding to the column
    member x.Snapshot = x._line.Snapshot

    /// The Point which represents the begining of this character.
    member x.StartPoint = x._line.Start.Add(x._offset)

    /// The Point which follows this CodePoint.
    member x.EndPoint = x.StartPoint.Add(x.Length)

    /// The character text for code point.
    member x.CharacterText = 
        let c = x.StartPoint.GetChar()
        if x.Length = 1 then 
            sprintf "%c" c
        else
            // TODO: way to convert an int codepoint into a string
            let low = x.StartPoint.Add(1).GetChar()
            sprintf "%c%c" c low

    member x.UnicodeCategory = 
        let c = x.StartPoint.GetChar()
        if x.Length = 1 then 
            CharUnicodeInfo.GetUnicodeCategory c
        else
            let str = x.CharacterText
            CharUnicodeInfo.GetUnicodeCategory(str, 0)

    member x.Span = 
        let length = 
            if x._codePointInfo = CodePointInfo.SurrogatePairHighCharacter then 2
            else 1
        SnapshotSpan(x.StartPoint, length)

    member x.CodePointInfo = x._codePointInfo

    /// Returns the code point which represents this character. In the case of a broken surrogate pair
    /// this will return the raw broken value.
    member x.CodePoint = 
        match x._codePointInfo with
        | CodePointInfo.SurrogatePairHighCharacter -> 
            let highChar = x.StartPoint.GetChar()
            let lowChar = x.StartPoint.Add(1).GetChar()
            CharUtil.ConvertToCodePoint highChar lowChar
        | _ -> int (x.StartPoint.GetChar())

    /// Returns the code point which represents this character. In the case of a broken surrogate pair
    /// this will return the raw broken value.
    member x.CodePointText = 
        let codePoint = x.CodePoint
        match x._codePointInfo with
        | CodePointInfo.SurrogatePairHighCharacter -> sprintf "U%8X" codePoint
        | _ -> sprintf "u%4X" codePoint

    /// The position or text buffer offset of the column
    member x.StartPosition = x.StartPoint.Position

    member x.EndPosition = x.EndPoint.Position

    member x.IsStartPoint = x.StartPosition = 0

    member x.IsEndPoint = EditorCoreUtil.IsEndPoint x.StartPoint

    member x.IsStartOfLine = x._line.Start.Position = x.StartPosition

    member x.IsInsideLineBreak = EditorCoreUtil.IsInsideLineBreak x.StartPoint x._line

    member x.IsInsideLineBreakOrEnd = x.IsEndPoint || x.IsInsideLineBreak

    member x.IsSurrogatePair = x.CodePointInfo = CodePointInfo.SurrogatePairHighCharacter

    /// Is the unicode character at this point represented by the specified value? This will only match
    /// for characters in the BMP plane.
    member x.IsCharacter(c: char) = 
        if x.IsEndPoint then false
        else x.Length = 1 && x.StartPoint.GetChar() = c

    /// Is the codepoint represented by this string value?
    member x.IsCharacter(text: string) = x.CharacterText = text

    member x.IsCharacter(func: char -> bool) =
        if x.IsEndPoint then false
        else x.Length = 1 && func (x.StartPoint.GetChar())

    member x.IsBlank = x.IsCharacter CharUtil.IsBlank

    member x.IsWhiteSpace = x.IsCharacter CharUtil.IsWhiteSpace

    member x.Contains (point: SnapshotPoint) = 
        if EditorCoreUtil.IsEndPoint point then 
            x.IsEndPoint
        else
            let position = point.Position
            position >= x.StartPosition && position < x.EndPosition

    /// Move forward by the specified number of CodePoint values
    member x.Add count =
        if count = 0 then 
            x
        elif count < 0 then
            x.Subtract (-count)
        else
            let mutable count = count
            let mutable current = x.StartPoint
            while count > 0 do
                current <-
                    match EditorCoreUtil.GetCodePointInfo current with
                    | CodePointInfo.SurrogatePairHighCharacter -> current.Add(2)
                    | _ -> current.Add(1)
                count <- count - 1

            let line = 
                if current.Position < x.Line.EndIncludingLineBreak.Position then x.Line
                else current.GetContainingLine()
            SnapshotCodePoint(line, current)

    /// Go backward (positive) or forward (negative) by the specified number of
    /// CodePoint values
    member x.Subtract count =
        if count = 0 then 
            x
        elif count < 0 then
            x.Add (-count)
        else
            let mutable count = count
            let mutable current = x.StartPoint
            while count > 0 do
                let previous = current.Subtract(1)
                current <-
                    match EditorCoreUtil.GetCodePointInfo previous with
                    | CodePointInfo.SurrogatePairLowCharacter -> previous.Subtract(1)
                    | _ -> previous
                count <- count - 1

            let line = 
                if current.Position >= x.Line.Start.Position then x.Line
                else current.GetContainingLine()
            SnapshotCodePoint(line, current)

    /// Get the number of spaces this point occupies on the screen.
    member x.GetSpaces tabStop = 

        // Determines whether the given character occupies space on screen when displayed.
        // For instance, combining diacritics occupy the space of the previous character,
        // while control characters are simply not displayed.
        // based on http://www.cl.cam.ac.uk/~mgk25/ucs/wcwidth.c
        let isNonSpacingCategory category =
            match category with
            //Visual studio does not render control characters
            | System.Globalization.UnicodeCategory.Control
            | System.Globalization.UnicodeCategory.NonSpacingMark
            | System.Globalization.UnicodeCategory.Format
            | System.Globalization.UnicodeCategory.EnclosingMark ->
                /// Contrarily to http://www.cl.cam.ac.uk/~mgk25/ucs/wcwidth.c
                /// the Soft hyphen (\u00ad) is invisible in VS.
                true
            | _ -> false

        // based on http://www.cl.cam.ac.uk/~mgk25/ucs/wcwidth.c
        let isNonSpacingBmp (c: char) =
            if ((c = '\u200b') || ('\u1160' <= c && c <= '\u11ff')) then
                true
            else
                let category = CharUnicodeInfo.GetUnicodeCategory c
                isNonSpacingCategory category

        if x.IsEndPoint then
            0
        elif x.Length = 1 then
            match x.StartPoint.GetChar() with
            | '\u0000' -> 1
            | '\t' -> tabStop
            | c when isNonSpacingBmp c -> 0
            | c when UnicodeUtil.IsWideBmp (int c) -> 2
            | _ -> 1
        else
            if UnicodeUtil.IsWideAstral x.CodePoint then 2
            else if isNonSpacingCategory x.UnicodeCategory then 0 
            else 1

    /// Get the text corresponding to the column
    member x.GetText () = x.Span.GetText()

    member x.Equals(other : SnapshotCodePoint) =
        x.Snapshot = other.Snapshot &&
        x.Line.LineNumber = other.Line.LineNumber &&
        x.Offset = other.Offset

    /// Debugger display
    override x.ToString() =
        sprintf "CodePoint: %s Text: %s Line: %d Offset: %d" (x.CodePointText) (x.GetText()) (x.Line.LineNumber) (x.Offset)

    override x.GetHashCode() = 
        HashUtil.Combine2 x.Line.LineNumber x.Offset

    static member op_Equality(this, other) = System.Collections.Generic.EqualityComparer<SnapshotCodePoint>.Default.Equals(this, other)
    static member op_Inequality(this, other) = not (System.Collections.Generic.EqualityComparer<SnapshotCodePoint>.Default.Equals(this, other))

    override x.Equals(other: obj) = 
        match other with
        | :? SnapshotCodePoint as codePoint -> x.Equals(codePoint)
        | _ -> false

    interface IEquatable<SnapshotCodePoint> with
        member x.Equals(other: SnapshotCodePoint) = x.Equals(other)

/// Conceptually, a character span corresponds to a single logical character:
/// - a normal character
/// - a line break (which may consist of one or more physical characters)
/// - a UTF32 character (represented by two UTF16 characters called the "surrogate pair")
/// Alternatively, a character span represents the places where it is valid to set the caret.
[<Struct>]
[<StructuralEquality>]
[<NoComparison>]
type SnapshotColumn =

    val private _codePoint : SnapshotCodePoint
    val private _columnNumber: int

    /// Constructor for a character span corresponding to a point
    new (point: SnapshotPoint) =
        let line = point.GetContainingLine();
        SnapshotColumn(line, point)

    /// Constructor for a character span corresponding to a point within a line
    /// that looks up the column number by searching the positions array
    new (line: ITextSnapshotLine, point: SnapshotPoint) =
        let mutable codePoint = SnapshotCodePoint(line.Start)
        let mutable columnNumber = 0 
        let mutable isDone = false
        while not isDone do
            if codePoint.Contains point then
                isDone <- true
            elif codePoint.StartPosition = line.End.Position && EditorCoreUtil.IsInsideLineBreak point line then
                isDone <- true
            elif codePoint.StartPosition >= line.End.Position then
                invalidArg "point" "The value point is not contained on the specified line"
            else
                codePoint <- codePoint.Add 1
                columnNumber <- columnNumber  + 1

        { _codePoint = codePoint; _columnNumber = columnNumber }

    /// Constructor for a character span corresponding to the first
    /// column of a line that "parses" the line and builds a lookup
    /// table of physical positions corresponding to logical columns
    new (line: ITextSnapshotLine) =
        { _codePoint = SnapshotCodePoint(line); _columnNumber = 0 }

    private new (codePoint: SnapshotCodePoint, columnNumber: int) = 
        { _codePoint = codePoint; _columnNumber = columnNumber }

    /// Returns the SnapshotCodePoint that corresponds to this column. In the case the 
    /// column is inside the line break this will refer to the first character inside
    /// the line break text. 
    member x.CodePoint = x._codePoint

    /// The snapshot line containing the column
    member x.Snapshot = x._codePoint.Snapshot

    /// The snapshot line containing the column
    member x.Line = x._codePoint.Line

    /// The line number of the line containing the column
    member x.LineNumber = x.Line.LineNumber

    /// Whether the "character" at column is a linebreak
    member x.IsLineBreak = 
        x.CodePoint.StartPoint.Position = x.Line.End.Position &&
        not x.IsEndColumn

    /// The number of positions this occupies in the ITextSnapshot
    member x.Length = 
        if x.IsLineBreak then x.Line.LineBreakLength
        else x.CodePoint.Length

    /// The point where the column begins
    member x.StartPoint = x.CodePoint.StartPoint

    member x.StartPosition = x.StartPoint.Position

    member x.EndPoint = x.StartPoint.Add x.Length

    member x.EndPosition = x.EndPoint.Position

    /// Is this the first column in the buffer
    member x.IsStartColumn = x.CodePoint.IsStartPoint

    /// Is this the end column in the buffer
    member x.IsEndColumn = EditorCoreUtil.IsEndPoint x.StartPoint

    member x.IsLineBreakOrEnd = x.IsLineBreak || x.IsEndColumn

    member x.IsCharacter (c: char) = x.CodePoint.IsCharacter c

    member x.IsCharacter(text: string) = x.CodePoint.IsCharacter text

    /// The column number of the column
    /// (Warning: don't use the column number as a buffer position offset)
    member x.ColumnNumber = x._columnNumber

    /// The position offset of the column relative the beginning of the line
    member x.Offset = x.StartPosition - x.Line.Start.Position

    /// The snapshot span covering the logical character at the column
    member x.Span = new SnapshotSpan(x.Snapshot, x.StartPosition, x.Length)

    /// Whether this is the first column of the line
    member x.IsStartOfLine = x._columnNumber = 0

    /// Go forward (positive) or backward (negative) by the specified number of
    /// columns stopping at the beginning or end of the buffer
    /// (note that linebreaks and surrogate pairs count as a single column)
    member x.Add count =
        let mutable count = count
        let mutable codePoint = x._codePoint
        let mutable column = x._columnNumber
        if count >= 0 then
            while count > 0 do
                if codePoint.IsEndPoint then
                    invalidOp Resources.Common_LocationOutsideBuffer

                if codePoint.IsInsideLineBreak then
                    let nextLine = x.Snapshot.GetLineFromLineNumber (codePoint.Line.LineNumber + 1)
                    column <- 0 
                    codePoint <- SnapshotCodePoint(nextLine)
                else
                    codePoint <- codePoint.Add 1
                    column <- column + 1
                count <- count - 1
            SnapshotColumn(codePoint, column)
        else
            count <- -count
            while count > 0 do
                if codePoint.IsStartPoint then
                    invalidOp Resources.Common_LocationOutsideBuffer

                if codePoint.IsStartOfLine then
                    let previousLine = x.Snapshot.GetLineFromLineNumber (codePoint.Line.LineNumber - 1)
                    let cs = SnapshotColumn(previousLine, previousLine.End)
                    column <- cs._columnNumber
                    codePoint <- cs._codePoint
                else
                    codePoint <- codePoint.Subtract 1
                    column <- column - 1
                count <- count - 1
            SnapshotColumn(codePoint, column)

    member x.AddOrEnd count =
        match x.TryAdd count with
        | Some column -> column
        | None -> SnapshotColumn.GetEndColumn x.Snapshot

    member x.AddOneOrCurrent() =
        match x.TryAdd 1 with
        | Some column -> column
        | None -> x

    /// Go backward (positive) or forward (negative) by the specified number of
    /// columns
    member x.Subtract count =
        x.Add -count

    member x.SubtractOrStart count =
        match x.TrySubtract count with
        | Some column -> column
        | None -> SnapshotColumn.GetStartColumn x.Snapshot

    member x.SubtractOneOrCurrent() = 
        match x.TrySubtract 1 with
        | Some column -> column
        | None -> x

    member private x.TryAddCore count testFunc = 
        let mutable count = count
        let mutable current = x  
        let mutable isGood = true
        if count >= 0 then
            while count > 0 && isGood do
                if current.IsEndColumn then
                    isGood <- false
                else
                    current <- current.Add 1
                    count <- count - 1

                    if not (testFunc current) then
                        isGood <- false
        else
            while count < 0 && isGood do
                if current.IsStartColumn then
                    isGood <- false
                else
                    count <- count + 1
                    current <- current.Add -1

                if not (testFunc current) then
                    isGood <- false
            
        if isGood then Some current
        else None

    member x.TryAdd count = 
        x.TryAddCore count (fun _ -> true)

    member x.TrySubtract count = 
        x.TryAdd -count

    /// Try and add within the same ITextSnapshotLine
    member x.TryAddInLine(count: int, ?includeLineBreak) = 
        let includeLineBreak = defaultArg includeLineBreak false
        let lineNumber = x.LineNumber
        x.TryAddCore count (fun current -> 
            current.LineNumber = lineNumber &&
            (not current.IsLineBreak || includeLineBreak))

    /// Try and add within the same ITextSnapshot
    member x.TryAddInLine(count: int) =
        x.TryAddInLine(count, false)

    /// Try and subract within the same ITextSnapshotLine
    member x.TrySubtractInLine(count: int) =
        let lineNumber = x.LineNumber
        x.TryAddCore -count (fun current -> current.LineNumber = lineNumber)

    member x.AddInLine(count: int, ?includeLineBreak) = 
        let includeLineBreak = defaultArg includeLineBreak false
        match x.TryAddInLine(count, includeLineBreak) with
        | Some column -> column
        | None -> invalidArg "count" (Resources.Common_InvalidColumnCount count)

    /// Add 'count' columns in the current line or return the End column of the line if 'count' goes 
    /// past the end.
    member x.AddInLineOrEnd(count: int) =
        match x.TryAddInLine(count, includeLineBreak = true) with
        | Some column -> column
        | None -> SnapshotColumn.GetLineEnd x.Line

    member x.SubtractInLine(count: int, ?includeLineBreak) = 
        let includeLineBreak = defaultArg includeLineBreak false
        x.AddInLine(-count, includeLineBreak)

    /// Get the text corresponding to the column
    member x.GetText () =
        x.Span.GetText()

    /// Get the number of spaces occupied by this column. There are couple of 
    /// caveats to this function
    ///  1. Line break will return 1 no matter how long line break text is
    ///  2. Tabs will return 'tabStop' no matter whether the tab is on a 
    ///     tab boundary.
    member x.GetSpaces tabStop =
        if x.IsLineBreak then 1 
        elif x.IsEndColumn then 0
        else x.CodePoint.GetSpaces tabStop

    /// This will get the number of spaces occupied by the column. This will accurately
    /// measure tabs as they appear in the context of a line.
    member x.GetSpacesInContext tabStop =
        if x.CodePoint.IsCharacter '\t' then
            let before = x.GetSpacesToColumn tabStop
            let remainder = before % tabStop
            tabStop - remainder
        else x.GetSpaces tabStop

    /// Get the number of spaces before this column on the same line. 
    member x.GetSpacesToColumn tabStop =
        SnapshotColumn.GetSpacesToColumnNumber(x.Line, x.ColumnNumber, tabStop = tabStop)

    /// Get the total number of spaces on the line before and including this 
    /// column. This will count tabs as 'tabStop' no matter where it appears on 
    /// the line.
    member x.GetSpacesIncludingToColumn tabStop =
        if x.IsCharacter '\t' then
            x.Add(1).GetSpacesToColumn tabStop
        else 
            let before = x.GetSpacesToColumn tabStop
            let this = x.GetSpaces tabStop
            before + this

    /// Debugger display
    override x.ToString() =
        sprintf "Point: %s Line: %d Column: %d" (x.CodePoint.ToString()) x.LineNumber x.ColumnNumber

    static member op_Equality(this, other) = System.Collections.Generic.EqualityComparer<SnapshotColumn>.Default.Equals(this, other)
    static member op_Inequality(this, other) = not (System.Collections.Generic.EqualityComparer<SnapshotColumn>.Default.Equals(this, other))

    static member GetForColumnNumber(line: ITextSnapshotLine, columnNumber: int, ?includeLineBreak) =
        let includeLineBreak = defaultArg includeLineBreak false
        let mutable column = SnapshotColumn(line)
        let mutable count = columnNumber
        let mutable isGood = true
        while count > 0 && isGood do
            if not column.IsEndColumn then
                column <- column.Add 1
                count <- count - 1
            if 
                column.IsEndColumn || 
                column.LineNumber <> line.LineNumber ||
                (column.IsLineBreak && not includeLineBreak)
            then
                isGood <- false

        // Account for the case when columNumber is 0 and we're on an empty line
        if (column.IsLineBreak || column.IsEndColumn) && not includeLineBreak then None
        elif isGood then Some column
        else None

    static member GetLineStart(line: ITextSnapshotLine) = SnapshotColumn(line, line.Start)

    static member GetLineEnd(line: ITextSnapshotLine) = SnapshotColumn(line, line.End)

    static member GetForColumnNumberOrEnd(line: ITextSnapshotLine, columnNumber: int) =
        match SnapshotColumn.GetForColumnNumber(line, columnNumber, includeLineBreak = true) with
        | Some column -> column
        | None -> SnapshotColumn(line.End)

    static member GetForLineAndColumnNumber((snapshot: ITextSnapshot), lineNumber: int, columnNumber: int, ?includeLineBreak) =
        let includeLineBreak = defaultArg includeLineBreak false
        if lineNumber < 0 || lineNumber >= snapshot.LineCount then
            None
        else
            let line = snapshot.GetLineFromLineNumber(lineNumber)
            SnapshotColumn.GetForColumnNumber(line, columnNumber, includeLineBreak)

    /// Get a sequence of columns which begins with the specified searchColumn in the direction provided
    /// by serachPath. The searchColumn will always be included in the results except if:
    ///  - It's a line break  and line breaks are not included
    ///  - It's the end point 
    static member GetColumns(searchColumn: SnapshotColumn, searchPath: SearchPath, ?includeLineBreaks) =
        let includeLineBreaks = defaultArg includeLineBreaks false
        match searchPath with
        | SearchPath.Forward -> 
            seq {
                let mutable current = searchColumn
                while not current.IsEndColumn do
                    if not current.IsLineBreak || includeLineBreaks then
                        yield current
                    current <- current.Add 1
            }
        | SearchPath.Backward ->
            seq {
                let mutable isDone = false
                let mutable current = searchColumn
                while not isDone do
                    if 
                        not current.IsEndColumn && 
                        (not current.IsLineBreak || includeLineBreaks)
                    then
                        yield current

                    if current.IsStartColumn then
                        isDone <- true
                    else
                        current <- current.Subtract 1
            }

    /// Get all of the columns on a specified line in the specified order.
    static member GetColumnsInLine(searchLine: ITextSnapshotLine, searchPath: SearchPath, ?includeLineBreak) =
        let includeLineBreak = defaultArg includeLineBreak false
        let all = seq {
            let mutable current = SnapshotColumn(searchLine)
            while not current.IsEndColumn && current.LineNumber = searchLine.LineNumber do 
                if not current.IsLineBreak || includeLineBreak then
                    yield current
                current <- current.Add 1
        }

        match searchPath with 
        | SearchPath.Forward -> all
        | SearchPath.Backward -> Seq.rev all

    /// Get the total count of columns on the line, potentially including the line break / end.
    static member GetColumnCountInLine(line: ITextSnapshotLine, ?includeLineBreak) = 
        let includeLineBreak = defaultArg includeLineBreak false
        SnapshotColumn.GetColumnsInLine(line, SearchPath.Forward, includeLineBreak)
        |> Seq.length

    static member GetStartColumn(snapshot: ITextSnapshot) = 
        let startPoint = SnapshotPoint(snapshot, 0)
        SnapshotColumn(startPoint)

    static member GetEndColumn(snapshot: ITextSnapshot) = 
        let endPoint = SnapshotPoint(snapshot, snapshot.Length)
        SnapshotColumn(endPoint)

    static member GetSpacesOnLine(line: ITextSnapshotLine, tabStop: int): int =
        let column = SnapshotColumn(line.End)
        column.GetSpacesToColumn tabStop

    static member GetSpacesToColumnNumber(line: ITextSnapshotLine, columnNumber: int, tabStop: int): int =
        let mutable spaces = 0
        let mutable current = SnapshotColumn(line)

        while current.ColumnNumber < columnNumber && not current.IsLineBreakOrEnd do
            if current.IsCharacter '\t' then
                let remainder = spaces % tabStop 
                spaces <- spaces + (tabStop - remainder)
            else
                spaces <- spaces + (current.GetSpaces tabStop)
            current <- current.Add 1
        spaces

    /// Get the SnapshotColumn on the line which contains the "spaces" value. This will fail if "spaces" goes
    /// past the line break (but will succeed if it points to the line break)
    static member GetColumnForSpaces(line: ITextSnapshotLine, spaces: int, tabStop: int): SnapshotColumn option = 
        match SnapshotOverlapColumn.GetColumnForSpaces(line, spaces, tabStop) with
        | Some column -> Some column.Column
        | None -> None

    static member GetColumnForSpacesOrEnd(line: ITextSnapshotLine, spaces: int, tabStop: int): SnapshotColumn =
        match SnapshotColumn.GetColumnForSpaces(line, spaces, tabStop) with
        | Some column -> column
        | None -> SnapshotColumn(line, line.End)

/// The Text Editor interfaces only have granularity down to the character in the 
/// ITextBuffer.  However Vim needs to go a bit deeper in certain scenarios like 
/// BlockSpan's.  It needs to understand spaces within a single SnapshotColumn when
/// there are multiple logical characters (like tabs).  This structure represents
/// a value within a SnapshotPoint
and [<Struct>] [<StructuralEquality>] [<NoComparison>] [<DebuggerDisplay("{ToString()}")>] SnapshotOverlapColumn =

    val private _column: SnapshotColumn
    val private _beforeSpaces: int
    val private _totalSpaces: int
    val private _tabStop: int

    private new (column: SnapshotColumn, beforeSpaces: int, totalSpaces: int, tabStop: int) = 
        if totalSpaces < 0 then
            invalidArg "totalSpaces" "totalSpaces must be positive"
        { _column = column; _beforeSpaces = beforeSpaces; _totalSpaces = totalSpaces; _tabStop = tabStop }

    new (column: SnapshotColumn, tabStop: int) = 
        let spaces = 
            if column.IsLineBreakOrEnd then 0
            else column.GetSpacesInContext tabStop
        { _column = column; _beforeSpaces = 0; _totalSpaces = spaces; _tabStop = tabStop }

    member x.TabStop = x._tabStop

    /// The number of spaces in the overlap point before this space
    member x.SpacesBefore = x._beforeSpaces

    member x.SpacesBeforeTotal = 
        let toColumn = x.Column.GetSpacesToColumn x._tabStop
        toColumn + x.SpacesBefore

    /// The number of spaces in the overlap point after this space 
    member x.SpacesAfter = max 0 ((x._totalSpaces - 1) - x._beforeSpaces)

    /// The SnapshotColumn in which this overlap occurs
    member x.Column: SnapshotColumn = x._column

    member x.CodePoint = x.Column.CodePoint

    member x.Line = x.Column.Line

    member x.LineNumber = x.Column.LineNumber

    /// The number of spaces the column occupies in the editor. 
    ///
    /// An interesting case to consider here is tabs.  They will not always occupy 
    /// 'tabstop' spaces.  It can occupy less if there is a character in front of the 
    /// tab which occurs on a 'tabstop' boundary. 
    member x.TotalSpaces = x._totalSpaces

    member x.Snapshot = x.Column.Snapshot

    member x.WithTabStop(tabStop: int) = 
        if x.TabStop = tabStop then x
        else SnapshotOverlapColumn.GetColumnForSpacesOrEnd(x.Line, x.SpacesBeforeTotal, tabStop)

    override x.ToString() = 
        sprintf "Column: %s Spaces: %d Before: %d After: %d" (x.Column.ToString()) x.TotalSpaces x.SpacesBefore x.SpacesAfter

    static member op_Equality(this, other) = System.Collections.Generic.EqualityComparer<SnapshotOverlapColumn>.Default.Equals(this, other)
    static member op_Inequality(this, other) = not (System.Collections.Generic.EqualityComparer<SnapshotOverlapColumn>.Default.Equals(this, other))

    static member GetColumnForSpaces(line: ITextSnapshotLine, spaces: int, tabStop: int): SnapshotOverlapColumn option = 
        let totalSpaces = spaces
        let mutable current = SnapshotColumn(line, line.Start)
        let mutable spaces = 0 
        let mutable isDone = false
        let mutable value: SnapshotOverlapColumn option = None

        while not isDone do
            let currentSpaces = 
                if current.IsCharacter '\t' then
                    // A tab takes up the remaining spaces on a tabstop increment.
                    let remainder = tabStop - (spaces % tabStop)
                    if remainder = 0 then tabStop
                    else remainder
                else
                    current.GetSpaces tabStop

            if spaces = totalSpaces then
                // Landed exactly at the SnapshotColumn in question
                value <- Some (SnapshotOverlapColumn(current, 0, currentSpaces, tabStop))
                isDone <- true
            elif (spaces + currentSpaces) > totalSpaces then
                // The space is a slice of a SnapshotColumn value.  Have to determine the
                // offset
                let before = totalSpaces - spaces
                value <- Some (SnapshotOverlapColumn(current, before, currentSpaces, tabStop))
                isDone <- true
            elif current.IsLineBreakOrEnd then
                // At this point we are at the end, there are more spaces and hence this has failed.
                isDone <- true
            else
                current <- current.Add 1
                spaces <- spaces + currentSpaces

        value

    static member GetColumnForSpacesOrEnd(line: ITextSnapshotLine, spaces: int, tabStop: int): SnapshotOverlapColumn =
        match SnapshotOverlapColumn.GetColumnForSpaces(line, spaces, tabStop) with
        | Some column -> column
        | None -> 
            let column = SnapshotColumn(line, line.End)
            SnapshotOverlapColumn(column, beforeSpaces = 0, totalSpaces = 1, tabStop = tabStop)

    static member GetLineStart(line: ITextSnapshotLine, tabStop: int) = 
        let startColumn = SnapshotColumn.GetLineStart(line)
        SnapshotOverlapColumn(startColumn, tabStop)

    static member GetLineEnd(line: ITextSnapshotLine, tabStop: int) = 
        let endColumn = SnapshotColumn.GetLineEnd(line)
        SnapshotOverlapColumn(endColumn, tabStop)

/// This is the pair to SnapshotColumn as VirtualSnapshotPoint is to SnapshotPoint
[<Struct>]
[<StructuralEquality>]
[<NoComparison>]
type VirtualSnapshotColumn =

    val private _column: SnapshotColumn
    val private _virtualSpaces: int

    new (point: SnapshotPoint) =
        let column = SnapshotColumn(point)
        { _column = column; _virtualSpaces = 0 }

    new (point: VirtualSnapshotPoint) =
        if point.IsInVirtualSpace then
            let line = point.Position.GetContainingLine()
            let column = SnapshotColumn(line, line.End)
            VirtualSnapshotColumn(column, point.VirtualSpaces)
        else
            VirtualSnapshotColumn(point.Position)

    new (column: SnapshotColumn) =
        { _column = column; _virtualSpaces = 0 }

    new (column: SnapshotColumn, virtualSpaces: int) = 
        // This is deliberately ignoring the provided virtual spaces if this is not the end
        // of the line instead of throwing. That behavior is matching what VirtualSnapshotPoint 
        // does.
        let virtualSpaces =
            if virtualSpaces <> 0 && not (column.IsLineBreak || column.IsEndColumn) then 0
            else virtualSpaces
        { _column = column; _virtualSpaces = virtualSpaces }

    member x.IsInVirtualSpace = x._virtualSpaces <> 0

    member x.VirtualSpaces = x._virtualSpaces;

    member x.Column = x._column

    member x.Line = x._column.Line

    member x.LineNumber = x.Line.LineNumber

    member x.Snapshot = x.Column.Snapshot

    member x.VirtualColumnNumber = x.Column.ColumnNumber + x.VirtualSpaces

    /// The offset in position from the start of the line
    member x.VirtualOffset = x.Column.Offset + x.VirtualSpaces

    member x.VirtualStartPoint = VirtualSnapshotPoint(x._column.StartPoint, x._virtualSpaces)

    /// Get the number of spaces occupied by this column. There are couple of 
    /// caveats to this function
    ///  1. Line break will return 1 no matter how long line break text is
    ///  2. Tabs will return 'tabStop' no matter whether the tab is on a 
    ///     tab boundary.
    ///  3. Virtual spaces will return 1
    member x.GetSpaces tabStop =
        if x.IsInVirtualSpace then 1
        else x.Column.GetSpaces tabStop

    /// Get the number of spaces before this column on the same line. 
    member x.GetSpacesToColumn tabStop =
        if x.IsInVirtualSpace then (x.GetSpacesIncludingToColumn tabStop) - 1
        else x.Column.GetSpacesToColumn tabStop

    /// Get the total number of spaces on the line before and including this 
    /// column
    member x.GetSpacesIncludingToColumn tabStop =
        if x.IsInVirtualSpace then
            let endColumn = SnapshotColumn(x.Line, x.Line.End)
            let spaces = endColumn.GetSpacesIncludingToColumn tabStop
            spaces + x.VirtualSpaces
        else
            x.Column.GetSpacesIncludingToColumn tabStop

    /// Add "count" columns on the current line. If the count exceeds the number of columns on the line 
    /// then it will overflow into virtual space. 
    member x.AddInLine(count: int) = 
        if count >= 0 then
            if x.IsInVirtualSpace then
                VirtualSnapshotColumn(x.Column, x.VirtualSpaces + count)
            else
                let mutable count = count
                let mutable current = x.Column
                while not current.IsLineBreakOrEnd && count > 0 do
                    current <- current.AddInLine(1, includeLineBreak = true)
                    count <- count - 1
                if current.IsLineBreakOrEnd then VirtualSnapshotColumn(current, count)
                else VirtualSnapshotColumn(current)
        else
            let originalCount = -count
            let count = originalCount
            if count < x.VirtualSpaces then
                VirtualSnapshotColumn(x.Column, x.VirtualSpaces - count)
            else
                let mutable current = x.Column
                let mutable count = count - x.VirtualSpaces
                while count > 0 do
                    if current.IsStartOfLine then
                        invalidArg "count" (Resources.Common_InvalidColumnCount originalCount)
                    count <- count - 1
                    current <- current.Subtract 1
                VirtualSnapshotColumn(current)

    /// Subtract "count" columns on the current line. Will throw if the count forces it to move past the
    /// start of the line
    member x.SubtractInLine(count: int) = 
        x.AddInLine(-count)
    
    member x.SubtractOneOrCurrent() =
        if x.IsInVirtualSpace then
            x.SubtractInLine 1
        else
            let column = x.Column.SubtractOneOrCurrent()
            VirtualSnapshotColumn(column)

    member x.TryAddInLine(count: int) = 
        if count >= 0 then 
            Some (x.AddInLine(count))
        else
            let mutable count = -count
            let mutable current = x
            while count > 0 && not current.Column.IsStartOfLine do
                current <- current.SubtractInLine(1)
                count <- count - 1
            if count > 0 then None
            else Some current

    override x.ToString() =
        sprintf "Spaces %d %s" x._virtualSpaces (x._column.ToString())

    static member GetLineStart(line: ITextSnapshotLine) = VirtualSnapshotColumn(line.Start)

    static member GetLineBreak(line: ITextSnapshotLine) = VirtualSnapshotColumn(line.End)

    static member GetLineEnd(line: ITextSnapshotLine) = 
        let column = SnapshotColumn.GetLineEnd(line)
        VirtualSnapshotColumn(column)

    /// Get the count of spaces to get to the specified absolute column offset.  This will count
    /// tabs as counting for 'tabstop' spaces.  Note though that tabs which don't occur on a 'tabstop'
    /// boundary only count for the number of spaces to get to the next tabstop boundary. The column
    /// number is allowed to extend into virtual spaces.
    static member GetSpacesToColumnNumber(line: ITextSnapshotLine, columnNumber: int, tabStop: int) =
        let column = SnapshotColumn.GetForColumnNumberOrEnd(line, columnNumber)
        let remainingSpaces = columnNumber - column.ColumnNumber
        let spaces = column.GetSpacesToColumn tabStop
        remainingSpaces + spaces

    /// Create for the specified column number in the line. Any extra columns past the end will betreated
    /// as virtual spaces
    static member GetForColumnNumber(line: ITextSnapshotLine, columnNumber: int) =
        match SnapshotColumn.GetForColumnNumber(line, columnNumber, includeLineBreak = false) with
        | Some column -> 
            VirtualSnapshotColumn(column, 0)
        | None -> 
            let column = SnapshotColumn(line.End)
            let virtualSpaces = columnNumber - column.ColumnNumber
            VirtualSnapshotColumn(column, virtualSpaces)

    /// Get the VirtualSnapshotColumn which begins with the "spaces" count.
    static member GetColumnForSpaces(line: ITextSnapshotLine, spaces: int, tabStop: int): VirtualSnapshotColumn =
        match SnapshotColumn.GetColumnForSpaces(line, spaces, tabStop) with
        | Some column -> VirtualSnapshotColumn(column)
        | None ->
            let endColumn = SnapshotColumn(line, line.End)
            let realSpaces = endColumn.GetSpacesToColumn tabStop
            VirtualSnapshotColumn(endColumn, spaces - realSpaces)

[<Struct>]
[<StructuralEquality>]
[<NoComparison>]
type SnapshotColumnSpan = 

    val private _startColumn: SnapshotColumn
    val private _endColumn: SnapshotColumn

    new(span: SnapshotSpan) =
        let startColumn = SnapshotColumn(span.Start)
        let endColumn = SnapshotColumn(span.End)
        SnapshotColumnSpan(startColumn, endColumn)

    new(startColumn, endColumn) = 
        { _startColumn = startColumn; _endColumn = endColumn }

    new(startColumn: SnapshotColumn, columnLength: int) =
        let endColumn = startColumn.Add(columnLength)
        { _startColumn = startColumn; _endColumn = endColumn }

    new(line: ITextSnapshotLine, ?includeLineBreak) =
        let includeLineBreak = defaultArg includeLineBreak false
        let startColumn = SnapshotColumn(line, line.Start)
        let endColumn = 
            if includeLineBreak then SnapshotColumn(line.EndIncludingLineBreak)
            else SnapshotColumn(line.End)
        { _startColumn = startColumn; _endColumn = endColumn } 

    new(line: ITextSnapshotLine) =
        SnapshotColumnSpan(line, includeLineBreak = false)

    member x.Start = x._startColumn

    member x.End = x._endColumn

    member x.IsEmpty = x.Start = x.End

    member x.Span = SnapshotSpan(x.Start.StartPoint, x.End.StartPoint)

    member x.StartLine = x.Start.Line

    member x.EndLine = x.End.Line

    member x.LastLine = 
        if not x.IsEmpty then
            x.End.Subtract(1).Line
        else
            x.StartLine

    member x.Last =
        if x.IsEmpty then None
        else x.End.Subtract(1) |> Some

    member x.LineCount =
        (x.LastLine.LineNumber - x.StartLine.LineNumber) + 1

    member x.GetColumns(searchPath) = 
        match searchPath with 
        | SearchPath.Forward ->
            let x = x
            seq {
                let mutable current = x.Start
                while (current <> x.End) do
                    yield current
                    current <- current.Add(1)
            }
        | SearchPath.Backward ->
            x.GetColumns SearchPath.Forward
            |> Seq.rev

    member x.GetText() = x.Span.GetText()

    override x.ToString() = sprintf "Start: %s End: %s" (x.Start.ToString()) (x.End.ToString())

[<Struct>]
[<StructuralEquality>]
[<NoComparison>]
type VirtualSnapshotColumnSpan = 

    val private _startColumn: VirtualSnapshotColumn
    val private _endColumn: VirtualSnapshotColumn

    new(span: VirtualSnapshotSpan) =
        let startColumn = VirtualSnapshotColumn(span.Start)
        let endColumn = VirtualSnapshotColumn(span.End)
        { _startColumn = startColumn; _endColumn = endColumn }

    new(startColumn, endColumn) = 
        { _startColumn = startColumn; _endColumn = endColumn }

    new(startColumn: SnapshotColumn, endColumn: VirtualSnapshotColumn) = 
        let startColumn = VirtualSnapshotColumn(startColumn)
        { _startColumn = startColumn; _endColumn = endColumn }

    new(startColumn: VirtualSnapshotColumn, endColumn: SnapshotColumn) = 
        let endColumn = VirtualSnapshotColumn(endColumn)
        { _startColumn = startColumn; _endColumn = endColumn }

    new(startColumn: SnapshotColumn, endColumn: SnapshotColumn) = 
        let startColumn = VirtualSnapshotColumn(startColumn)
        let endColumn = VirtualSnapshotColumn(endColumn)
        { _startColumn = startColumn; _endColumn = endColumn }

    member x.Start = x._startColumn

    member x.End = x._endColumn

    member x.IsInVirtualSpace = x.Start.IsInVirtualSpace || x.End.IsInVirtualSpace

    member x.IsEmpty = x.Start = x.End

    member x.ColumnSpan = SnapshotColumnSpan(x.Start.Column, x.End.Column)

    member x.Span = x.ColumnSpan.Span

    member x.VirtualSpan = VirtualSnapshotSpan(x.Start.VirtualStartPoint, x.End.VirtualStartPoint)

    member x.StartLine = x.Start.Line

    member x.EndLine = x.End.Line

    member x.LastLine = 
        if x.End.IsInVirtualSpace then
            x.End.Line
        elif not x.IsEmpty then
            x.End.Column.Subtract(1).Line
        else
            x.StartLine

    /// Get the number of lines in this VirtualSnapshotSpan
    member x.LineCount =
        (x.LastLine.LineNumber - x.StartLine.LineNumber) + 1

    member x.GetText() = x.Span.GetText()

    override x.ToString() = sprintf "Start: %s End: %s" (x.Start.ToString()) (x.End.ToString())

[<Struct>] 
[<StructuralEquality>] 
[<NoComparison>] 
[<DebuggerDisplay("{ToString()}")>] 
type SnapshotOverlapColumnSpan = 

    val private _start: SnapshotOverlapColumn
    val private _end: SnapshotOverlapColumn 

    new (startColumn: SnapshotOverlapColumn, endColumn: SnapshotOverlapColumn, tabStop: int) = 
        let startColumn = startColumn.WithTabStop tabStop
        let endColumn = endColumn.WithTabStop tabStop
        if startColumn.Column.StartPosition + startColumn.SpacesBefore > endColumn.Column.StartPosition + endColumn.SpacesBefore then
            invalidArg "endColumn" "End cannot be before the start"
        { _start = startColumn; _end = endColumn }

    new (span: SnapshotColumnSpan, tabStop: int) =
        let startColumn = SnapshotOverlapColumn(span.Start, tabStop = tabStop)
        let endColumn = SnapshotOverlapColumn(span.End, tabStop = tabStop)
        { _start = startColumn; _end = endColumn }

    new (span: SnapshotSpan, tabStop: int) =
        let startColumn = SnapshotOverlapColumn(SnapshotColumn(span.Start), tabStop = tabStop)
        let endColumn = SnapshotOverlapColumn(SnapshotColumn(span.End), tabStop = tabStop)
        { _start = startColumn; _end = endColumn }

    member x.Start = x._start

    member x.End = x._end

    /// Does this structure have any overlap
    member x.HasOverlap = x.HasOverlapStart || x.HasOverlapEnd

    /// Does this structure have any overlap at the start
    member x.HasOverlapStart = x.Start.SpacesBefore > 0 

    /// Does this structure have any overlap at the end 
    member x.HasOverlapEnd = x.End.SpacesBefore > 0 

    member x.OverarchingStart = x._start.Column

    member x.OverarchingEnd = 
        if x.End.SpacesBefore = 0 then
            x.End.Column
        else
            x.End.Column.AddOneOrCurrent()

    /// A SnapshotSpan which fully encompasses this overlap span 
    member x.OverarchingSpan = SnapshotColumnSpan(x.OverarchingStart, x.OverarchingEnd)

    /// This is the SnapshotSpan which contains the SnapshotColumn values which have 
    /// full coverage.  The edges which have overlap are excluded from this span
    member x.InnerSpan =    
        let startColumn = 
            if x.Start.SpacesBefore = 0 then x.Start.Column
            else x.Start.Column.AddOneOrCurrent()
        let endColumn = 
            if x.End.SpacesBefore = 0 then x.End.Column
            else x.End.Column.SubtractOneOrCurrent()
        if startColumn.StartPosition <= endColumn.StartPosition then
            SnapshotColumnSpan(startColumn, endColumn)
        else
            SnapshotColumnSpan(startColumn, startColumn)

    member x.Snapshot = x._start.Snapshot

    member x.TabStop = x.Start.TabStop

    /// Get the text contained in this SnapshotOverlapSpan.  All overlap points are expressed
    /// with the appropriate number of spaces 
    member x.GetText() = 

        let builder = StringBuilder()

        if x.Start.Column = x.End.Column then
            // Special case the scenario where the span is within a single SnapshotPoint
            // value.  Just create the correct number of spaces here 
            let count = x.End.SpacesBefore - x.Start.SpacesBefore 
            for i = 1 to count do 
                builder.AppendChar ' '
        else
            // First add in the spaces for the start if it is an overlap point 
            let mutable current = x.Start.Column
            if x.Start.SpacesBefore > 0 then
                for i = 0 to x.Start.SpacesAfter do
                    builder.AppendChar ' '
                current <- current.Add 1

            // Next add in the middle SnapshotPoint values which don't have any overlap
            // to consider.  Don't use InnerSpan.GetText() here as it will unnecessarily
            // allocate an extra string 
            while current.StartPosition < x.End.Column.StartPosition do
                let text = current.GetText()
                builder.AppendString text
                current <- current.Add 1

            // Lastly add in the spaces on the end point.  Remember End is exclusive so 
            // only add spaces which come before
            if x.End.SpacesBefore > 0 then
                for i = 0 to (x.End.SpacesBefore - 1) do
                    builder.AppendChar ' '

        builder.ToString()

    override x.ToString() = 
        x.OverarchingSpan.ToString()

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

    member x.ColumnExtent = SnapshotColumnSpan(x.Extent)

    member x.ColumnExtentIncludingLineBreak = SnapshotColumnSpan(x.ExtentIncludingLineBreak)

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
    let GetLastLineAndLineCount span = 
        let startLine, lastLine = GetStartAndLastLine span
        let lineCount = (lastLine.LineNumber - startLine.LineNumber) + 1
        lastLine, lineCount

    /// Get the number of lines in this SnapshotSpan
    let GetLineCount span = 
        let _, lineCount = GetLastLineAndLineCount span
        lineCount

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

    /// Get the character spans in the line in the path
    let private GetColumnsCore path includeLineBreak line =
        let startColumn = SnapshotColumn(GetStart line)
        let items = 
            seq { 
                let mutable column = SnapshotColumn(line)
                while (not column.IsLineBreakOrEnd) do
                    yield column
                    column <- column.Add 1
                if includeLineBreak then
                    yield column
            }
        match path with
        | SearchPath.Forward -> items
        | SearchPath.Backward -> Seq.rev items

    /// Get the character spans in the specified direction
    let GetColumns path line = GetColumnsCore path false line

    /// Get the character spans in the specified direction including the line break
    let GetColumnsIncludingLineBreak path line = GetColumnsCore path true line

    let GetColumnsCount path line = 
        let seq = GetColumns path line
        Seq.length seq

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
    let GetOffsetOrEnd offset (line: ITextSnapshotLine) = 
        if line.Start.Position + offset >= line.End.Position then line.End
        else line.Start.Add(offset)

    /// Get a SnapshotPoint representing 'offset' characters into the line or it's
    /// line break or the EndIncludingLineBreak of the line
    let GetOffsetOrEndIncludingLineBreak offset (line: ITextSnapshotLine) = 
        if line.Start.Position + offset > line.End.Position then line.EndIncludingLineBreak
        else line.Start.Add(offset)

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

    /// Whether the specified point is the start/end of an empty line
    let IsEmptyLine point =
        let line = GetContainingLine point
        line.Start.Position = point.Position && line.End.Position = point.Position

    /// Is this the start of the Snapshot
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
    let GetLineAndOffset point = 
        let line = GetContainingLine point
        let offset = point.Position - line.Start.Position
        (line, offset)

    let GetLineNumberAndOffset point = 
        let line, offset = GetLineAndOffset point
        (line.LineNumber, offset)

    let GetLineOffset point = 
        let line = GetContainingLine point
        point.Position - line.Start.Position

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

    /// Get a point relative to a starting point backward or forward
    /// 'count' characters skipping line breaks if 'skipLineBreaks' is
    /// specified.  Goes as far as possible in the specified direction
    let GetRelativePoint (startPoint: SnapshotPoint) count skipLineBreaks =

        /// Get the relative column in 'direction' using predicate 'isEnd'
        /// to stop the motion
        let GetRelativeColumn direction (isEnd: SnapshotPoint -> bool) =

            let mutable current = startPoint
            let mutable currentLine = startPoint.GetContainingLine()
            let mutable remaining = abs count

            let syncLine () = 
                if not (currentLine.ExtentIncludingLineBreak.Contains(current)) then
                    currentLine <- current.GetContainingLine()

            let move () = 
                current <- 
                    if direction = 1 then current.Add(1)
                    else current.Subtract(1)
                syncLine ()

                /// Adjust 'point' backward or forward if it is in the
                /// middle of a line break
                current <-
                    if current.Position <= currentLine.End.Position then
                        current
                    else if direction = -1 then
                        currentLine.End
                    else
                        currentLine.EndIncludingLineBreak
                syncLine ()

            while remaining > 0 && not (isEnd current) do
                move ()
                remaining <- remaining -
                    if skipLineBreaks then
                        if currentLine.Length = 0 || not (EditorCoreUtil.IsInsideLineBreak current currentLine) then
                            1
                        else
                            0
                    else
                        1
            current

        if count < 0 then
            GetRelativeColumn -1 IsStartPoint
        else
            GetRelativeColumn 1 IsEndPointOfLastLine

    /// Get a character span relative to a starting point backward or forward
    /// 'count' characters skipping line breaks if 'skipLineBreaks' is
    /// specified.  Goes as far as possible in the specified direction
    let GetRelativeColumn (column: SnapshotColumn) count skipLineBreaks =

        /// Get the relative column in 'direction' using predicate 'isEnd'
        /// to stop the motion
        let getRelativeColumn direction (isEnd: SnapshotPoint -> bool) =
            let mutable column = column
            let mutable remaining = abs count
            while remaining > 0 && not (isEnd column.StartPoint) do
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

        column

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
    let GetSpacesToPoint (point: SnapshotPoint) tabStop = 
        let column = SnapshotColumn(point)
        column.GetSpacesToColumn tabStop

    /// Get the point or the end point of the last line, whichever is first
    let GetPointOrEndPointOfLastLine (point: SnapshotPoint) =
        let endPointOfLastLine = SnapshotUtil.GetEndPointOfLastLine point.Snapshot
        match OrderAscending point endPointOfLastLine with
        | (firstPoint, _) -> firstPoint

/// Functional operations acting on virtual snapshot points
module VirtualSnapshotPointUtil =
    
    /// Create a virtual point from a non-virtual point
    let OfPoint (point: SnapshotPoint) = VirtualSnapshotPoint(point)

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

    /// Get the non-virtual snapshot point from the virtual point
    let GetPoint (point: VirtualSnapshotPoint) = point.Position

    /// Get the buffer position of the non-virtual snapshot point
    let GetPosition point = 
        let point = GetPoint point
        point.Position

    /// Get the snapshot line containing the specified virtual point
    let GetContainingLine (point: VirtualSnapshotPoint) = SnapshotPointUtil.GetContainingLine point.Position

    /// Whether the specified virtual point is in virtual space
    let IsInVirtualSpace (point: VirtualSnapshotPoint) = point.IsInVirtualSpace

    /// Get the line number and column number of the specified virtual point
    let GetLineAndOffset (point: VirtualSnapshotPoint) =
        let line = GetContainingLine point
        let realOffset = point.Position.Position - line.Start.Position
        line, realOffset + point.VirtualSpaces

    /// Get the line number and column number of the specified virtual point
    let GetLineNumberAndOffset (point: VirtualSnapshotPoint) =
        let line, offset = GetLineAndOffset point
        (line.LineNumber, offset)

    /// Get the column number of the specified virtual point
    let GetLineOffset (point: VirtualSnapshotPoint) =
        let _, offset = GetLineAndOffset point
        offset

    /// Add count to the VirtualSnapshotPoint keeping it on the same line
    let AddOnSameLine count point =
        let line, offset = GetLineAndOffset point
        VirtualSnapshotPoint(line, offset + count)

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
        let column = VirtualSnapshotColumn(point)
        column.GetSpacesToColumn(tabStop)

    /// Get the next character span in the buffer with wrap
    let GetNextCharacterSpanWithWrap (point: VirtualSnapshotPoint) =
        if point.IsInVirtualSpace then
            VirtualSnapshotPoint(point.Position, point.VirtualSpaces + 1)
        else
            VirtualSnapshotPoint(SnapshotPointUtil.GetNextCharacterSpanWithWrap point.Position)

    /// Get the previous character span in the buffer with wrap
    let GetPreviousCharacterSpanWithWrap (point: VirtualSnapshotPoint) =
        if point.IsInVirtualSpace then
            VirtualSnapshotPoint(point.Position, point.VirtualSpaces - 1)
        else
            VirtualSnapshotPoint(SnapshotPointUtil.GetPreviousCharacterSpanWithWrap point.Position)

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

    /// Get the first line in the VirtualSnapshotSpan
    let GetStartLine (span: VirtualSnapshotSpan) =
        span.Start.Position.GetContainingLine()

    /// Get the end line in the VirtualSnapshotSpan
    let GetLastLine (span: VirtualSnapshotSpan) =
        if span.End.IsInVirtualSpace then
            span.End.Position.GetContainingLine()
        elif span.Length > 0 then
            span.End.Position.Subtract(1).GetContainingLine()
        else
            GetStartLine span

    /// Get the start and end line of the VirtualSnapshotSpan
    let GetStartAndLastLine span = GetStartLine span, GetLastLine span

    /// Get the number of lines in this VirtualSnapshotSpan
    let GetLineCount span =
        let startLine, lastLine = GetStartAndLastLine span
        (lastLine.LineNumber - startLine.LineNumber) + 1

    /// Whether this a multiline VirtualSnapshotSpan
    let IsMultiline span =
        let startLine, lastLine = GetStartAndLastLine span
        startLine.LineNumber < lastLine.LineNumber

    let GetText (span: VirtualSnapshotSpan) =
        let spanText = SnapshotSpanUtil.GetText span.SnapshotSpan
        let virtualSpaces = StringUtil.RepeatChar span.End.VirtualSpaces ' '
        spanText + virtualSpaces

    let Contains (span: VirtualSnapshotSpan) (point: VirtualSnapshotPoint) =
        span.Contains(point)

    let ContainsOrEndsWith (span: VirtualSnapshotSpan) (point: VirtualSnapshotPoint) =
        Contains span point || span.End = point

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

    /// Map the column up to the given ITextSnapshot.  Returns None if the mapping is not 
    /// possible
    let MapColumnUpToSnapshot (bufferGraph: IBufferGraph) (column: SnapshotColumn) snapshot trackingMode affinity =
        match MapPointUpToSnapshot bufferGraph column.StartPoint snapshot trackingMode affinity with
        | Some point -> Some (SnapshotColumn(point))
        | None -> None

    /// Map the point up to the given ITextSnapshot.  Returns None if the mapping is not 
    /// possible
    let MapPointUpToSnapshotStandard (bufferGraph: IBufferGraph) point snapshot =
        MapPointUpToSnapshot bufferGraph point snapshot PointTrackingMode.Negative PositionAffinity.Predecessor

    /// Map the column up to the given ITextSnapshot.  Returns None if the mapping is not 
    /// possible
    let MapColumnUpToSnapshotStandard (bufferGraph: IBufferGraph) column snapshot =
        MapColumnUpToSnapshot bufferGraph column snapshot PointTrackingMode.Negative PositionAffinity.Predecessor

    /// Map the point down to the given ITextSnapshot.  Returns None if the mapping is not 
    /// possible
    let MapPointDownToSnapshot (bufferGraph: IBufferGraph) point snapshot trackingMode affinity =
        try
            bufferGraph.MapDownToSnapshot(point, trackingMode, snapshot, affinity)
            |> OptionUtil.ofNullable
        with
            | :? System.ArgumentException-> None
            | :? System.InvalidOperationException -> None

    /// Map the column down to the given ITextSnapshot.  Returns None if the mapping is not 
    /// possible
    let MapColumnDownToSnapshot (bufferGraph: IBufferGraph) (column: SnapshotColumn) snapshot trackingMode affinity =
        match MapPointDownToSnapshot bufferGraph column.StartPoint snapshot trackingMode affinity with
        | Some point -> Some (SnapshotColumn(point))
        | None -> None

    /// Map the point down to the given ITextSnapshot.  Returns None if the mapping is not 
    /// possible
    let MapPointDownToSnapshotStandard (bufferGraph: IBufferGraph) point snapshot =
        MapPointDownToSnapshot bufferGraph point snapshot PointTrackingMode.Negative PositionAffinity.Predecessor

    /// Map the column down to the given ITextSnapshot.  Returns None if the mapping is not 
    /// possible
    let MapColumnDownToSnapshotStandard (bufferGraph: IBufferGraph) column snapshot =
        MapColumnDownToSnapshot bufferGraph column snapshot PointTrackingMode.Negative PositionAffinity.Predecessor

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
    member x.CaretVirtualColumn = VirtualSnapshotColumn(x.CaretVirtualPoint)

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

    let GetCaretColumn (textView: ITextView) = 
        let point = textView.Caret.Position.BufferPosition
        SnapshotColumn(point)

    let GetCaretVirtualColumn (textView: ITextView) = 
        let point = textView.Caret.Position.VirtualBufferPosition
        VirtualSnapshotColumn(point)

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
            | :? InvalidOperationException as ex ->
                VimTrace.TraceError ex
                None

    /// Get the text view line relative to the specified point
    let GetTextViewLineRelativeToPoint textView offset (point: SnapshotPoint) =

        // Try to get the text view lines for the same snapshot as point.
        match GetTextViewLines textView with
        | Some textViewLines when
            textViewLines.FormattedSpan.Snapshot = point.Snapshot ->

            // Protect against GetTextViewLineContainingBufferPosition
            // returning null.
            match textViewLines.GetTextViewLineContainingBufferPosition point with
            | textViewLine when textViewLine <> null && textViewLine.IsValid ->
                if offset = 0 then
                    Some textViewLine
                else

                    // Validate offset.
                    let index = textViewLines.GetIndexOfTextLine textViewLine
                    if index + offset >= 0 && index + offset < textViewLines.Count then
                        let textViewLine = textViewLines.[index + offset]
                        if textViewLine.IsValid then
                            Some textViewLine
                        else
                            None
                    else
                        None
            | _ -> None
        | _ -> None

    /// Get the text view line containing the specified point
    let GetTextViewLineContainingPoint textView point =
        GetTextViewLineRelativeToPoint textView 0 point

    /// Get the text view line containing the specified point
    let GetTextViewLineContainingCaret textView =
        GetCaretPoint textView
        |> GetTextViewLineRelativeToPoint textView 0

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

        // We need to scroll if the caret is offscreen.
        match GetTextViewLineContainingPoint textView point with
        | Some textViewLine when textViewLine.VisibilityState = Formatting.VisibilityState.FullyVisible ->
            ()
        | _ ->
            match GetTextViewLines textView with
            | Some textViewLines -> doScrollToPoint textViewLines
            | _ -> ()

    let IsSelectionEmpty (textView: ITextView) =
        textView.Selection.IsEmpty
        || textView.Selection.StreamSelectionSpan.Length = 0

    /// Clear out the selection if it isn't already cleared
    let ClearSelection (textView: ITextView) =
        if not (IsSelectionEmpty textView) then
            textView.Selection.Clear()

    /// Move the caret to the specified point if it isn't already there
    let private MoveCaretToCommon (textView: ITextView) (point: VirtualSnapshotPoint) (flags: MoveCaretFlags) = 

        // Don't change the caret position if it is correct.
        if textView.Caret.Position.VirtualBufferPosition <> point then
            textView.Caret.MoveTo(point) |> ignore

        if Util.IsFlagSet flags MoveCaretFlags.ClearSelection then
            ClearSelection textView

        if Util.IsFlagSet flags MoveCaretFlags.EnsureOnScreen then
            let point = GetCaretPoint textView
            CheckScrollToPoint textView point
            EnsureCaretOnScreen textView

    /// Move the caret to the given point
    let MoveCaretToPointRaw textView (point: SnapshotPoint) flags = 
        let point = VirtualSnapshotPoint(point)
        MoveCaretToCommon textView point flags

    /// Move the caret to the given point, ensure it is on screen and clear out the previous 
    /// selection.  Will not expand any outlining regions
    let MoveCaretToPoint textView point =
        MoveCaretToPointRaw textView point MoveCaretFlags.All

    /// Move the caret to the given point with the specified flags
    let MoveCaretToVirtualPointRaw textView (point: VirtualSnapshotPoint) flags = 
        MoveCaretToCommon textView point flags

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

    /// Move the caret to the given column, ensure it is on screen and clear out the previous 
    /// selection.  Will not expand any outlining regions
    let MoveCaretToColumnRaw textView (column: SnapshotColumn) flags = 
        MoveCaretToPointRaw textView column.StartPoint flags

    /// Move the caret to the given column, ensure it is on screen and clear out the previous 
    /// selection.  Will not expand any outlining regions
    let MoveCaretToColumn textView column = 
        MoveCaretToColumnRaw textView column MoveCaretFlags.All

    /// Apply the specified primary selection
    let Select (textView: ITextView) caretPoint (anchorPoint: VirtualSnapshotPoint) (activePoint: VirtualSnapshotPoint) =
        MoveCaretToCommon textView caretPoint MoveCaretFlags.None

        // Don't change the selection if it is correct.
        if
            textView.Selection.AnchorPoint <> anchorPoint
            || textView.Selection.ActivePoint <> activePoint
        then
            textView.Selection.Select(anchorPoint, activePoint)

    // Apply the specified primary selection
    let SelectSpan (textView: ITextView) (selectedSpan: SelectedSpan) =
        Select textView selectedSpan.CaretPoint selectedSpan.AnchorPoint selectedSpan.ActivePoint

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

    /// Get the visible span for the specified text view line
    let GetVisibleSpan (textView: ITextView) (textViewLine: ITextViewLine) =

        let firstPoint =

            // Whether the specified point is to the right of the left edge of
            // the viewport.
            let isRightOfViewportLeft (point: SnapshotPoint) =
                let bounds = textViewLine.GetCharacterBounds(point)
                bounds.Left >= textView.ViewportLeft

            // Scan forward looking for a visible point.
            textViewLine.Extent
            |> SnapshotSpanUtil.GetPoints SearchPath.Forward
            |> Seq.tryFind isRightOfViewportLeft
            |> Option.defaultValue textViewLine.Start

        let lastPoint =

            // Whether the specified point is to the left of the right edge of
            // the viewport.
            let isLeftOfViewportRight (point: SnapshotPoint) =
                let bounds = textViewLine.GetCharacterBounds(point)
                bounds.Right <= textView.ViewportRight

            // Scan backward looking for a visible point.
            textViewLine.Extent
            |> SnapshotSpanUtil.GetPoints SearchPath.Backward
            |> Seq.tryFind isLeftOfViewportRight
            |> Option.defaultValue textViewLine.End

        SnapshotSpan(firstPoint, lastPoint)

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
        elif oldSnapshot.Version.ReiteratedVersionNumber = newSnapshot.Version.ReiteratedVersionNumber then
            Some (SnapshotPoint(newSnapshot, point.Position))
        else
            let trackingPoint = oldSnapshot.CreateTrackingPoint(point.Position, mode)
            GetPoint newSnapshot trackingPoint

    let GetVirtualPointInSnapshot (point: VirtualSnapshotPoint) mode (newSnapshot: ITextSnapshot) =
        let oldSnapshot = SnapshotPointUtil.GetSnapshot point.Position
        if oldSnapshot.Version.VersionNumber = newSnapshot.Version.VersionNumber then
            Some point
        elif oldSnapshot.Version.ReiteratedVersionNumber = newSnapshot.Version.ReiteratedVersionNumber then
            Some (VirtualSnapshotPoint(SnapshotPoint(newSnapshot, point.Position.Position), point.VirtualSpaces))
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

/// Abstraction useful for APIs which need to work over a single SnapshotColumnSpan 
/// or collection of SnapshotColumnSpan values
[<RequireQualifiedAccess>]
type EditSpan = 
    /// Common case of an edit operation which occurs over a single SnapshotSpan
    | Single of ColumnSpan: SnapshotColumnSpan 

    /// Occurs during block edits
    | Block of ColumnSpans: NonEmptyCollection<SnapshotOverlapColumnSpan>

    with

    /// View the data as a collection.  For Single values this just creates a
    /// collection with a single element
    member x.ColumnSpans =
        match x with
        | Single span -> NonEmptyCollection(span, List.empty) 
        | Block col -> col |> NonEmptyCollectionUtil.Map (fun span -> span.OverarchingSpan)

    member x.Spans = x.ColumnSpans |> NonEmptyCollectionUtil.Map (fun span -> span.Span)

    /// View the data as a collection of overlap spans.  For Single values this just creates a
    /// collection with a single element
    member x.GetOverlapSpans(tabStop: int)=
        match x with
        | Single span -> 
            let span = SnapshotOverlapColumnSpan(span, tabStop)
            NonEmptyCollection(span, List.empty) 
        | Block col -> col

    /// Returns the overarching span of the entire EditSpan value.  For Single values
    /// this is a 1-1 mapping.  For Block values it will take the min start position
    /// and combine it with the maximum end position
    member x.OverarchingColumnSpan =
        match x with 
        | Single span -> span
        | Block col -> 
            let startColumn = col |> Seq.map (fun s -> s.Start.Column) |> Seq.minBy (fun s -> s.StartPosition)
            let endColumn = col |> Seq.map (fun s -> s.End.Column) |> Seq.maxBy (fun s -> s.StartPosition)
            SnapshotColumnSpan(startColumn, endColumn)

    member x.OverarchingSpan = x.OverarchingColumnSpan.Span

    /// Provide an implicit conversion from SnapshotSpan.  Useful from C# code
    static member op_Implicit span = EditSpan.Single span

    /// Provide an implicit conversion from NormalizedSnapshotSpan.  Useful from C# code
    static member op_Implicit block = EditSpan.Block block

module EditUtil = 

    /// NewLine to use for the ITextBuffer
    let NewLine (options: IEditorOptions) (textBuffer: ITextBuffer) =

        // According to the documentation for
        // DefaultOptions.NewLineCharacterOptionId,
        // DefaultOptionExtensions.GetNewLineCharacter is only used if
        // DefaultOptionExtensions.GetReplicateNewLineCharacter is false or if
        // the text buffer is empty. See issue #2561.
        let replicateNewLine = DefaultOptionExtensions.GetReplicateNewLineCharacter options
        if replicateNewLine && textBuffer.CurrentSnapshot.Length > 0 then
            let firstNewLine =
                SnapshotUtil.GetLine textBuffer.CurrentSnapshot 0
                |> SnapshotLineUtil.GetLineBreakSpan
                |> SnapshotSpanUtil.GetText
            if StringUtil.IsNullOrEmpty firstNewLine then
                DefaultOptionExtensions.GetNewLineCharacter options
            else
                firstNewLine
        else
            DefaultOptionExtensions.GetNewLineCharacter options

    /// Get the text for a tab character based on the given options
    let GetTabText (options: IEditorOptions) = 
        if DefaultOptionExtensions.IsConvertTabsToSpacesEnabled options then
            StringUtil.RepeatChar (DefaultOptionExtensions.GetTabSize options) ' '
        else
            "\t"

    /// Get the length of the line break at the given index 
    let GetLineBreakLengthAtIndex (str: string) index =
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

    let IsInsideLineBreakAtIndex (str: string) index = GetLineBreakLengthAtIndex str index > 0

    let GetFullLineBreakSpanAtIndex (str: string) index =
        if str.Chars(index) = '\n' && index > 0 && str.Chars(index - 1) = '\r' then
            Some (Span(index - 1, 2))
        else
            let length = GetLineBreakLengthAtIndex str index
            if length = 0 then None
            else Some (Span(index, length))

    /// Get the length of the line break at the end of the string
    let GetLineBreakLengthAtEnd (str: string) =
        if System.String.IsNullOrEmpty str then 
            0
        else
            let index = str.Length - 1
            if str.Length > 1 && str.Chars(index - 1) = '\r' && str.Chars(index) = '\n' then
                2
            else
                GetLineBreakLengthAtIndex str index

    /// Get the count of new lines in the string
    let GetLineBreakCount (str: string) =
        let rec inner index count =
            if index >= str.Length then
                count
            else
                let length = GetLineBreakLengthAtIndex str index 
                if length > 0 then
                    inner (index + length) (count + 1)
                else
                    inner (index + 1) count

        inner 0 0

    /// Get the indentation level given the context line (the line above the line which is 
    /// being indented)
    let GetAutoIndent (contextLine: ITextSnapshotLine) tabStop =
        contextLine 
        |> SnapshotLineUtil.GetIndentPoint 
        |> (fun point -> SnapshotPointUtil.GetSpacesToPoint point tabStop)

    /// Does the specified string end with a valid newline string 
    let EndsWithNewLine value = 0 <> GetLineBreakLengthAtEnd value

    /// Does this text have a new line character inside of it?
    let HasNewLine (text: string) = 
        { 0 .. (text.Length - 1) }
        |> SeqUtil.any (fun index -> GetLineBreakLengthAtIndex text index > 0)

    /// Remove the NewLine at the beginning of the string.  Returns the original input
    /// if no newline is found
    let RemoveBeginingNewLine value = 
        if System.String.IsNullOrEmpty value then
            value
        else
            let length = GetLineBreakLengthAtIndex value 0
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
                let length = GetLineBreakLengthAtIndex text index
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
                let length = EditUtil.GetLineBreakLengthAtIndex fullText index
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

module internal ITextEditExtensions =

    type ITextEdit with

        /// Delete the overlapped span from the ITextBuffer.  If there is any overlap then the
        /// remaining spaces will be filed with ' ' 
        member x.Delete (overlapSpan: SnapshotOverlapColumnSpan) = 
            let pre = overlapSpan.Start.SpacesBefore
            let post = 
                if overlapSpan.HasOverlapEnd then
                    overlapSpan.End.SpacesAfter + 1
                else
                    0

            let span = overlapSpan.OverarchingSpan
            match pre + post with
            | 0 -> x.Delete(span.Span.Span) 
            | _ -> x.Replace(span.Span.Span, String.replicate (pre + post) " ") 


