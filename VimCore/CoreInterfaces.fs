#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open System.Diagnostics

type IStatusUtil =

    /// Raised when there is a special status message that needs to be reported
    abstract OnStatus : string -> unit

    /// Raised when there is a long status message that needs to be reported
    abstract OnStatusLong : string seq -> unit 

    /// Raised when there is an error message that needs to be reported
    abstract OnError : string -> unit 

/// Abstracts away VsVim's interaction with the file system to facilitate testing
type IFileSystem =

    /// Set of environment variables considered when looking for VimRC paths
    abstract EnvironmentVariables : list<string>

    /// Set of file names considered (in preference order) when looking for vim rc files
    abstract VimRcFileNames : list<string>
    
    /// Get the directories to probe for RC files
    abstract GetVimRcDirectories : unit -> seq<string>

    /// Get the file paths in preference order for vim rc files
    abstract GetVimRcFilePaths : unit -> seq<string>

    /// Attempts to load the contents of the .VimRC and return both the path the file
    /// was loaded from and it's contents a
    abstract LoadVimRc : unit -> (string * string[]) option

    /// Attempt to read all of the lines from the given file 
    abstract ReadAllLines : path:string -> string[] option

type ModeKind = 
    | Normal = 1
    | Insert = 2
    | Command = 3
    | VisualCharacter = 4
    | VisualLine = 5
    | VisualBlock = 6 

    // Mode when Vim is disabled via the user
    | Disabled = 42

/// The actual command name.  This is a wrapper over the collection of KeyInput 
/// values which make up a command name.  
///
/// The intent of this type is that two values are equal if the sequence of 
/// KeyInputs are Equal.  So a OneKeyInput can be equal to a ManyKeyInputs if the
/// have the same values
///
/// It is not possible to simple store this as a string as it is possible, and 
/// in fact likely due to certain virtual key codes which are unable to be mapped,
/// for KeyInput values will map to a single char.  Hence to maintain proper semantics
/// we have to use KeyInput values directly.
[<CustomEquality; CustomComparison>]
type CommandName =
    | EmptyName 
    | OneKeyInput of KeyInput
    | TwoKeyInputs of KeyInput * KeyInput
    | ManyKeyInputs of KeyInput list
    with 

    /// Get the list of KeyInput which represent this CommandName
    member x.KeyInputs =
        match x with 
        | EmptyName -> List.empty
        | OneKeyInput(ki) -> [ki]
        | TwoKeyInputs(k1,k2) -> [k1;k2]
        | ManyKeyInputs(list) -> list

    /// A string representation of the name.  It is unreliable to use this for anything
    /// other than display as two distinct KeyInput values can map to a single char
    member x.Name = x.KeyInputs |> Seq.map (fun ki -> ki.Char) |> StringUtil.ofCharSeq

    /// Add a KeyInput to the end of this CommandName and return the 
    /// resulting value
    member x.Add (ki) =
        match x with 
        | EmptyName -> OneKeyInput ki
        | OneKeyInput(previous) -> TwoKeyInputs(previous,ki)
        | TwoKeyInputs(p1,p2) -> ManyKeyInputs [p1;p2;ki]
        | ManyKeyInputs(list) -> ManyKeyInputs (list @ [ki])

    /// Does the name start with the given KeyInput
    member x.StartsWith target = 
        match x with 
        | EmptyName -> false
        | OneKeyInput(ki) -> target = ki
        | TwoKeyInputs(ki,_) -> target = ki
        | ManyKeyInputs(list) ->
            match ListUtil.tryHeadOnly list with
            | None -> false
            | Some(ki) -> target = ki

    override x.GetHashCode() = 
        match x with
        | EmptyName -> 1
        | OneKeyInput(ki) -> ki.GetHashCode()
        | TwoKeyInputs(k1,k2) -> k1.GetHashCode() ^^^ k2.GetHashCode()
        | ManyKeyInputs(list) -> 
            list 
            |> Seq.ofList
            |> Seq.map (fun ki -> ki.GetHashCode())
            |> Seq.sum

    override x.Equals(yobj) =
        match yobj with
        | :? CommandName as y -> 
            match x,y with
            | OneKeyInput(left),OneKeyInput(right) -> left = right
            | TwoKeyInputs(l1,l2),TwoKeyInputs(r1,r2) -> l1 = r1 && l2 = r2
            | _ -> ListUtil.contentsEqual x.KeyInputs y.KeyInputs
        | _ -> false

    override x.ToString() =
        x.KeyInputs
        |> Seq.map (fun ki ->
            if ki.Key = VimKey.NotWellKnownKey then ki.Char.ToString()
            else System.String.Format("<{0}>", ki.Key)  )
        |> StringUtil.ofStringSeq

    interface System.IComparable with
        member x.CompareTo yobj = 
            match yobj with
            | :? CommandName as y -> 
                let rec inner (left:KeyInput list) (right:KeyInput list) =
                    if left.IsEmpty && right.IsEmpty then 0
                    elif left.IsEmpty then -1
                    elif right.IsEmpty then 1
                    elif left.Head < right.Head then -1
                    elif left.Head > right.Head then 1
                    else inner (List.tail left) (List.tail right)
                inner x.KeyInputs y.KeyInputs
            | _ -> failwith "Cannot compare values of different types"


type ModeSwitch =
    | NoSwitch
    | SwitchMode of ModeKind
    | SwitchPreviousMode 
            
type CommandResult =   
    | Completed  of ModeSwitch
    | Error of string

type LongCommandResult =
    | Finished of CommandResult
    | Cancelled
    | NeedMoreInput of (KeyInput -> LongCommandResult)

/// Information about the kind of Command
type CommandKind =
    /// Relates to the movement of the cursor
    | Movement
    /// A Command which can be repeated
    | Repeatable
    /// A Command which cannot be repeated
    | NotRepeatable
    /// A Command which should not be considered when looking at last changes
    | Special

/// Representation of commands within Vim.  
type Command = 
    
    /// Represents a Command which has no motion modifiers.  The  delegate takes 
    /// an optional count and a Register.  If unspecified the default register
    /// will be used
    | SimpleCommand of CommandName * CommandKind * (int option -> Register -> CommandResult)

    /// Represents a Command prefix which has an associated motion.  The delegate takes
    /// an optional count, a Register and a MotionData value.  If unspecified the default
    /// register will be used
    | MotionCommand of CommandName * CommandKind * (int option -> Register -> MotionData -> CommandResult)

    /// Represents a command which has a Name but then has additional unspecified input
    /// which needs to be dealt with specially by the command.  These commands are not
    /// repeatable
    | LongCommand of CommandName * CommandKind * (int option -> Register -> LongCommandResult)

    with 

    /// The raw command inputs
    member x.CommandName = 
        match x with
        | SimpleCommand(value,_,_ ) -> value
        | MotionCommand(value,_,_) -> value
        | LongCommand(value,_,_) -> value

    /// The kind of the Command
    member x.CommandKind =
        match x with
        | SimpleCommand(_,value,_ ) -> value
        | MotionCommand(_,value,_) -> value
        | LongCommand(_,value,_) -> value

/// The information about the particular run of a Command
type CommandRunData = {
    Command : Command;
    Register : Register;
    Count : int option;
    MotionData : MotionData option;
}

/// Represents the types of MotionCommands which exist
type MotionCommand = 

    /// Simple motion which comprises of a single KeyInput and a function which given 
    /// a start point and count will produce the motion.  None is returned in the 
    /// case the motion is not valid
    | SimpleMotionCommand of CommandName * (SnapshotPoint -> int -> MotionData option)

    /// Complex motion commands take more than one KeyInput to complete.  For example 
    /// the f,t,F and T commands all require at least one additional input.  The bool
    /// in the middle of the tuple indicates whether or not the motion can be 
    /// used as a cursor movement operation  
    | ComplexMotionCommand of CommandName * bool * (SnapshotPoint -> int -> MotionResult)

module CommandUtil = 

    let CountOrDefault opt = 
        match opt with 
        | Some(count) -> count
        | None -> 1

    let CreateCommandName name =
        match StringUtil.length name with
        | 0 -> EmptyName
        | 1 -> OneKeyInput (name.Chars(0) |> InputUtil.CharToKeyInput)
        | 2 -> TwoKeyInputs ((name.Chars(0) |> InputUtil.CharToKeyInput), (name.Chars(1) |> InputUtil.CharToKeyInput))
        | _ -> name |> Seq.map InputUtil.CharToKeyInput |> List.ofSeq |> ManyKeyInputs

/// Represents the types of actions which are taken when an ICommandRunner is presented
/// with a KeyInput to run
type RunKeyInputResult = 
    
    /// Ran a command which produced the attached result.  
    | CommandRan of CommandRunData * ModeSwitch

    /// Command was cancelled
    | CommandCancelled 

    /// Command ran but resulted in an error
    | CommandErrored of CommandRunData * string

    /// More input is needed to determine if there is a matching command or not
    | NeedMoreKeyInput 

    /// The ICommandRunner was asked to process a KeyInput when it was already in
    /// the middle of processing one.  This KeyInput was hence ignored
    | NestedRunDetected

    /// No command which matches the given input
    | NoMatchingCommand 

/// Represents the different states of the ICommandRunner with respect to running a Command
type CommandRunnerState =

    /// This is the start state.  No input is on the queue and there is no interesting state
    | NoInput

    /// At least one KeyInput was run but it was not enough to disambiguate which Command to 
    /// run.  
    | NotEnoughInput

    /// Waiting for a Motion or Long Command to complete.  Enough input is present to determine this
    /// is the command to execute but not enough to complete the execution of the command
    | NotFinishWithCommand of Command

/// Responsible for managing a set of Commands and running them
type ICommandRunner =
    
    /// Set of Commands currently supported
    abstract Commands : Command seq

    /// Current state of the ICommandRunner
    abstract State : CommandRunnerState

    /// True if waiting on more input
    abstract IsWaitingForMoreInput : bool

    /// Add a Command.  If there is already a Command with the same name an exception will
    /// be raised
    abstract Add : Command -> unit

    /// Remove a command with the specified name
    abstract Remove : CommandName -> unit

    /// Process the given KeyInput.  If the command completed it will return a result.  A
    /// None value implies more input is needed to finish the operation
    abstract Run : KeyInput -> RunKeyInputResult

    /// If currently waiting for more input on a Command, reset to the 
    /// initial state
    abstract ResetState : unit -> unit

/// Modes for a key remapping
type KeyRemapMode =
    | Normal 
    | Visual 
    | Select 
    | OperatorPending 
    | Insert 
    | Command 
    | Language 

type KeyMappingResult =

    /// No mapping exists 
    | NoMapping 

    | SingleKey of KeyInput 

    | KeySequence of KeyInput seq

    /// The mapping encountered a recursive element that had to be broken 
    | RecursiveMapping of KeyInput seq

    /// More input is needed to resolve this mapping
    | MappingNeedsMoreInput

/// Manages the key map for Vim.  Responsible for handling all key remappings
type IKeyMap =

    /// Get the mapping for the provided KeyInput for the given mode.  If no mapping exists
    /// then a sequence of a single element containing the passed in key will be returned.  
    /// If a recursive mapping is detected it will not be persued and treated instead as 
    /// if the recursion did not exist
    abstract GetKeyMapping : KeyInput -> KeyRemapMode -> KeyInput seq

    /// Get the key mapping for the specified KeyInput
    abstract GetKeyMappingResult : KeyInput -> KeyRemapMode -> KeyMappingResult

    /// Get the key mapping result from the specified set of keys
    abstract GetKeyMappingResultFromMultiple : KeyInput seq -> KeyRemapMode -> KeyMappingResult
    
    /// Map the given key sequence without allowing for remaping
    abstract MapWithNoRemap : lhs:string -> rhs:string -> KeyRemapMode -> bool

    /// Map the given key sequence allowing for a remap 
    abstract MapWithRemap : lhs:string -> rhs:string -> KeyRemapMode -> bool

    /// Unmap the specified key sequence for the specified mode
    abstract Unmap : lhs:string -> KeyRemapMode -> bool

    /// Clear the Key mappings for the specified mode
    abstract Clear : KeyRemapMode -> unit

    /// Clear the Key mappings for all modes
    abstract ClearAll : unit -> unit

type IMarkMap =
    abstract IsLocalMark : char -> bool
    abstract GetLocalMark : ITextBuffer -> char -> VirtualSnapshotPoint option

    /// Setup a local mark for the given SnapshotPoint
    abstract SetLocalMark : SnapshotPoint -> char -> unit
    abstract GetMark : ITextBuffer -> char -> VirtualSnapshotPoint option
    abstract SetMark : SnapshotPoint -> char -> unit

    /// Get the ITextBuffer to which this global mark points to 
    abstract GetGlobalMarkOwner : char -> ITextBuffer option

    /// Get the current value of the specified global mark
    abstract GetGlobalMark : char -> VirtualSnapshotPoint option

    /// Get all of the local marks for the buffer
    abstract GetLocalMarks : ITextBuffer -> (char * VirtualSnapshotPoint) seq

    /// Get all of the available global marks
    abstract GetGlobalMarks : unit -> (char * VirtualSnapshotPoint) seq

    /// Delete the specified local mark on the ITextBuffer
    abstract DeleteLocalMark : ITextBuffer -> char -> bool
    abstract DeleteAllMarks : unit -> unit
    abstract DeleteAllMarksForBuffer : ITextBuffer -> unit


/// Jump list information
type IJumpList = 

    /// Current jump
    abstract Current : SnapshotPoint option

    /// Get all of the jumps in the jump list.  Returns in order of most recent to oldest
    abstract AllJumps : (SnapshotPoint option) seq 

    /// Move to the previous point in the jump list
    abstract MovePrevious: unit -> bool

    /// Move to the next point in the jump list
    abstract MoveNext : unit -> bool

    /// Add a given SnapshotPoint to the jump list
    abstract Add : SnapshotPoint -> unit


/// Map containing the various VIM registers
type IRegisterMap = 
    abstract DefaultRegisterName : char
    abstract DefaultRegister : Register
    abstract RegisterNames : seq<char>
    abstract IsRegisterName : char -> bool
    abstract GetRegister : char -> Register
    
/// Result of an individual search
type SearchResult =
    | SearchFound of SnapshotSpan
    | SearchNotFound 

[<System.Flags>]
type SearchOptions = 
    | None = 0x0

    /// Consider the "ignorecase" option when doing the search
    | AllowIgnoreCase = 0x1

    /// Consider the "smartcase" option when doing the search
    | AllowSmartCase = 0x2

type SearchText =
    | Pattern of string
    | WholeWord of string
    | StraightText of string
    with 
        member x.RawText =
            match x with
            | Pattern(p) -> p
            | WholeWord(p) -> p
            | StraightText(p) -> p

type SearchData = {
    Text : SearchText;
    Kind : SearchKind;
    Options : SearchOptions
}

type SearchProcessResult =
    | SearchComplete 
    | SearchCancelled 
    | SearchNeedMore

/// Global information about searches within Vim
type ISearchService = 

    /// Last search performed
    abstract LastSearch : SearchData with get, set

    /// Raised when the LastSearch value changes
    [<CLIEvent>]
    abstract LastSearchChanged : IEvent<SearchData>

    /// Find the next occurrence of the pattern in the buffer starting at the 
    /// given SnapshotPoint
    abstract FindNext : SearchData -> SnapshotPoint -> ITextStructureNavigator -> SnapshotSpan option

    /// Find the next Nth occurrence of the pattern
    abstract FindNextMultiple : SearchData -> SnapshotPoint -> ITextStructureNavigator -> count:int -> SnapshotSpan option

type IIncrementalSearch = 
    abstract InSearch : bool
    abstract CurrentSearch : SearchData option

    /// ISearchInformation instance this incremental search is associated with
    abstract SearchService : ISearchService

    /// The ITextStructureNavigator used for finding 'word' values in the ITextBuffer
    abstract WordNavigator : ITextStructureNavigator

    /// Processes the next piece of input.  Returns true when the incremental search operation is complete
    abstract Process : KeyInput -> SearchProcessResult

    /// Called when a search is about to begin
    abstract Begin : SearchKind -> unit

    [<CLIEvent>]
    abstract CurrentSearchUpdated : IEvent<SearchData * SearchResult>

    [<CLIEvent>]
    abstract CurrentSearchCompleted : IEvent<SearchData * SearchResult>

    [<CLIEvent>]
    abstract CurrentSearchCancelled : IEvent<SearchData>

type ProcessResult = 
    | Processed
    | ProcessNotHandled
    | SwitchMode of ModeKind
    | SwitchPreviousMode

type SettingKind =
    | NumberKind
    | StringKind    
    | ToggleKind

type SettingValue =
    | NoValue 
    | NumberValue of int
    | StringValue of string
    | ToggleValue of bool
    | CalculatedValue of (unit -> SettingValue)

    /// Get the AggregateValue of the SettingValue.  This will dig through any CalculatedValue
    /// instances and return the actual value
    member x.AggregateValue = 

        let rec digThrough value = 
            match value with 
            | CalculatedValue(func) -> digThrough (func())
            | _ -> value
        digThrough x

[<DebuggerDisplay("{Name}={Value}")>]
type Setting = {
    Name : string
    Abbreviation : string
    Kind : SettingKind
    DefaultValue : SettingValue
    Value : SettingValue
    IsGlobal : bool
} with 

    member x.AggregateValue = x.Value.AggregateValue

    /// Is the setting value currently set to the default value
    member x.IsValueDefault = 
        match x.Value with
        | NoValue -> true
        | _ -> false

module GlobalSettingNames = 

    let IgnoreCaseName = "ignorecase"
    let ShiftWidthName = "shiftwidth"
    let HighlightSearchName = "hlsearch"
    let StartOfLineName = "startofline"
    let TildeOpName = "tildeop"
    let SmartCaseName = "smartcase"
    let VisualBell = "visualbell"
    let DoubleEscapeName = "vsvimdoubleescape"
    let VimRcName = "vimrc"
    let VimRcPathsName = "vimrcpaths"

module LocalSettingNames =
    
    let ScrollName = "scroll"
    let NumberName = "number"

/// Represent the setting supported by the Vim implementation.  This class **IS** mutable
/// and the values will change.  Setting names are case sensitive but the exposed property
/// names tend to have more familiar camel case names
type IVimSettings =

    /// Returns a sequence of all of the settings and values
    abstract AllSettings : Setting seq

    /// Try and set a setting to the passed in value.  This can fail if the value does not 
    /// have the correct type.  The provided name can be the full name or abbreviation
    abstract TrySetValue : settingName:string -> value:SettingValue -> bool

    /// Try and set a setting to the passed in value which originates in string form.  This 
    /// will fail if the setting is not found or the value cannot be converted to the appropriate
    /// value
    abstract TrySetValueFromString : settingName:string -> strValue:string -> bool

    /// Get the value for the named setting.  The name can be the full setting name or an 
    /// abbreviation
    abstract GetSetting : settingName:string -> Setting option

    /// Raised when a Setting changes
    [<CLIEvent>]
    abstract SettingChanged : IEvent<Setting>

and IVimGlobalSettings = 

    abstract IgnoreCase : bool with get, set
    abstract ShiftWidth : int with get, set
    abstract StartOfLine : bool with get, set

    /// Controls the behavior of ~ in normal mode
    abstract TildeOp : bool with get,set

    /// Overrides the IgnoreCase setting in certain cases if the pattern contains
    /// any upper case letters
    abstract SmartCase : bool with get,set

    /// Whether or not to highlight previous search patterns matching cases
    abstract HighlightSearch : bool with get,set

    /// Whether or not to use a visual indicator of errors instead of a beep
    abstract VisualBell : bool with get,set

    /// Affects behavior of <ESC> in Insert Mode.  <ESC> is overloaded some environments to be both 
    /// an exit of Insert mode and a dismisser of intellisense.  The default behavior of insert 
    /// mode is to dismiss intellisense and enter normal mode.  When this option is set it will 
    /// just dismiss intellisense
    abstract DoubleEscape:bool with get,set

    /// Retrieves the location of the loaded VimRC file.  Will be the empty string if the load 
    /// did not succeed or has not been tried
    abstract VimRc : string with get, set

    /// Set of paths considered when looking for a .vimrc file.  Will be the empty string if the 
    /// load has not been attempted yet
    abstract VimRcPaths : string with get, set

    abstract DisableCommand: KeyInput;

    inherit IVimSettings

/// Settings class which is local to a given IVimBuffer.  This will hide the work of merging
/// global settings with non-global ones
and IVimLocalSettings =

    /// Return the handle to the global IVimSettings instance
    abstract GlobalSettings : IVimGlobalSettings

    abstract Scroll : int with get,set

    inherit IVimSettings

/// Vim instance.  Global for a group of buffers
and IVim =
    abstract VimHost : IVimHost
    abstract MarkMap : IMarkMap
    abstract RegisterMap : IRegisterMap
    abstract Settings : IVimGlobalSettings

    /// IKeyMap for this IVim instance
    abstract KeyMap : IKeyMap

    /// IChangeTracker for this IVim instance
    abstract ChangeTracker : IChangeTracker

    /// ISearchService for this IVim instance
    abstract SearchService : ISearchService

    /// Is the VimRc loaded
    abstract IsVimRcLoaded : bool

    /// Create an IVimBuffer for the given IWpfTextView
    abstract CreateBuffer : ITextView -> IVimBuffer

    /// Get the IVimBuffer associated with the given view
    abstract GetBuffer : ITextView -> IVimBuffer option

    /// Get or create an IVimBuffer for the given IWpfTextView
    abstract GetOrCreateBuffer : ITextView -> IVimBuffer

    /// Get the IVimBuffer associated with the given view
    abstract GetBufferForBuffer : ITextBuffer -> IVimBuffer option

    /// Remove the IVimBuffer associated with the given view.  This will not actually close
    /// the IVimBuffer but instead just removes it's association with the given view
    abstract RemoveBuffer : ITextView -> bool

    /// Load the VimRc file.  If the file was previously, a new load will be attempted
    abstract LoadVimRc : IFileSystem -> createViewFunc:(unit -> ITextView) -> bool

    
/// Main interface for the Vim editor engine so to speak. 
and IVimBuffer =

    /// Name of the buffer.  Used for items like Marks
    abstract Name : string

    /// View of the file
    abstract TextView : ITextView

    /// Underyling ITextBuffer Vim is operating under
    abstract TextBuffer : ITextBuffer

    /// Current ITextSnapshot of the ITextBuffer
    abstract TextSnapshot : ITextSnapshot

    /// Buffered KeyInput list.  When a key remapping has multiple source elements the input 
    /// is buffered until it is completed or the ambiguity is removed.  
    abstract BufferedRemapKeyInputs : KeyInput list

    /// Owning IVim instance
    abstract Vim : IVim
    abstract MarkMap : IMarkMap

    /// Jump list
    abstract JumpList : IJumpList

    /// ModeKind of the current IMode in the buffer
    abstract ModeKind : ModeKind

    /// Current mode of the buffer
    abstract Mode : IMode

    /// Whether or not the IVimBuffer is currently processing input
    abstract IsProcessingInput : bool

    abstract NormalMode : INormalMode 
    abstract CommandMode : ICommandMode 
    abstract DisabledMode : IDisabledMode

    /// Sequence of available Modes
    abstract AllModes : seq<IMode>

    abstract Settings : IVimLocalSettings
    abstract RegisterMap : IRegisterMap

    abstract GetRegister : char -> Register

    /// Get the specified Mode
    abstract GetMode : ModeKind -> IMode
    
    /// Process the char in question and return whether or not it was handled
    abstract ProcessChar : char -> bool
    
    /// Process the KeyInput and return whether or not the input was completely handled
    abstract ProcessInput : KeyInput -> bool
    abstract CanProcessInput : KeyInput -> bool
    abstract SwitchMode : ModeKind -> IMode

    /// Switch the buffer back to the previous mode which is returned
    abstract SwitchPreviousMode : unit -> IMode

    /// Called when the view is closed and the IVimBuffer should uninstall itself
    /// and it's modes
    abstract Close : unit -> unit
    
    /// Raised when the mode is switched
    [<CLIEvent>]
    abstract SwitchedMode : IEvent<IMode>

    /// Raised when a key is processed.  This is raised when the KeyInput is actually
    /// processed by Vim not when it is received.  
    ///
    /// Typically these occur back to back.  One example of where it does not though is 
    /// the case of a key remapping where the source mapping contains more than one key.  
    /// In this case the input is buffered until the second key is read and then the 
    /// inputs are processed
    [<CLIEvent>]
    abstract KeyInputProcessed : IEvent<KeyInput * ProcessResult>

    /// Raised when a KeyInput is received by the buffer
    [<CLIEvent>]
    abstract KeyInputReceived : IEvent<KeyInput>

    /// Raised when a key is received but not immediately processed.  Occurs when a
    /// key remapping has more than one source key strokes
    [<CLIEvent>]
    abstract KeyInputBuffered : IEvent<KeyInput>

    /// Raised when an error is encountered
    [<CLIEvent>]
    abstract ErrorMessage : IEvent<string>

    /// Raised when a status message is encountered
    [<CLIEvent>]
    abstract StatusMessage : IEvent<string>

    /// Raised when a long status message is encountered
    [<CLIEvent>]
    abstract StatusMessageLong : IEvent<string seq>

and IMode =

    /// Owning IVimBuffer
    abstract VimBuffer : IVimBuffer 

    /// What type of Mode is this
    abstract ModeKind : ModeKind

    /// Sequence of commands handled by the Mode.  
    abstract Commands : seq<KeyInput>

    /// Can the mode process this particular KeyIput at the current time
    abstract CanProcess : KeyInput -> bool

    /// Process the given KeyInput
    abstract Process : KeyInput -> ProcessResult

    /// Called when the mode is entered
    abstract OnEnter : unit -> unit

    /// Called when the mode is left
    abstract OnLeave : unit -> unit


and INormalMode =

    /// Buffered input for the current command
    abstract Command : string 

    /// Is in the middle of an operator pending 
    abstract IsOperatorPending : bool

    /// Is normal mode waiting for additional input on a command
    abstract IsWaitingForInput : bool

    /// Is normal mode in the middle of a character replace operation
    abstract IsInReplace : bool

    /// The IIncrementalSearch instance for normal mode
    abstract IncrementalSearch : IIncrementalSearch

    /// Raised when a command is executed
    [<CLIEvent>]
    abstract CommandExecuted : IEvent<NormalModeCommand>

    inherit IMode

and ICommandMode = 

    /// buffered input for the current command
    abstract Command : string

    /// Run the specified command
    abstract RunCommand : string -> unit

    inherit IMode
    
and IDisabledMode =
    
    /// Help message to display 
    abstract HelpMessage : string 

    inherit IMode

/// Command executed in normal mode
and NormalModeCommand =
    | NonRepeatableCommand
    | RepeatableCommand of KeyInput list * int * Register 

and IChangeTracker =
    
    abstract LastChange : RepeatableChange option

and RepeatableChange =
    | NormalModeChange of KeyInput list * int * Register
    | TextChange of string

