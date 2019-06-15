#light

namespace Vim.Interpreter
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
    NameScope: NameScope
    Name: string 
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
    | Number of Number: int
    | Float of Float: float
    | String of String: string
    | FunctionRef of FunctionName: string
    | List of VariableValues: VariableValue list
    | Dictionary of Map: Map<string, VariableValue>
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

[<RequireQualifiedAccess>]
type EvaluateResult = 
    | Succeeded of Value: VariableValue
    | Failed of Failed: string

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
    | Named of Name: string 

type AutoCommand = {
    Group: AutoCommandGroup

    EventKind: EventKind

    LineCommandText: string

    Pattern: string
}    

type AutoCommandDefinition = { 
    Group: AutoCommandGroup

    EventKinds: EventKind list

    LineCommandText: string

    Patterns: string list
}

/// A single line specifier in a range 
[<RequireQualifiedAccess>]
type LineSpecifier = 

    /// The line with the specified number
    | Number of Number: int

    /// The current line: '.'
    | CurrentLine

    /// The last line: '$'
    | LastLine

    /// The line containing the specified mark
    | MarkLine of Mark: Mark

    /// The next line where the given pattern matches
    | NextLineWithPattern of Pattern: string

    /// The next line where the previous search pattern occurs
    | NextLineWithPreviousPattern

    /// The next line where the previous substitute pattern occurs
    | NextLineWithPreviousSubstitutePattern

    /// The previous line where the given pattern matches
    | PreviousLineWithPattern of Pattern: string

    /// The previous line where the previous search pattern occurs
    | PreviousLineWithPreviousPattern

    /// LineSpecifier with the given line adjustment
    | LineSpecifierWithAdjustment of LineSpecifier: LineSpecifier * Adjustment: int

/// A line range in the file 
[<RequireQualifiedAccess>]
type LineRangeSpecifier = 

    /// There is no specifier
    | None

    /// The entire buffer: '%'
    | EntireBuffer 

    /// A single line range
    | SingleLine of LineSpecifier: LineSpecifier

    /// A range defined by two lines.  The bool is whether or not the cursor should be
    /// adjusted for the for the second line specifier (true when ';' is used to separate
    /// the LineSpecifier values)
    | Range of StartLineSpecifier: LineSpecifier * LastLineSpecifier: LineSpecifier * AdjustCaret: bool

    /// The range is an end count on top of another LineRange value.  It's possible for the 
    /// end count to exist in the abscence a range
    | WithEndCount of LineRangeSpecifier: LineRangeSpecifier * Count: int

    /// The LineRange value for Join is heavily special cased
    | Join of LineRangeSpecifier: LineRangeSpecifier * Count: int option

/// Represents the values for '++opt' which can occur on commands like :edit
[<RequireQualifiedAccess>]
type FileOption =
    | FileFormat of FileFormat: string
    | Encoding of Encoding: string
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
    | DisplaySetting of SettingName: string

    /// The 'all&' argument.  Resets all setting to their default value
    | ResetAllToDefault

    /// Use the setting.  Produced when an setting name is used without arguments.  The behavior 
    /// depends on the type of the setting once it's bound
    | UseSetting of SettingName: string

    /// Toggle the setting value off.  How the toggle works depends on the type of the setting
    | ToggleOffSetting of SettingName: string

    /// Invert the setting
    | InvertSetting of SettingName: string

    /// Reset the setting to it's default value
    | ResetSetting of SettingName: string

    /// Set the setting to the specified value
    | AssignSetting of SettingName: string * Value: string

    /// Add the value to the setting
    | AddSetting of SettingName: string * Value: string

    /// Multiply the value of the setting with the value
    | MultiplySetting of SettingName: string * Value: string 

    /// Subtracte the value of the setting with the value
    | SubtractSetting of SettingName: string * Value: string

[<RequireQualifiedAccess>]
type BinaryKind = 
    | Add
    | Concatenate
    | Divide
    | Multiply
    | Modulo
    | Subtract
    | GreaterThan
    | LessThan
    | Equal
    | NotEqual

/// Data for the :call command
type CallInfo = {
    Name: string
    Arguments: string
    LineRange: LineRangeSpecifier
    IsScriptLocal: bool
}

type FunctionDefinition = {

    /// Name of the function
    Name: string

    /// Arguments to the function
    Parameters: string list

    /// Is the function responsible for its ranges
    IsRange: bool

    /// Is the function supposed to abort on the first error
    IsAbort: bool

    /// Is the function intended to be invoked from a dictionary
    IsDictionary: bool

    /// Is this a forced definition of the function
    IsForced: bool

    /// Is this a script local function (begins with s:)
    IsScriptLocal: bool
}

/// See :help filename-modifiers
[<RequireQualifiedAccess>]
type FileNameModifier =
    /// :e
    | Extension
    /// :h
    | Head
    /// :p
    | PathFull
    /// :r
    | Root
    /// :t
    | Tail

    member x.Char =
        match x with
        | Extension -> 'e'
        | Head -> 'h'
        | PathFull -> 'p'
        | Root -> 'r'
        | Tail -> 't'

    static member OfChar c =
        match c with
        | 'e' -> Some FileNameModifier.Extension
        | 'h' -> Some FileNameModifier.Head
        | 'p' -> Some FileNameModifier.PathFull
        | 'r' -> Some FileNameModifier.Root
        | 't' -> Some FileNameModifier.Tail
        | _ -> None

[<RequireQualifiedAccess>]
type SymbolicPathComponent =
    /// '%' + modifiers
    | CurrentFileName of FileNameModifiers: FileNameModifier list
    /// '#'[number] + modifiers
    | AlternateFileName of Count: int * FileNameModifiers: FileNameModifier list
    /// Literal text
    | Literal of Literal: string

type SymbolicPath = SymbolicPathComponent list

/// Represents the values or the '+cmd' which can occur on commands like :edit
[<RequireQualifiedAccess>]
type CommandOption =
    | StartAtLastLine
    | StartAtLine of LineNumber: int
    | StartAtPattern of Pattern: string
    | ExecuteLineCommand of LineCommand: LineCommand

and Function = {

    /// The definition of the function 
    Definition: FunctionDefinition

    // Line commands that compose the function
    LineCommands: LineCommand list
}

/// The ConditionalBlock type is used to represent if / else if / else blocks
/// of commands.  
and ConditionalBlock = {

    /// The conditional which must be true in order for the LineCommand list
    /// to be executed.  If there is no condition then the LineCommand list 
    /// is unconditionally executed
    Conditional: Expression option

    /// The LineCommand values that make up this conditional block
    LineCommands: LineCommand list

}

with
    static member Empty = { Conditional = None; LineCommands = List.Empty }

and [<RequireQualifiedAccess>] Expression =

    /// Binary expression
    | Binary of BinaryKind: BinaryKind * Left: Expression * Right: Expression

    /// A constant value
    | ConstantValue of Value: VariableValue 

    /// The name of an option/setting
    | OptionName of OptionName: string

    /// The name of a register
    | RegisterName of RegisterName: RegisterName

    /// The name of a variable
    | VariableName of VariableName: VariableName

    /// The name of an environment variable
    | EnvironmentVariableName of EnvironmentVariableName: string

    /// Invocation of a function
    | FunctionCall of VariableName: VariableName * Arguments: Expression list

    /// List of expressions
    | List of Expressions: Expression list

and [<RequireQualifiedAccess>] LineCommand =

    /// Add a new AutoCommand to the set of existing AutoCommand values
    | AddAutoCommand of AutoCommandDefinition: AutoCommandDefinition

    /// The :behave command to set common behaviors in certain environments
    | Behave of Text: string

    /// The :call command to invoke a function.  The first string is the 
    | Call of CallInfo: CallInfo

    /// Change the current directory to the given value
    | ChangeDirectory of SymbolicPath: SymbolicPath

    /// Change the current directory for the local window
    | ChangeLocalDirectory of SymbolicPath: SymbolicPath

    /// Clear out the keyboard map for the given modes
    | ClearKeyMap of KeyRemapModes: KeyRemapMode list * KeyMapArguments: KeyMapArgument list

    /// The :close command.  The bool value represents whether or not the 
    /// bang modifier was added
    | Close of HasBang: bool

    /// Compose two line commands
    | Compose of First: LineCommand * Second: LineCommand

    /// Copy the specific line range to the given position.  The first line range is the 
    /// source and the second is the destination.  The last entry is an optional count
    /// which can be specified
    | CopyTo of Source: LineRangeSpecifier * Destination: LineRangeSpecifier * Count: int option

    ///  The :csx command to run C# script.
    | CSharpScript of CallInfo: CallInfo

    ///  The :csxe command to run C# script and create each time.
    | CSharpScriptCreateEachTime of CallInfo: CallInfo

    /// Delete the specified marks
    | DeleteMarks of Marks: Mark list

    /// Delete all of the marks except A-Z and 0-9
    | DeleteAllMarks 

    /// Move the specific line range to the given position.  The first line range is the 
    /// source and the second is the destination
    | MoveTo of Source: LineRangeSpecifier * Destination: LineRangeSpecifier * Count: int option

    /// The :delete command
    | Delete of LineRangeSpecifier: LineRangeSpecifier * RegisterName: RegisterName option

    /// The beginning of a function definition.  When 'None' is present that means there was
    /// an error parsing the function definition.
    | FunctionStart of FunctionDefinition: FunctionDefinition option

    /// The :endfunc member
    | FunctionEnd

    /// A complete function 
    | Function of Function: Function

    /// Add the specified digraphs to the digraph mapping
    | Digraphs of Digraphs: (char * char * int) list

    /// Display the contents of registers.  Unless a specific register name is 
    /// given all registers will be displayed
    | DisplayRegisters of RegisterNames: RegisterName list

    /// Display the specified marks.  If no Mark values are provided then display 
    /// all marks
    | DisplayMarks of Marks: Mark list

    /// Display the keymap for the given modes.  Restrict the display to the provided
    /// key notation if it's provided
    | DisplayKeyMap of KeyRemapModes: KeyRemapMode list * Notation: string option

    /// Display the specified let value
    | DisplayLet of VariableNames: VariableName list

    /// Display the specified line range with the specified flags
    | DisplayLines of LineRangeSpecifier: LineRangeSpecifier * LineCommandFlags: LineCommandFlags

    // The :echo command
    | Echo of Expression: Expression

    // The :execute command
    | Execute of Expression: Expression

    /// The :edit command.  The values range as follows
    ///  - ! option present
    ///  - The provided ++opt
    ///  - The provided +cmd 
    ///  - The provided file to edit 
    | Edit of HasBang: bool * FileOptions: FileOption list * CommandOptions: CommandOption option * SymbolicPath: SymbolicPath

    /// List recent files
    | Files

    /// Fold the selected LineRange
    | Fold of LineRangeSpecifier: LineRangeSpecifier

    /// Run the given command against all lines in the specified range (default is the 
    /// entire buffer) which match the predicate pattern.  If the bool provided is false
    /// then it will be run on the lines which don't match the pattern
    | Global of LineRangeSpecifier: LineRangeSpecifier * Pattern: string * MatchPattern: bool * LineCommand: LineCommand

    // Executes the given key strokes as if they were typed in normal mode
    | Normal of LineRangeSpecifier: LineRangeSpecifier * KeyInputs: KeyInput list

    /// Go to the first tab 
    | GoToFirstTab

    /// Go to the last tab
    | GoToLastTab

    /// Go to the next tab
    | GoToNextTab of Count: int option

    /// Go to the previous tab
    | GoToPreviousTab of Count: int option

    /// Get help on VsVim
    | Help of Subject: string

    /// Get help on Vim
    | VimHelp of Subject: string

    /// Print out the default history 
    | History

    /// Run a host command.  The first string is the command and the second string is the argument
    | HostCommand of HasBang: bool * Command: string * Argument: string

    /// Process the 'split' command.  The values range as follows
    ///  - Height of the window if specified.  Expressed as a range.  The actual documentation
    ///    doesn't specify a range can be used here but usage indicates it can
    ///  - The provided ++opt
    ///  - The provided +cmd
    | HorizontalSplit of LineRangeSpecifier: LineRangeSpecifier * FileOptiosn: FileOption list * CommandOption: CommandOption option

    /// The if command
    | If of Blocks: ConditionalBlock list

    /// The :if definition command
    | IfStart of Expression: Expression

    /// The :endif definition
    | IfEnd

    /// The :else definition 
    | Else

    /// The :elseif definition 
    | ElseIf of Expression: Expression

    /// Join the lines in the specified range.  Optionally provides a count of lines to 
    /// start the join after the line range
    | Join of LineRangeSpecifier: LineRangeSpecifier * JoinKind: JoinKind

    /// Jump to the last line of the specified line range
    | JumpToLastLine of LineRangeSpecifier: LineRangeSpecifier

    // Let command.  The first item is the name and the second is the value
    | Let of VariableName: VariableName * Expression: Expression

    // Let command applied to an environment variable. The first item is the name and the second is the value
    | LetEnvironment of EnvironmentVariableName: string * Expression: Expression

    // Let command applied to a register. The first item is the name and the second is the value
    | LetRegister of RegisterName: RegisterName * Expression: Expression

    /// Make command.  The options are as follows
    ///   - The ! option
    ///   - All of the text after the !
    | Make of HasBang: bool * Text: string

    /// Map the key notation in the given modes.  The bool is whether or not the right
    /// notation can allow remapping
    | MapKeys of LeftKeyNotation: string * RightKeyNotation: string * KeyRemapModes: KeyRemapMode list * AllowRemap: bool * KeyMapArguments: KeyMapArgument list

    /// Temporarily disable the 'hlsearch' option
    | NoHighlightSearch

    /// This is a line command that does nothing on execution
    | Nop

    // Close all windows but this one in the current tab
    | Only

    /// There was a parse error on the specified line
    | ParseError of Error: string

    /// Print out the current directory
    | PrintCurrentDirectory

    /// Put the contents of the given register after the line identified by the
    /// LineRange (defaults to current)
    | PutAfter of LineRangeSpecifier: LineRangeSpecifier * RegisterName: RegisterName option

    /// Put the contents of the given register before the line identified by the
    /// LineRange (defaults to current)
    | PutBefore of LineRangeSpecifier: LineRangeSpecifier * RegisterName: RegisterName option

    /// Display the quickfix window
    | QuickFixWindow

    /// Next item in the quickfix list
    | QuickFixNext of Count: int option * HasBang: bool

    /// Previous item in the quickfix list
    | QuickFixPrevious of Count: int option * HasBang: bool

    /// Go to item in the quickfix list
    | QuickFixRewind of Number: int option * DefaultToLast: bool * HasBang: bool

    /// Display the location window
    | LocationWindow

    /// Next item in the location list
    | LocationNext of Count: int option * HasBang: bool

    /// Previous item in the location list
    | LocationPrevious of Count: int option * HasBang: bool

    /// Go to item in the location list
    | LocationRewind of Number: int option * DefaultToLast: bool * HasBang: bool

    /// Quit the curren window without writing it's content.  If the boolean option
    /// is present (for !) then don't warn about a dirty window
    | Quit of HasBang: bool

    /// Quit all windows without writing their content and exit Vim.  If the boolean
    /// option is present then don't warn about writing a dirty window
    | QuitAll of HasBang: bool

    /// Quit the current window after writing out it's contents.  The values range as 
    /// follows
    ///  - Range of lines to write.  All if no option present
    ///  - ! option present
    ///  - The provided ++opt
    ///  - The provided +cmd
    | QuitWithWrite of LineRangeSpecifier: LineRangeSpecifier * HasBang: bool * FileOptions: FileOption list * FilePath: string option 

    /// Read the contents of the specified file and put it after the specified
    /// line range or the caret
    | ReadFile of LineRangeSpecifier: LineRangeSpecifier * FileOptions: FileOption list * FilePath: string

    /// Read the contens of the specified command and put it after the specified
    /// line range or the caret
    | ReadCommand of LineRangeSpecifier: LineRangeSpecifier * CommandText: string

    /// Redo the last item on the undo stack
    | Redo

    /// Remove all auto commands with the specified definition
    | RemoveAutoCommands of AutoCommandDefinition: AutoCommandDefinition

    /// Retab the specified LineRange.  The options are as follows
    ///  - The LineRange to change (defaults to entire buffer)
    ///  - True to replace both tabs and spaces, false for just spaces
    ///  - new tabstop value
    | Retab of LineRangeSpecifier: LineRangeSpecifier * HasBang: bool * TabStop: int option

    /// Process the 'set' command
    | Set of SetArguments: SetArgument list

    /// Process the '/' and '?' commands
    | Search of LineRangeSpecifier: LineRangeSpecifier * SearchPath: SearchPath * Pattern: string

    /// Start a shell window
    | Shell

    /// Filter the given line range through shell command
    | ShellCommand of LineRangeSpecifier: LineRangeSpecifier * CommandText: string

    /// Process the '<' shift left command
    | ShiftLeft of LineRangeSpecifier: LineRangeSpecifier * Count: int

    /// Process the '>' shift right command
    | ShiftRight of LineRangeSpecifier: LineRangeSpecifier * Count: int

    /// Sort the specified LineRange.  The options are as follows:
    /// - The LineRange to change (defaults to entire buffer)
    /// - True to reverse sort
    /// - sort flags
    /// - optional pattern
    | Sort of LineRangeSpecifier: LineRangeSpecifier * HasBang: bool * SortFlags: SortFlags * Pattern: string option

    /// Process the 'source' command.  
    | Source of HasBang: bool * FilePath: string

    /// Stop insert mode as soon as possible
    | StopInsert

    // Close all tabs but this one
    | TabOnly

    /// Process the 'tabnew' / 'tabedit' commands.  The optional string represents the file path 
    | TabNew of SymbolicPath: SymbolicPath

    /// The version command
    | Version

    /// Search for pattern in the specified files
    | VimGrep of HasBang: bool * Pattern: string * OneMatchPerFile: bool * JumpToFirst: bool * FilePattern: string

    /// Process the 'vsplit' command. Values are as per HorizontalSplit
    | VerticalSplit of LineRangeSpecifier: LineRangeSpecifier * FileOptions: FileOption list * CommandOption: CommandOption option

    /// The :substitute command.  The argument order is range, pattern, replace,
    /// substitute flags and count
    | Substitute of LineRangeSpecifier: LineRangeSpecifier * Pattern: string * Replace: string * SubstituteFlags: SubstituteFlags

    /// The variant of the :substitute command which repeats the last :subsitute with
    /// different flags and count
    | SubstituteRepeat of LineRangeSpecifier: LineRangeSpecifier * SubstituteFlags: SubstituteFlags

    /// Undo the last change
    | Undo

    /// Unlet a value.  
    | Unlet of HasBang: bool * Names: string list

    /// Unmap the key notation in the given modes
    | UnmapKeys of KeyNotation: string * KeyRemapModes: KeyRemapMode list * KeyMapArguments: KeyMapArgument list

    /// Write the 
    ///  - The line range to write out
    ///  - Whether or not a ! was provided
    ///  - The provided ++opt
    ///  - The file name to write to
    | Write of LineRangeSpecifier: LineRangeSpecifier * HasBang: bool * FileOptions: FileOption list * FilePath: string option

    /// Write out all changed buffers
    | WriteAll of HasBang: bool * Quit: bool

    /// Yank the line range into the given register with the specified count
    | Yank of LineRangeSpecifier: LineRangeSpecifier * RegisterNames: RegisterName option * Count: int option

with 

    member x.Succeeded = 
        match x with
        | ParseError _ -> false
        | _ -> true

    member x.Failed = 
        not x.Succeeded

[<NoComparison>]
[<NoEquality>]
[<RequireQualifiedAccess>]
type BuiltinFunctionCall =
    | Escape of Value: string * EscapeCharacters: string
    | Exists of Name: string
    | Localtime
    | Nr2char of Nr: int

/// Engine which interprets Vim commands and expressions
type IVimInterpreter =

    /// Get the ITextSnapshotLine for the provided LineSpecifier if it's 
    /// applicable
    abstract GetLine: lineSpecifier: LineSpecifier -> ITextSnapshotLine option

    /// Get the specified LineRange in the IVimBuffer
    abstract GetLineRange: lineRange: LineRangeSpecifier -> SnapshotLineRange option

    /// Run the LineCommand
    abstract RunLineCommand: lineCommand: LineCommand -> unit

    /// Run the Expression
    abstract RunExpression: expression: Expression -> VariableValue

    /// Evaluate the text as an expression and return its value
    abstract EvaluateExpression: text: string -> EvaluateResult

    /// Run the given script 
    abstract RunScript: lines: string[] -> unit

