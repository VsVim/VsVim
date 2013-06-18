namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Projection
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Utilities
open System.Diagnostics
open System.Text
open StringBuilderExtensions

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

    val private _point : SnapshotPoint
    val private _before : int
    val private _width : int

    // BTODO: This constructor should be hidden behind a signature file.  No one should ever call it
    // directly.  It is too easy to get the 'width' wrong
    internal new (point : SnapshotPoint, before : int, width : int) = 
        if width < 0 then
            invalidArg "width" "Width must be positive"
        { _point = point; _before = before; _width = width }

    /// Create a SnapshotOverlapPoint over a SnapshotPoint value.  Even if the character underneath
    /// the SnapshotPoint is wide this will treate it as a width 0 character.  It will never see it
    /// as an overlap 
    new (point : SnapshotPoint) =
        let width = 
            if SnapshotPointUtil.IsEndPoint point then
                0
            else
                1
        { _point = point; _before = 0; _width = width }

    /// The number of spaces in the overlap point before this space
    member x.SpacesBefore = x._before

    /// The number of spaces in the overlap point after this space 
    member x.SpacesAfter = max 0 ((x._width - 1) - x._before)

    /// Does this structure have any overlap
    member x.HasOverlap = x.SpacesAfter <> 0 || x.SpacesBefore <> 0

    /// The SnapshotPoint in which this overlap occurs
    member x.Point = x._point

    /// Number of spaces this SnapshotOverlapPoint occupies
    member x.Width = x._width

    member x.Snapshot = x._point.Snapshot

    static member op_Equality(this,other) = System.Collections.Generic.EqualityComparer<SnapshotOverlapPoint>.Default.Equals(this,other)
    static member op_Inequality(this,other) = not (System.Collections.Generic.EqualityComparer<SnapshotOverlapPoint>.Default.Equals(this,other))

    override x.ToString() = 
        sprintf "Point: %s Width: %d Before: %d After: %d" (x.Point.ToString()) x.Width x.SpacesBefore x.SpacesAfter

and [<StructuralEquality>] [<NoComparison>] [<Struct>] [<DebuggerDisplay("{ToString()}")>] SnapshotOverlapSpan = 

    val private _start : SnapshotOverlapPoint
    val private _end : SnapshotOverlapPoint 

    new (startPoint : SnapshotOverlapPoint, endPoint : SnapshotOverlapPoint) = 
        if startPoint.Point.Position + startPoint.SpacesBefore > endPoint.Point.Position + endPoint.SpacesBefore then
            invalidArg "endPoint" "End cannot be before the start"
        { _start = startPoint; _end = endPoint }

    new (span : SnapshotSpan) =
        let startPoint = SnapshotOverlapPoint(span.Start)
        let endPoint = SnapshotOverlapPoint(span.End)
        { _start = startPoint; _end = endPoint }

    member x.Start = x._start

    member x.End = x._end

    /// Does this structure have any overlap
    member x.HasOverlap = x.Start.HasOverlap || x.End.HasOverlap

    member x.OverarchingStart = x._start.Point

    member x.OverarchingEnd = 
        if x.End.SpacesBefore = 0 then
           x.End.Point
        else
            SnapshotPointUtil.AddOneOrCurrent x.End.Point

    /// A SnapshotSpan which fully encompasses this overlap span 
    member x.OverarchingSpan = SnapshotSpan(x.OverarchingStart, x.OverarchingEnd)

    /// This is the SnapshotSpan which contains the SnapshotPoint values which have 
    /// full coverage.  The edges which have overlap are excluded from this span
    member x.InnerSpan =    
        let startPoint = 
            if x.Start.SpacesBefore = 0 then
                x.Start.Point
            else
                SnapshotPointUtil.AddOneOrCurrent x.Start.Point
        let endPoint = 
            if x.End.SpacesBefore = 0 then
                x.End.Point
            else
                SnapshotPointUtil.SubtractOneOrCurrent x.End.Point
        if startPoint.Position <= endPoint.Position then
            SnapshotSpan(startPoint, endPoint)
        else
            SnapshotSpan(startPoint, startPoint)

    member x.Snapshot = x._start.Snapshot

    /// Get the text contained in this SnapshotOverlapSpan.  All overlap points are expressed
    /// with the appropriate number of spaces 
    member x.GetText() = 

        let builder = StringBuilder()

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
            let point = SnapshotUtil.GetPoint x.Snapshot position
            let c = point.GetChar()
            builder.AppendChar c
            position <- position + 1

        // Lastly add in the spaces on the end point.  Remember End is exclusive so 
        // only add spaces which come before
        if x.End.HasOverlap then
            for i = 0 to (x.End.SpacesBefore - 1) do
                builder.AppendChar ' '

        builder.ToString()

    override x.ToString() = 
        x.OverarchingSpan.ToString()

// TODO: The methods of this type should be combined with SnapshotLineUtil
and internal ColumnWiseUtil =

    /// Determines whether the given character occupies space on screen when displayed.
    /// For instance, combining diacritics occupy the space of the previous character,
    /// while control characters are simply not displayed.
    static member IsNonSpacingCharacter c =
        // based on http://www.cl.cam.ac.uk/~mgk25/ucs/wcwidth.c
        // TODO: this should be checked for consistency with
        //       Visual studio handling of each character.

        match System.Char.GetUnicodeCategory(c) with
        //Visual studio does not render control characters
        | System.Globalization.UnicodeCategory.Control
        | System.Globalization.UnicodeCategory.NonSpacingMark
        | System.Globalization.UnicodeCategory.Format
        | System.Globalization.UnicodeCategory.EnclosingMark ->
            /// Contrarily to http://www.cl.cam.ac.uk/~mgk25/ucs/wcwidth.c
            /// the Soft hyphen (\u00ad) is invisible in VS.
            true
        | _ -> (c = '\u200b') || ('\u1160' <= c && c <= '\u11ff')

    /// Determines if the given character occupies a single or two cells on screen.
    static member IsWideCharacter c =
        // based on http://www.cl.cam.ac.uk/~mgk25/ucs/wcwidth.c
        // TODO: this should be checked for consistency with
        //       Visual studio handling of each character.
        (c >= '\u1100' &&
            (
                // Hangul Jamo init. consonants
                c <= '\u115f' || c = '\u2329' || c = '\u232a' ||
                // CJK ... Yi
                (c >= '\u2e80' && c <= '\ua4cf' && c <> '\u303f') ||
                // Hangul Syllables */
                (c >= '\uac00' && c <= '\ud7a3') ||
                // CJK Compatibility Ideographs
                (c >= '\uf900' && c <= '\ufaff') ||
                // Vertical forms
                (c >= '\ufe10' && c <= '\ufe19') ||
                // CJK Compatibility Forms
                (c >= '\ufe30' && c <= '\ufe6f') ||
                // Fullwidth Forms
                (c >= '\uff00' && c <= '\uff60') ||
                (c >= '\uffe0' && c <= '\uffe6')));

                // The following can only be detected with pairs of characters
                //   (surrogate characters)
                // Supplementary ideographic plane
                //(ucs >= 0x20000 && ucs <= 0x2fffd) ||
                // Tertiary ideographic plane
                //(ucs >= 0x30000 && ucs <= 0x3fffd)));

    /// Determines the character width when displayed, computed according to the various local settings.
    static member GetCharacterWidth c tabStop =
        // TODO: for surrogate pairs, we need to be able to match characters specified as strings.
        // E.g. if System.Char.IsHighSurrogate(c) then
        //    let fullchar = point.Snapshot.GetSpan(point.Position, 1).GetText()
        //    CommontUtil.GetSurrogatePairWidth fullchar

        match c with
        | '\u0000' -> 1
        | '\t' -> tabStop
        | _ when ColumnWiseUtil.IsNonSpacingCharacter c -> 0
        | _ when ColumnWiseUtil.IsWideCharacter c -> 2
        | _ -> 1

    static member GetPointWidth point tabStop = 
        if SnapshotPointUtil.IsEndPoint point then
            0
        else
            let c = point.GetChar()
            ColumnWiseUtil.GetCharacterWidth c tabStop

    // Get the point in the given line which is just before the character that 
    // overlaps the specified column into the line, as well as the position of 
    // that column inside the character. Returns End if it goes beyond the last 
    // point in the string
    static member GetPointForSpacesWithOverlap line spacesCount tabStop = 
        let snapshot = SnapshotLineUtil.GetSnapshot line
        let endPoint = line |> SnapshotLineUtil.GetEnd
        // The following retrieves the location of the character that is
        // spacesCount cells inside the line. The result is a triple 
        // (pre, position, post) where
        //    position is the position in the snapshot of the character that 
        //       overlaps the spacesCount-th cell 
        //    pre is the number of cells that the character spans before the
        //       spacesCount-th cell
        //    post is the number of cells that the character spans after the
        //       spacesCount-th cell
        let rec inner position spacesCount = 
            if position = endPoint.Position then
                (0, endPoint, 0)
            else 
                let point = SnapshotPoint(snapshot, position)
                let charWidth = ColumnWiseUtil.GetCharacterWidth (SnapshotPointUtil.GetChar point) tabStop
                let remaining = spacesCount - charWidth

                if spacesCount = 0 && charWidth <> 0 then
                    (0, point, charWidth)
                elif remaining < 0 then
                    (spacesCount, point, charWidth)
                else
                    inner (position + 1) remaining

        let (before, point, width) = inner line.Start.Position spacesCount
        SnapshotOverlapPoint(point, before, width)

    // Get the point in the given line which is just before the character that 
    // overlaps the specified column into the line. Returns End if it goes 
    // beyond the last point in the string
    static member GetPointForSpaces line spacesCount tabStop = 
        let overlapPoint = ColumnWiseUtil.GetPointForSpacesWithOverlap line spacesCount tabStop
        overlapPoint.Point

    static member GetSpacesForPoint point tabStop = 
        let c = SnapshotPointUtil.GetChar point
        ColumnWiseUtil.GetCharacterWidth c tabStop

    static member GetSpacesForSpan span tabStop = 
        span
        |> SnapshotSpanUtil.GetPoints Path.Forward
        |> Seq.map (fun point -> ColumnWiseUtil.GetSpacesForPoint point tabStop)
        |> Seq.sum

    /// Get the count of spaces to get to the specified absolute column offset.  This will count
    /// tabs as counting for 'tabstop' spaces
    static member GetSpacesToColumn line column tabStop = 
        SnapshotLineUtil.GetSpanInLine line 0 column
        |> SnapshotSpanUtil.GetPoints Path.Forward
        |> Seq.map (fun point -> ColumnWiseUtil.GetSpacesForPoint point tabStop)
        |> Seq.sum

    /// Get the count of spaces to get to the specified point in it's line when tabs are expanded
    static member GetSpacesToPoint point tabStop = 
        let line = SnapshotPointUtil.GetContainingLine point
        let column = point.Position - line.Start.Position 
        ColumnWiseUtil.GetSpacesToColumn line column tabStop

    static member GetSpanFromSpaceAndCount line start count tabStop = 
        let startPoint = ColumnWiseUtil.GetPointForSpacesWithOverlap line start tabStop
        let endPoint = ColumnWiseUtil.GetPointForSpacesWithOverlap line (start + count) tabStop
        SnapshotOverlapSpan(startPoint, endPoint)


