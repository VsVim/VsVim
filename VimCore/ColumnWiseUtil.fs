namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Projection
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Utilities
open StringBuilderExtensions

module ColumnWiseUtils =
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
    let GetCharacterWidth c (localSettings:IVimLocalSettings) =
        // TODO: for surrogate pairs, we need to be able to match characters specified as strings.
        // E.g. if System.Char.IsHighSurrogate(c) then
        //    let fullchar = point.Snapshot.GetSpan(point.Position, 1).GetText()
        //    CommontUtil.GetSurrogatePairWidth fullchar

        match c with
        | '\u0000' -> 1
        | '\t' -> localSettings.TabStop
        | _ when IsNonSpacingCharacter c -> 0
        | _ when IsWideCharacter c -> 2
        | _ -> 1

    // Get the point in the given line which is just before the character that 
    // overlaps the specified column into the line, as well as the position of 
    // that column inside then character. Returns End if it goes beyond the last 
    // point in the string
    let GetPointForSpacesWithOverlap line spacesCount localsettings = 
        let snapshot = SnapshotLineUtil.GetSnapshot line
        let endPosition = line |> SnapshotLineUtil.GetEnd |> SnapshotPointUtil.GetPosition
        let rec inner position spacesCount = 
            if position = endPosition then
                (0, endPosition, 0)
            else 
                let point = SnapshotPoint(snapshot, position)
                let charWidth = GetCharacterWidth (SnapshotPointUtil.GetChar point) localsettings
                let spacesCount = spacesCount - charWidth
                if spacesCount < 0 then
                    let pre = -spacesCount
                    let post = charWidth - spacesCount
                    (pre, position, post)
                else
                    inner (position + 1) spacesCount

        let (pre, position, post) = inner line.Start.Position spacesCount
        (pre, SnapshotPoint(snapshot, position), post)

    // Get the point in the given line which is just before the character that 
    // overlaps the specified column into the line. Returns End if it goes 
    // beyond the last point in the string
    let GetPointForSpaces line spacesCount localsettings = 
        let _, result, _ = GetPointForSpacesWithOverlap line spacesCount localsettings
        result