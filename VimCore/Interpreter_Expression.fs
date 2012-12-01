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
type LineRangeSpecifier = 

    /// There is no specifier
    | None

    /// The entire buffer: '%'
    | EntireBuffer 

    /// A single line range
    | SingleLine of LineSpecifier

    /// A range defined by two lines.  The bool is whether or not the cursor should be
    /// adjusted for the for the second line specifier (true when ';' is used to separate
    /// the LineSpecifier values)
    | Range of LineSpecifier * LineSpecifier * bool

    /// The range is an end count on top of another LineRange value.  It's possible for the 
    /// end count to exist in the abscence a range
    | WithEndCount of LineRangeSpecifier * int option

    /// The LineRange value for Join is heavily special cased
    | Join of LineRangeSpecifier * int option

/// Represents the values for '++opt' which can occur on commands like :edit
[<RequireQualifiedAccess>]
type FileOption =
    | FileFormat of string
    | Encoding of string
    | Binary 
    | NoBinary
    | Bad
    | Edit

/// Arguments which can be passed to the mapping functions
[<RequireQualifiedAccess>]
type KeyMapArgument = 
    | Buffer
    | Silent
    | Special
    | Script
    | Expr
    | Unique

/// The ex-flags value
[<RequireQualifiedAccess>]
type LineCommandFlags = 
    | None = 0
    | List = 0x1
    | AddLineNumber = 0x2
    | Print = 0x4

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

[<RequireQualifiedAccess>]
type BinaryKind = 
    | Add
    | Concatenate
    | Divide
    | Multiply
    | Modulo
    | Subtract

/// Represents te values or the '+cmd' which can occur on commads like :edit
[<RequireQualifiedAccess>]
type CommandOption =
    | StartAtLastLine
    | StartAtLine of int
    | StartAtPattern of string
    | ExecuteLineCommand of LineCommand

and [<RequireQualifiedAccess>] Expression =

    /// Binary expression
    | Binary of BinaryKind * Expression * Expression

    /// A constant value
    | ConstantValue of VariableValue 

and [<RequireQualifiedAccess>] LineCommand =

    /// The :behave command to set common behaviors in certain environments
    | Behave of string

    /// Change the current directory to the given value
    | ChangeDirectory of string option

    /// Change the current directory for the local window
    | ChangeLocalDirectory of string option

    /// Clear out the keyboard map for the given modes
    | ClearKeyMap of KeyRemapMode list * KeyMapArgument list

    /// The :close command.  The bool value represents whether or not the 
    /// bang modifier was added
    | Close of bool

    /// Copy the specific line range to the given position.  The first line range is the 
    /// source and the second is the desitination.  The last entry is an optional count
    /// which can be specified
    | CopyTo of LineRangeSpecifier * LineRangeSpecifier * int option

    /// Move the specific line range to the given position.  The first line range is the 
    /// source and the second is the desitination
    | MoveTo of LineRangeSpecifier * LineRangeSpecifier * int option

    /// The :delete command
    | Delete of LineRangeSpecifier * RegisterName option

    /// Display the contents of registers.  Unless a specific register name is 
    /// given all registers will be displayed
    | DisplayRegisters of RegisterName option

    /// Display the specified marks.  If no Mark values are provided then display 
    /// all marks
    | DisplayMarks of Mark list

    /// Display the keymap for the given modes.  Restrict the display to the provided
    /// key notation if it's provided
    | DisplayKeyMap of KeyRemapMode list * string option

    /// The :edit command.  The values range as follows
    ///  - ! option present
    ///  - The provided ++opt
    ///  - The provided +cmd 
    ///  - The provided file to edit 
    | Edit of bool * FileOption list * CommandOption option * string

    /// Fold the selected LineRange
    | Fold of LineRangeSpecifier

    /// Run the given command against all lines in the specified range (default is the 
    /// entire buffer) which match the predicate pattern.  If the bool provided is false
    /// then it will be run on the lines which don't match the pattern
    | Global of LineRangeSpecifier * string * bool * LineCommand

    /// Go to the first tab 
    | GoToFirstTab

    /// Go to the last tab
    | GoToLastTab

    /// Go to the next tab
    | GoToNextTab of int option

    /// Go to the previous tab
    | GoToPreviousTab of int option

    /// Print out the default history 
    | History

    /// Process the 'split' command.  The values range as follows
    ///  - Height of the window if specified.  Expressed as a range.  The actual documentation
    ///    doesn't specify a range can be used here but usage indicates it can
    ///  - The provided ++opt
    ///  - The provided +cmd
    | HorizontalSplit of LineRangeSpecifier * FileOption list * CommandOption option

    /// Join the lines in the specified range.  Optionally provides a count of lines to 
    /// start the join after the line range
    | Join of LineRangeSpecifier * JoinKind

    /// Jump to the specified line number 
    | JumpToLine of int

    /// Jump to the last line of the ITextBuffer
    | JumpToLastLine

    // Let command.  The first item is the name and the second is the value
    | Let of string * VariableValue

    /// Make command.  The options are as follows
    ///   - The ! option
    ///   - All of the text after the !
    | Make of bool * string

    /// Map the key notation in the given modes.  The bool is whether or not the right
    /// notation can allow remapping
    | MapKeys of string * string * KeyRemapMode list * bool * KeyMapArgument list

    /// Temporarily disable the 'hlsearch' option
    | NoHighlightSearch

    /// This is a line command that does nothing on execution
    | Nop

    /// Print out the specified line range 
    | Print of LineRangeSpecifier * LineCommandFlags

    /// Print out the current directory
    | PrintCurrentDirectory

    /// Put the contents of the given register after the line identified by the
    /// LineRange (defaults to current)
    | PutAfter of LineRangeSpecifier * RegisterName option

    /// Put the contents of the given register before the line identified by the
    /// LineRange (defaults to current)
    | PutBefore of LineRangeSpecifier * RegisterName option

    /// Next error in the quick fix list.  int is for count and bool is for the bang option
    | QuickFixNext of int option * bool

    /// Previous error in the quick fix list.  int is for count and bool is for the bang option
    | QuickFixPrevious of int option * bool

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
    | QuitWithWrite of LineRangeSpecifier * bool * FileOption list * string option 

    /// Read the contents of the specified file and put it after the specified
    /// line range or the caret
    | ReadFile of LineRangeSpecifier * FileOption list * string

    /// Read the contens of the specified command and put it after the specified
    /// line range or the caret
    | ReadCommand of LineRangeSpecifier * string

    /// Redo the last item on the undo stack
    | Redo

    /// Retab the specified LineRange.  The options are as follows
    ///  - The LineRange to change (defaults to entire buffer)
    ///  - True to replace both tabs and spaces, false for just spaces
    ///  - new tabstop value
    | Retab of LineRangeSpecifier * bool * int option

    /// Process the 'set' command
    | Set of SetArgument list

    /// Process the '/' and '?' commands
    | Search of LineRangeSpecifier * Path * string

    /// Execute the given shell command
    | ShellCommand of string

    /// Process the '<' shift left command
    | ShiftLeft of LineRangeSpecifier

    /// Process the '>' shift right command
    | ShiftRight of LineRangeSpecifier

    /// Process the 'source' command.  
    | Source of bool * string

    /// The version command
    | Version

    /// Process the 'vsplit' command. Values are as per HorizontalSplit
    | VerticalSplit of LineRangeSpecifier * FileOption list * CommandOption option

    /// The :substitute command.  The argument order is range, pattern, replace,
    /// substitute flags and count
    | Substitute of LineRangeSpecifier * string * string * SubstituteFlags

    /// The variant of the :substitute command which repeats the last :subsitute with
    /// different flags and count
    | SubstituteRepeat of LineRangeSpecifier * SubstituteFlags

    /// Undo the last change
    | Undo

    // Unlet a value.  
    | Unlet of bool * string list

    /// Unmap the key notation in the given modes
    | UnmapKeys of string * KeyRemapMode list * KeyMapArgument list

    /// Run a visual studio command (or really any custom host command).  The first string is the
    /// command and the second string in the argument
    | VisualStudioCommand of string * string

    /// Write the 
    ///  - The line range to write out
    ///  - Whether or not a ! was provided
    ///  - The provided ++opt
    ///  - The file name to write to
    | Write of LineRangeSpecifier * bool * FileOption list * string option

    /// Write out all changed buffers
    | WriteAll of bool

    /// Yank the line range into the given register with the specified count
    | Yank of LineRangeSpecifier * RegisterName option * int option
