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

    new (point : SnapshotPoint, before : int, width : int) = 
        if width <= 0 then
            invalidArg "width" "Width must be greater than 0"
        { _point = point; _before = before; _width = width }

    /// The number of spaces in the overlap point before this space
    member x.SpacesBefore = x._before

    /// The number of spaces in the overlap point after this space 
    member x.SpacesAfter = (x._width - 1) - x._before

    /// The SnapshotPoint in which this overlap occurs
    member x.Point = x._point

    /// Number of spaces this SnapshotOverlapPoint occupies
    member x.Width = x._width

    member x.Snapshot = x._point.Snapshot

    static member op_Equality(this,other) = System.Collections.Generic.EqualityComparer<SnapshotOverlapPoint>.Default.Equals(this,other)
    static member op_Inequality(this,other) = not (System.Collections.Generic.EqualityComparer<SnapshotOverlapPoint>.Default.Equals(this,other))

[<StructuralEquality>]
[<NoComparison>]
[<Struct>]
[<DebuggerDisplay("{ToString()}")>]
type SnapshotOverlapSpan = 

    val private _start : SnapshotOverlapPoint
    val private _end : SnapshotOverlapPoint 

    new (startPoint : SnapshotOverlapPoint, endPoint : SnapshotOverlapPoint) = 
        if startPoint.Point.Position + startPoint.SpacesBefore > endPoint.Point.Position + endPoint.SpacesBefore then
            invalidArg "endPoint" "End cannot be before the start"
        { _start = startPoint; _end = endPoint }

    member x.Start = x._start

    member x.End = x._end

    member x.OverarchingStart = x._start.Point

    member x.OverarchingEnd = 
        if x.End.SpacesBefore = 0 then
           x.End.Point
        else
            SnapshotPointUtil.AddOneOrCurrent x.End.Point

    /// A SnapshotSpan which fully encompasses this overlap span 
    member x.OverarchingSpan = SnapshotSpan(x.OverarchingStart, x.OverarchingEnd)

    member x.Snapshot = x._start.Snapshot

    /// Get the text contained in this SnapshotOverlapSpan.
    ///
    /// TODO: Handle Unicode splits.  Right now we just handle tabs 
    member x.GetText() = 
        let startOverlapPoint = x.Start
        let endOverlapPoint = x.End
        let getBeforeText (builder : StringBuilder) = 
            match SnapshotPointUtil.TryGetChar startOverlapPoint.Point with
            | Some '\t' ->
                if startOverlapPoint.SpacesBefore = 0 then  
                    builder.AppendChar '\t'
                else
                    for i = 0 to startOverlapPoint.SpacesAfter do
                        builder.AppendChar ' '
            | Some c -> builder.AppendChar c
            | None -> ()

        let getAfterText (builder : StringBuilder) = 
            if endOverlapPoint.SpacesBefore > 0 then
                match SnapshotPointUtil.TryGetChar endOverlapPoint.Point with
                | Some '\t' -> 
                        for i = 0 to (endOverlapPoint.SpacesBefore - 1) do
                            builder.AppendChar ' '
                | Some c -> builder.AppendChar c
                | None -> ()

        let snapshot = x.Snapshot
        let getMiddleText (builder : StringBuilder) = 
            let mutable position = startOverlapPoint.Point.Position + 1
            while position < endOverlapPoint.Point.Position do
                let point = SnapshotUtil.GetPoint snapshot position 
                builder.AppendChar (point.GetChar())
                position <- position + 1
                    
        let builder = StringBuilder()
        getBeforeText builder
        getMiddleText builder
        getAfterText builder
        builder.ToString()

module internal ColumnWiseUtil =

    /// Determines whether the given character occupies space on screen when displayed.
    /// For instance, combining diacritics occupy the space of the previous character,
    /// while control characters are simply not displayed.
    let IsNonSpacingCharacter c =
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
    let IsWideCharacter c =
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
    let GetCharacterWidth c tabStop =
        // TODO: for surrogate pairs, we need to be able to match characters specified as strings.
        // E.g. if System.Char.IsHighSurrogate(c) then
        //    let fullchar = point.Snapshot.GetSpan(point.Position, 1).GetText()
        //    CommontUtil.GetSurrogatePairWidth fullchar

        match c with
        | '\u0000' -> 1
        | '\t' -> tabStop
        | _ when IsNonSpacingCharacter c -> 0
        | _ when IsWideCharacter c -> 2
        | _ -> 1

    // Get the point in the given line which is just before the character that 
    // overlaps the specified column into the line, as well as the position of 
    // that column inside the character. Returns End if it goes beyond the last 
    // point in the string
    let GetPointForSpacesWithOverlap line spacesCount tabStop = 
        let snapshot = SnapshotLineUtil.GetSnapshot line
        let endPosition = line |> SnapshotLineUtil.GetEnd |> SnapshotPointUtil.GetPosition
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
            if position = endPosition then
                (0, endPosition, 0)
            else 
                let point = SnapshotPoint(snapshot, position)
                let charWidth = GetCharacterWidth (SnapshotPointUtil.GetChar point) tabStop
                let remaining = spacesCount - charWidth

                if spacesCount = 0 && charWidth <> 0 then
                    (0, position, 0)
                elif remaining < 0 then
                    (spacesCount, position, charWidth - spacesCount)
                else
                    inner (position + 1) remaining

        let (pre, position, post) = inner line.Start.Position spacesCount
        (pre, SnapshotPoint(snapshot, position), post)

    // Get the point in the given line which is just before the character that 
    // overlaps the specified column into the line. Returns End if it goes 
    // beyond the last point in the string
    let GetPointForSpaces line spacesCount tabStop = 
        let _, result, _ = GetPointForSpacesWithOverlap line spacesCount tabStop
        result

    let GetSpacesForPoint point tabStop = 
        let c = SnapshotPointUtil.GetChar point
        GetCharacterWidth c tabStop

    let GetSpacesForSpan span tabStop = 
        span
        |> SnapshotSpanUtil.GetPoints Path.Forward
        |> Seq.map (fun point -> GetSpacesForPoint point tabStop)
        |> Seq.sum

    /// Get the count of spaces to get to the specified absolute column offset.  This will count
    /// tabs as counting for 'tabstop' spaces
    let GetSpacesToColumn line column tabStop = 
        SnapshotLineUtil.GetSpanInLine line 0 column
        |> SnapshotSpanUtil.GetPoints Path.Forward
        |> Seq.map (fun point -> GetSpacesForPoint point tabStop)
        |> Seq.sum

    /// Get the count of spaces to get to the specified point in it's line when tabs are expanded
    let GetSpacesToPoint point tabStop = 
        let line = SnapshotPointUtil.GetContainingLine point
        let column = point.Position - line.Start.Position 
        GetSpacesToColumn line column tabStop

