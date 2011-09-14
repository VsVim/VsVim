#light

namespace Vim.Interpreter
open Vim

/// A single line specifier in a range 
[<RequireQualifiedAccess>]
type LineSpecifier = 

    /// The line with the specified number
    | Number of int

    /// The current line: '.'
    | CurrentLine

    /// The last line: '$'
    | LastLine

    /// The line containing the specified mark
    | MarkLine of Mark

    /// The next line where the given pattern matches
    | NextLineWithPattern of string

    /// The next line where the previous search pattern occurs
    | NextLineWithPreviousPattern

    /// The next line where the previous substitute pattern occurs
    | NextLineWithPreviousSubstitutePattern

    /// The previous line where the given pattern matches
    | PreviousLineWithPattern of string

    /// The previous line where the previous search pattern occurs
    | PreviousLineWithPreviousPattern

    /// LineSpecifier with the given line adjustment
    | LineSpecifierWithAdjustment of LineSpecifier * int

    /// Adjust the current line with the adjustment.  Current depends on whether this is
    /// the first or second specifier in a range
    | AdjustmentOnCurrent of int

/// A line range in the file 
[<RequireQualifiedAccess>]
type LineRange = 

    /// The entire buffer: '%'
    | EntireBuffer 

    /// A single line range
    | SingleLine of LineSpecifier

    /// A range defined by two lines.  The bool is whether or not the cursor should be
    /// adjusted for the for the second line specifier (true when ';' is used to separate
    /// the LineSpecifier values)
    | Range of LineSpecifier * LineSpecifier * bool

[<RequireQualifiedAccess>]
type Expression =

    /// A variable expression with the provided name
    | Variable of string

[<RequireQualifiedAccess>]
type LineCommand =

    /// Jump to the specified line number 
    | JumpToLine of int

    /// Jump to the last line of the ITextBuffer
    | JumpToLastLine

    /// The :close command.  The bool value represents whether or not the 
    /// bang modifier was added
    | Close of bool


