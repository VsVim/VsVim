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

/// Represents the values for '++opt' which can occur on commands like :edit
[<RequireQualifiedAccess>]
type FileOption =
    | FileFormat of string
    | Encoding of string
    | Binary 
    | NoBinary
    | Bad
    | Edit

[<RequireQualifiedAccess>]
type SettingDisplay =
    | AllButTerminal
    | AllTerminal
    | Changed

/// Represents te values or the '+cmd' which can occur on commads like :edit
[<RequireQualifiedAccess>]
type CommandOption =
    | StartAtLastLine
    | StartAtLine of int
    | StartAtPattern of string
    | ExecuteLineCommand of LineCommand

and [<RequireQualifiedAccess>] Expression =

    /// A variable expression with the provided name
    | Variable of string

and [<RequireQualifiedAccess>] LineCommand =

    /// The :close command.  The bool value represents whether or not the 
    /// bang modifier was added
    | Close of bool

    /// The :edit command.  The values range as follows
    ///  - ! option present
    ///  - The provided ++opt
    ///  - The provided +cmd 
    ///  - The provided file to edit 
    | Edit of bool * FileOption list * CommandOption option * string option

    /// The :delete command 
    | Delete of LineRange option * RegisterName option * int option 

    /// Display the contents of registers.  Unless a specific register name is 
    /// given all registers will be displayed
    | DisplayRegisters of RegisterName option

    /// Display the specified marks.  If no Mark values are provided then display 
    /// all marks
    | DisplayMarks of Mark list

    /// Display the settings dictated by the SettingDisplay option
    | DisplaySettings of SettingDisplay

    /// Fold the selected LineRange
    | Fold of LineRange option

    /// Join the lines in the specified range.  Optionally provides a count of lines to 
    /// start the join after the line range
    | Join of LineRange option * int option

    /// Jump to the specified line number 
    | JumpToLine of int

    /// Jump to the last line of the ITextBuffer
    | JumpToLastLine

    /// Make command.  The options are as follows
    ///   - The ! option
    ///   - All of the text after the !
    | Make of bool * string

    /// Temporarily disable the 'hlsearch' option
    | NoHlSearch

    /// Put the contents of the given register after the line identified by the
    /// LineRange (defaults to current)
    | PutAfter of LineRange option * RegisterName option

    /// Put the contents of the given register before the line identified by the
    /// LineRange (defaults to current)
    | PutBefore of LineRange option * RegisterName option

    /// Redo the last item on the undo stack
    | Redo

    /// Retab the specified LineRange.  The options are as follows
    ///  - The LineRange to change (defaults to entire buffer)
    ///  - True to replace both tabs and spaces, false for just spaces
    ///  - new tabstop value
    | Retab of LineRange option * bool * int option

    /// The :substitute command.  The argument order is range, search, replace,
    /// substitute flags and count
    | Substitute of LineRange option * string * string * SubstituteFlags * int option

    /// The variant of the :substitute command which repeats the last :subsitute with
    /// different flags and count
    | SubstituteRepeatLast of LineRange option * SubstituteFlags * int option

    /// The variant of the :substitute command which repeats the last :subsitute with
    /// different flags, count and using the last search as the pattern
    | SubstituteRepeatLastWithSearch of LineRange option * SubstituteFlags * int option

    /// Quit the curren window without writing it's content.  If the boolean option
    /// is present (for !) then don't warn about a dirty window
    | Quit of bool

    /// Quit all windows without writing their content and exit Vim.  If the boolean
    /// option is present then don't warn about writing a dirty window
    | QuitAll of bool

    /// Quit the current window after writing out it's contents.  The values range as 
    /// follows
    ///  - Range of lines to write.  All if no option present
    ///  - ! option present
    ///  - The provided ++opt
    ///  - The provided +cmd
    | QuitWithWrite of LineRange option * bool * FileOption list * string option 

