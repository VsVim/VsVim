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

/// The set command can take a number of arguments.  This represents the allowed values
[<RequireQualifiedAccess>]
type SetArgument  =

    /// The 'all' argument. 
    | DisplayAllButTerminal
    
    /// The 'termcap' argument
    | DisplayAllTerminal

    /// Display the specified setting
    | DisplaySetting of string

    /// The 'all&' argument.  Resets all setting to their default value
    | ResetAllToDefault

    /// Use the setting.  Produced when an setting name is used without arguments.  The behavior 
    /// depends on the type of the setting once it's bound
    | UseSetting of string

    /// Toggle the setting value off.  How the toggle works depends on the type of the setting
    | ToggleOffSetting of string

    /// Invert the setting
    | InvertSetting of string

    /// Reset the setting to it's default value
    | ResetSetting of string

    /// Set the setting to the specified value
    | AssignSetting of string * string

    /// Add the value to the setting
    | AddSetting of string * string

    /// Multiply the value of the setting with the value
    | MultiplySetting of string * string 

    /// Subtracte the value of the setting with the value
    | SubtractSetting of string * string

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

    /// Change the current directory to the given value
    | ChangeDirectory of string option

    /// Change the current directory for the local window
    | ChangeLocalDirectory of string option

    /// Clear out the keyboard map for the given modes
    | ClearKeyMap of KeyRemapMode list

    /// The :close command.  The bool value represents whether or not the 
    /// bang modifier was added
    | Close of bool

    /// The :edit command.  The values range as follows
    ///  - ! option present
    ///  - The provided ++opt
    ///  - The provided +cmd 
    ///  - The provided file to edit 
    | Edit of bool * FileOption list * CommandOption option * string

    /// The :delete command 
    | Delete of LineRange option * RegisterName option * int option 

    /// Display the contents of registers.  Unless a specific register name is 
    /// given all registers will be displayed
    | DisplayRegisters of RegisterName option

    /// Display the specified marks.  If no Mark values are provided then display 
    /// all marks
    | DisplayMarks of Mark list

    /// Display the keymap for the given modes.  Restrict the display to the provided
    /// key notation if it's provided
    | DisplayKeyMap of KeyRemapMode list * string option

    /// Fold the selected LineRange
    | Fold of LineRange option

    /// Go to the first tab 
    | GoToFirstTab

    /// Go to the last tab
    | GoToLastTab

    /// Go to the next tab
    | GoToNextTab of int option

    /// Go to the previous tab
    | GoToPreviousTab of int option

    /// Join the lines in the specified range.  Optionally provides a count of lines to 
    /// start the join after the line range
    | Join of LineRange option * JoinKind * int option

    /// Jump to the specified line number 
    | JumpToLine of int

    /// Jump to the last line of the ITextBuffer
    | JumpToLastLine

    /// Make command.  The options are as follows
    ///   - The ! option
    ///   - All of the text after the !
    | Make of bool * string

    /// Map the key notation in the given modes.  The bool is whether or not the right
    /// notation can allow remapping
    | MapKeys of string * string * KeyRemapMode list * bool

    /// Temporarily disable the 'hlsearch' option
    | NoHighlightSearch

    /// Print out the current directory
    | PrintCurrentDirectory

    /// Put the contents of the given register after the line identified by the
    /// LineRange (defaults to current)
    | PutAfter of LineRange option * RegisterName option

    /// Put the contents of the given register before the line identified by the
    /// LineRange (defaults to current)
    | PutBefore of LineRange option * RegisterName option

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

    /// Read the contents of the specified file and put it after the specified
    /// line range or the caret
    | ReadFile of LineRange option * FileOption list * string

    /// Read the contens of the specified command and put it after the specified
    /// line range or the caret
    | ReadCommand of LineRange option * string

    /// Redo the last item on the undo stack
    | Redo

    /// Retab the specified LineRange.  The options are as follows
    ///  - The LineRange to change (defaults to entire buffer)
    ///  - True to replace both tabs and spaces, false for just spaces
    ///  - new tabstop value
    | Retab of LineRange option * bool * int option

    /// Process the 'set' command
    | Set of SetArgument list

    /// Process the '/' and '?' commands
    | Search of LineRange option * Path * string

    /// Process the '<' shift left command
    | ShiftLeft of LineRange option * int option

    /// Process the '>' shift right command
    | ShiftRight of LineRange option * int option

    /// Process the 'source' command.  
    | Source of bool * string

    /// Process the 'split' command.  The values range as follows
    ///  - Height of the window if specified.  Expressed as a range.  The actual documentation
    ///    doesn't specify a range can be used here but usage indicates it can
    ///  - The provided ++opt
    ///  - The provided +cmd
    | Split of LineRange option * FileOption list * CommandOption option

    /// The :substitute command.  The argument order is range, pattern, replace,
    /// substitute flags and count
    | Substitute of LineRange option * string * string * SubstituteFlags * int option

    /// The variant of the :substitute command which repeats the last :subsitute with
    /// different flags and count
    | SubstituteRepeat of LineRange option * SubstituteFlags * int option

    /// Undo the last change
    | Undo

    /// Unmap the key notation in the given modes
    | UnmapKeys of string * KeyRemapMode list

    /// Write the 
    ///  - The line range to write out
    ///  - Whether or not a ! was provided
    ///  - The provided ++opt
    ///  - The file name to write to
    | Write of LineRange option * bool * FileOption list * string option

    /// Write out all changed buffers
    | WriteAll of bool

    /// Yank the line range into the given register with the specified count
    | Yank of LineRange option * RegisterName option * int option
