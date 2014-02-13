#light

namespace Vim.Interpreter
open EditorUtils
open Microsoft.VisualStudio.Text
open Vim

/// Represents the scope that a name is attached to.  A "let" name for example can 
/// be attached to various scopes
[<RequireQualifiedAccess>]
type NameScope =
    | Global
    | Buffer
    | Window
    | Tab
    | Script
    | Function
    | Vim

type VariableName = { 
    NameScope : NameScope
    Name : string 
}

[<RequireQualifiedAccess>]
type VariableType =
    | Number
    | Float
    | String
    | FunctionRef
    | List
    | Dictionary
    | Error

[<RequireQualifiedAccess>]
type VariableValue =
    | Number of int
    | Float of float
    | String of string
    | FunctionRef of string
    | List of VariableValue list
    | Dictionary of Map<string, VariableValue>
    | Error

    with

    // TODO: Determine the appropriate values for List, Dictionary and Error
    member x.StringValue =
        match x with
        | Number number -> number.ToString()
        | Float number -> number.ToString()
        | String str -> str
        | FunctionRef name -> name
        | List _ -> "<list>"
        | Dictionary _  -> "<dictionary>"
        | Error -> "<error>"

    member x.VariableType = 
        match x with
        | Number _ -> VariableType.Number
        | Float _ -> VariableType.Float
        | String _ -> VariableType.String
        | FunctionRef _ -> VariableType.FunctionRef
        | List _ -> VariableType.List
        | Dictionary _ -> VariableType.Dictionary
        | Error -> VariableType.Error

type VariableMap = System.Collections.Generic.Dictionary<string, VariableValue>

/// The set of events Vim supports.  Defined in ':help autocmd-events'
///
/// Right now we only support a very limited set of autocmd events.  Enough to 
/// alter ts, sw, etc ... when a new file is created 
[<RequireQualifiedAccess>]
type EventKind = 
    | BufNewFile
    | BufReadPre
    | BufRead
    | BufReadPost
    | BufReadCmd
    | FileReadPre
    | FileReadPost
    | FileReadCmd
    | FilterReadPre
    | FilterReadPost
    | StdinReadPre
    | StdinReadPost
    | BufWrite
    | BufWritePre
    | BufWritePost
    | BufWriteCmd
    | FileWritePre
    | FileWritePost
    | FileWriteCmd
    | FileAppendPre
    | FileAppendPost
    | FileAppendCmd
    | FilterWritePre
    | FilterWritePost
    | BufAdd
    | BufCreate
    | BufDelete
    | BufWipeout
    | BufFilePre
    | BufFilePost
    | BufEnter
    | BufLeave
    | BufWinEnter
    | BufWinLeave
    | BufUnload
    | BufHidden
    | BufNew
    | SwapExists
    | FileType
    | Syntax
    | EncodingChanged
    | TermChanged
    | VimEnter
    | GUIEnter
    | TermResponse
    | VimLeavePre
    | VimLeave
    | FileChangedShell
    | FileChangedShellPost
    | FileChangedRO
    | ShellCmdPost
    | ShellFilterPost
    | FuncUndefined
    | SpellFileMissing
    | SourcePre
    | SourceCmd
    | VimResized
    | FocusGained
    | FocusLost
    | CursorHold
    | CursorHoldI
    | CursorMoved
    | CursorMovedI
    | WinEnter
    | WinLeave
    | TabEnter
    | TabLeave
    | CmdwinEnter
    | CmdwinLeave
    | InsertEnter
    | InsertChange
    | InsertLeave
    | ColorScheme
    | RemoteReply
    | QuickFixCmdPre
    | QuickFixCmdPost
    | SessionLoadPost
    | MenuPopup
    | User

[<RequireQualifiedAccess>]
[<NoComparison>]
[<StructuralEquality>]
type AutoCommandGroup = 
    | Default
    | Named of string 

type AutoCommand = {
    Group : AutoCommandGroup

    EventKind : EventKind

    LineCommandText : string

    Pattern : string
}    

type AutoCommandDefinition = { 
    Group : AutoCommandGroup

    EventKinds : EventKind list

    LineCommandText : string

    Patterns : string list
}

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

/// Data for the :call command
type CallInfo = {
    Name : string
    Arguments : string
    LineRange : LineRangeSpecifier
}

type FunctionDefinition = {

    /// Name of the function
    Name : string

    /// Arguments to the function
    Arguments : string list

    /// Is the function responsible for its ranges
    IsRange : bool

    /// Is the function supposed to abort on the first error
    IsAbort : bool

    /// Is the function intended to be invoked from a dictionary
    IsDictionary : bool

    /// Is this a forced definition of the function
    IsForced : bool

    /// Is this a script local function (begins with s:)
    IsScriptLocal : bool
}

/// Represents the values or the '+cmd' which can occur on commands like :edit
[<RequireQualifiedAccess>]
type CommandOption =
    | StartAtLastLine
    | StartAtLine of int
    | StartAtPattern of string
    | ExecuteLineCommand of LineCommand

and Function = {

    /// The definition of the function 
    Definition : FunctionDefinition

    // Line commands that compose the function
    LineCommands : LineCommand list
}

/// The ConditionalBlock type is used to represent if / else if / else blocks
/// of commands.  
and ConditionalBlock = {

    /// The conditional which must be true in order for the LineCommand list
    /// to be executed.  If there is no condition then the LineCommand list 
    /// is unconditionally executed
    Conditional : Expression option

    /// The LineCommand values that make up this conditional block
    LineCommands : LineCommand list

}

with
    static member Empty = { Conditional = None; LineCommands = List.Empty }

and [<RequireQualifiedAccess>] Expression =

    /// Binary expression
    | Binary of BinaryKind * Expression * Expression

    /// A constant value
    | ConstantValue of VariableValue 

and [<RequireQualifiedAccess>] LineCommand =

    /// Add a new AutoCommand to the set of existing AutoCommand values
    | AddAutoCommand of AutoCommandDefinition

    /// The :behave command to set common behaviors in certain environments
    | Behave of string

    /// The :call command to invoke a function.  The first string is the 
    | Call of CallInfo

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
    /// source and the second is the destination.  The last entry is an optional count
    /// which can be specified
    | CopyTo of LineRangeSpecifier * LineRangeSpecifier * int option

    /// Delete the specified marks
    | DeleteMarks of Mark list

    /// Delete all of the marks except A-Z and 0-9
    | DeleteAllMarks 

    /// Move the specific line range to the given position.  The first line range is the 
    /// source and the second is the destination
    | MoveTo of LineRangeSpecifier * LineRangeSpecifier * int option

    /// The :delete command
    | Delete of LineRangeSpecifier * RegisterName option

    /// The beginning of a function definition.  When 'None' is present that means there was
    /// an error parsing the function definition.
    | FunctionStart of FunctionDefinition option

    /// The :endfunc member
    | FunctionEnd

    /// A complete function 
    | Function of Function

    /// Display the contents of registers.  Unless a specific register name is 
    /// given all registers will be displayed
    | DisplayRegisters of RegisterName option

    /// Display the specified marks.  If no Mark values are provided then display 
    /// all marks
    | DisplayMarks of Mark list

    /// Display the keymap for the given modes.  Restrict the display to the provided
    /// key notation if it's provided
    | DisplayKeyMap of KeyRemapMode list * string option

    /// Display the specified let value
    | DisplayLet of VariableName list

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

    /// The if command
    | If of ConditionalBlock list

    /// The :if definition command
    | IfStart of Expression

    /// The :endif definition
    | IfEnd

    /// The :else definition 
    | Else

    /// The :elseif definition 
    | ElseIf of Expression

    /// Join the lines in the specified range.  Optionally provides a count of lines to 
    /// start the join after the line range
    | Join of LineRangeSpecifier * JoinKind

    /// Jump to the specified line number 
    | JumpToLine of int

    /// Jump to the last line of the ITextBuffer
    | JumpToLastLine

    // Let command.  The first item is the name and the second is the value
    | Let of VariableName * VariableValue

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

    /// There was a parse error on the specified line
    | ParseError of string

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

    /// Remove all auto commands with the specified definition
    | RemoveAutoCommands of AutoCommandDefinition

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

    /// Unlet a value.  
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

with 

    member x.Succeeded = 
        match x with
        | ParseError _ -> false
        | _ -> true

    member x.Failed = 
        not x.Succeeded

/// Engine which interprets Vim commands and expressions
type IVimInterpreter =

    /// Get the ITextSnapshotLine for the provided LineSpecifier if it's 
    /// applicable
    abstract GetLine : lineSpecifier : LineSpecifier -> ITextSnapshotLine option

    /// Get the specified LineRange in the IVimBuffer
    abstract GetLineRange : lineRange : LineRangeSpecifier -> SnapshotLineRange option

    /// Run the LineCommand
    abstract RunLineCommand : lineCommand : LineCommand -> RunResult

    /// Run the Expression
    abstract RunExpression : expression : Expression -> VariableValue

    /// Run the given script 
    abstract RunScript : lines : string[] -> unit

