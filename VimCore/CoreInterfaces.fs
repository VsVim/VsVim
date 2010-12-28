#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Utilities
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

/// Wraps an ITextUndoTransaction so we can avoid all of the null checks
type IUndoTransaction =

    /// Call when it completes
    abstract Complete : unit -> unit

    /// Cancels the transaction
    abstract Cancel : unit -> unit

    inherit System.IDisposable

/// Wraps all of the undo and redo operations
type IUndoRedoOperations = 

    /// StatusUtil instance that is used to report errors
    abstract StatusUtil : IStatusUtil

    /// Undo the last "count" operations
    abstract Undo : count:int -> unit

    /// Redo the last "count" operations
    abstract Redo : count:int -> unit

    /// Creates an Undo Transaction
    abstract CreateUndoTransaction : name:string -> IUndoTransaction

/// Context on how the motion is being used.  Several motions (]] for example)
/// change behavior based on how they are being used
[<RequireQualifiedAccess>]
type MotionContext =
    | Movement
    | AfterOperator

/// Arguments necessary to buliding a Motion
type MotionArgument = {

    /// Context of the Motion
    MotionContext : MotionContext

    /// Count passed to the operator
    OperatorCount : int option

    /// Count passed to the motion 
    MotionCount : int option 

} with

    /// Provides the raw count which is a combination of the OperatorCount
    /// and MotionCount values.  
    member x.RawCount = 
        match x.MotionCount,x.OperatorCount with
        | None,None -> None
        | Some(c),None -> Some c
        | None,Some(c) -> Some c
        | Some(l),Some(r) -> Some (l*r)

    /// Resolves the count to a value.  It will use the default 1 for 
    /// any count which is not provided 
    member x.Count = 
        let operatorCount = x.OperatorCount |> OptionUtil.getOrDefault 1
        let motionCount = x.MotionCount |> OptionUtil.getOrDefault 1
        operatorCount * motionCount

/// Responsible for implementing all of the Motion information
type ITextViewMotionUtil = 

    /// ITextView associated with the ITextViewMotionUtil
    abstract TextView : ITextView 

    /// Left "count" characters
    abstract CharLeft: int -> MotionData option

    /// Right "count" characters
    abstract CharRight: int -> MotionData option
    
    /// Forward to the next occurance of the specified char on the line
    abstract ForwardChar : char -> int -> MotionData option

    /// Handle the 't' motion.  Forward till the next occurrence of the specified character on
    /// this line
    abstract ForwardTillChar : char -> int -> MotionData option

    /// Handle the 'F' motion.  Backward to the previous occurrence of the specified character
    /// on this line
    abstract BackwardChar : char -> int -> MotionData option

    /// Handle the 'T' motion.  Backward till to the previous occurrence of the specified character
    abstract BackwardTillChar : char -> int -> MotionData option
        
    /// Implement the w/W motion
    abstract WordForward : WordKind -> int -> MotionData

    /// Implement the b/B motion
    abstract WordBackward : WordKind -> int -> MotionData 
        
    /// Implement the aw motion.  This is called once the a key is seen.
    abstract AllWord : WordKind -> int -> MotionData

    /// Implement the 'e' motion.  This goes to the end of the current word.  If we're
    /// not currently on a word it will find the next word and then go to the end of that
    abstract EndOfWord : WordKind -> int -> MotionData 
    
    /// Implement an end of line motion.  Typically in response to the $ key.  Even though
    /// this motion deals with lines, it's still a character wise motion motion. 
    abstract EndOfLine : int -> MotionData

    /// Find the first non-whitespace character as the start of the span.  This is an exclusive
    /// motion so be careful we don't go to far forward
    abstract FirstNonWhitespaceOnLine : unit -> MotionData 

    /// Find the last non-whitespace character on the line.  Count causes it to go "count" lines
    /// down and perform the search
    abstract LastNonWhitespaceOnLine : int -> MotionData

    /// Move to the begining of the line.  Interestingly since this command is bound to the '0' it 
    /// can't be associated with a count.  Doing a command like 30 binds as count 30 vs. count 3 
    /// for command '0'
    abstract BeginingOfLine : unit -> MotionData

    /// Handle the lines down to first non-whitespace motion.  This is one of the motions which 
    /// can accept a count of 0.
    abstract LineDownToFirstNonWhitespace : int -> MotionData 

    /// Handle the - motion
    abstract LineUpToFirstNonWhitespace : int -> MotionData

    /// Get the span of "count" lines upward careful not to run off the beginning of the
    /// buffer.  Implementation of the "k" motion
    abstract LineUp : int -> MotionData

    /// Get the span of "count" lines downward careful not to run off the end of the
    /// buffer.  Implementation of the "j" motion
    abstract LineDown : int -> MotionData

    /// Go to the specified line number or the first line if no line number is provided 
    abstract LineOrFirstToFirstNonWhitespace : int option -> MotionData 

    /// Go to the specified line number or the last line of no line number is provided
    abstract LineOrLastToFirstNonWhitespace : int option -> MotionData 

    /// Go to the "count - 1" line from the top of the visible window.  If the count exceeds
    /// the number of visible lines it will end on the last visible line
    abstract LineFromTopOfVisibleWindow : int option -> MotionData

    /// Go to the "count -1" line from the bottom of the visible window.  If the count 
    /// exceeds the number of visible lines it will end on the first visible line
    abstract LineFromBottomOfVisibleWindow : int option -> MotionData

    /// Go to the middle line in the visible window.  
    abstract LineInMiddleOfVisibleWindow : unit -> MotionData

    /// Count sentences forward
    abstract SentenceForward : count:int -> MotionData

    /// Count sentences backward 
    abstract SentenceBackward : count:int -> MotionData

    /// Gets count full sentences from the cursor.  If used on a blank line this will
    /// not return a value
    abstract SentenceFullForward : count:int -> MotionData

    /// Count paragraphs forward
    abstract ParagraphForward : count:int -> MotionData

    /// Count pargraphs backwards
    abstract ParagraphBackward : count:int -> MotionData

    /// Get count full sentences from the cursor.  
    abstract ParagraphFullForward : count:int -> MotionData

    /// Forward a section in the editor
    abstract SectionForward : MotionContext -> count:int-> MotionData

    /// Backward a section in the editor or to an open brace
    abstract SectionBackwardOrOpenBrace : count:int -> MotionData
    
    /// Backward a section in the editor or to a close brace
    abstract SectionBackwardOrCloseBrace : count:int -> MotionData

    /// The quoted string including the quotes
    abstract QuotedString : unit -> MotionData option

    /// The quoted string excluding the quotes
    abstract QuotedStringContents : unit -> MotionData option

type ModeKind = 
    | Normal = 1
    | Insert = 2
    | Command = 3
    | VisualCharacter = 4
    | VisualLine = 5
    | VisualBlock = 6 
    | Replace = 7
    | SubstituteConfirm = 8
    | ExternalEdit = 9

    // Mode when Vim is disabled via the user
    | Disabled = 42

[<RequireQualifiedAccess>]
type VisualKind =
    | Character
    | Line
    | Block
    with 
    static member ofModeKind kind = 
        match kind with 
        | ModeKind.VisualBlock -> VisualKind.Block |> Some
        | ModeKind.VisualLine -> VisualKind.Line |> Some
        | ModeKind.VisualCharacter -> VisualKind.Character |> Some
        | _ -> None

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
[<DebuggerDisplay("{ToString(),nq}")>]
type KeyInputSet =
    | Empty
    | OneKeyInput of KeyInput
    | TwoKeyInputs of KeyInput * KeyInput
    | ManyKeyInputs of KeyInput list
    with 

    /// Returns the first KeyInput if preseent
    member x.FirstKeyInput = 
        match x with 
        | Empty -> None
        | OneKeyInput(ki) -> Some ki
        | TwoKeyInputs(ki,_) -> Some ki
        | ManyKeyInputs(list) -> ListUtil.tryHeadOnly list

    /// Get the list of KeyInput which represent this KeyInputSet
    member x.KeyInputs =
        match x with 
        | Empty -> List.empty
        | OneKeyInput(ki) -> [ki]
        | TwoKeyInputs(k1,k2) -> [k1;k2]
        | ManyKeyInputs(list) -> list

    /// A string representation of the name.  It is unreliable to use this for anything
    /// other than display as two distinct KeyInput values can map to a single char
    member x.Name = x.KeyInputs |> Seq.map (fun ki -> ki.Char) |> StringUtil.ofCharSeq

    /// Length of the contained KeyInput's
    member x.Length =
        match x with
        | Empty -> 0
        | OneKeyInput(_) -> 1
        | TwoKeyInputs(_) -> 2
        | ManyKeyInputs(list) -> list.Length

    /// Add a KeyInput to the end of this KeyInputSet and return the 
    /// resulting value
    member x.Add (ki) =
        match x with 
        | Empty -> OneKeyInput ki
        | OneKeyInput(previous) -> TwoKeyInputs(previous,ki)
        | TwoKeyInputs(p1,p2) -> ManyKeyInputs [p1;p2;ki]
        | ManyKeyInputs(list) -> ManyKeyInputs (list @ [ki])

    /// Does the name start with the given KeyInputSet
    member x.StartsWith (targetName:KeyInputSet) = 
        match targetName,x with
        | Empty, _ -> true
        | OneKeyInput(leftKi), OneKeyInput(rightKi) ->  leftKi = rightKi
        | OneKeyInput(leftKi), TwoKeyInputs(rightKi,_) -> leftKi = rightKi
        | _ -> 
            let left = targetName.KeyInputs 
            let right = x.KeyInputs
            if left.Length < right.Length then
                SeqUtil.contentsEqual (left |> Seq.ofList) (right |> Seq.ofList |> Seq.take left.Length)
            else false

    override x.GetHashCode() = 
        match x with
        | Empty -> 1
        | OneKeyInput(ki) -> ki.GetHashCode()
        | TwoKeyInputs(k1,k2) -> k1.GetHashCode() ^^^ k2.GetHashCode()
        | ManyKeyInputs(list) -> 
            list 
            |> Seq.ofList
            |> Seq.map (fun ki -> ki.GetHashCode())
            |> Seq.sum

    override x.Equals(yobj) =
        match yobj with
        | :? KeyInputSet as y -> 
            match x,y with
            | OneKeyInput(left),OneKeyInput(right) -> left = right
            | TwoKeyInputs(l1,l2),TwoKeyInputs(r1,r2) -> l1 = r1 && l2 = r2
            | _ -> ListUtil.contentsEqual x.KeyInputs y.KeyInputs
        | _ -> false

    static member op_Equality(this,other) = System.Collections.Generic.EqualityComparer<KeyInputSet>.Default.Equals(this,other)
    static member op_Inequality(this,other) = not (System.Collections.Generic.EqualityComparer<KeyInputSet>.Default.Equals(this,other))

    override x.ToString() =
        x.KeyInputs
        |> Seq.map (fun ki ->
            if ki.Key = VimKey.NotWellKnown then ki.Char.ToString()
            else System.String.Format("<{0}>", ki.Key)  )
        |> StringUtil.ofStringSeq

    interface System.IComparable with
        member x.CompareTo yobj = 
            match yobj with
            | :? KeyInputSet as y -> 
                let rec inner (left:KeyInput list) (right:KeyInput list) =
                    if left.IsEmpty && right.IsEmpty then 0
                    elif left.IsEmpty then -1
                    elif right.IsEmpty then 1
                    elif left.Head < right.Head then -1
                    elif left.Head > right.Head then 1
                    else inner (List.tail left) (List.tail right)
                inner x.KeyInputs y.KeyInputs
            | _ -> failwith "Cannot compare values of different types"

module KeyInputSetUtil =

    let OfSeq sequence = 
        match Seq.length sequence with
        | 0 -> KeyInputSet.Empty
        | 1 -> KeyInputSet.OneKeyInput (Seq.nth 0 sequence)
        | 2 -> KeyInputSet.TwoKeyInputs ((Seq.nth 0 sequence),(Seq.nth 1 sequence))
        | _ -> sequence |> List.ofSeq |> KeyInputSet.ManyKeyInputs 

    let OfList list = 
        match list with
        | [] -> KeyInputSet.Empty
        | [ki] -> KeyInputSet.OneKeyInput ki
        | _ -> 
            match list.Length with
            | 2 -> KeyInputSet.TwoKeyInputs ((List.nth list 0),(List.nth list 1))
            | _ -> KeyInputSet.ManyKeyInputs list

    let OfChar c = c |> KeyInputUtil.CharToKeyInput |> OneKeyInput

    let OfString (str:string) = str |> Seq.map KeyInputUtil.CharToKeyInput |> OfSeq

/// Modes for a key remapping
[<RequireQualifiedAccess>]
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

    /// Mapped to the specified KeyInputSet
    | Mapped of KeyInputSet 

    /// The mapping encountered a recursive element that had to be broken 
    | RecursiveMapping of KeyInputSet

    /// More input is needed to resolve this mapping
    | MappingNeedsMoreInput


/// Flags for the substitute command
[<System.Flags>]
type SubstituteFlags = 
    | None = 0
    /// Replace all occurances on the line
    | ReplaceAll = 0x1
    /// Ignore case for the search pattern
    | IgnoreCase = 0x2
    /// Report only 
    | ReportOnly = 0x4
    | Confirm = 0x8
    | UsePreviousFlags = 0x10
    | UsePreviousSearchPattern = 0x20
    | SuppressError = 0x40
    | OrdinalCase = 0x80
    | Magic = 0x100
    | Nomagic = 0x200

    /// The p option.  Print the last replaced line
    | PrintLast = 0x400

    /// The # option.  Print the last replaced line with the line number prepended
    | PrintLastWithNumber = 0x800

    /// Print the last line as if :list was used
    | PrintLastWithList = 0x1000

type SubstituteData = {
    SearchPattern : string
    Substitute : string
    Flags : SubstituteFlags
}

[<RequireQualifiedAccess>]
type ModeArgument =
    | None

    /// Used for transitions from Visual Mode directly to Command mode
    | FromVisual 

    /// When the given mode is to execute a single command then return to 
    /// the previous mode.  The provided mode kind is the value which needs
    /// to be switched to upon completion of the command
    | OneTimeCommand of ModeKind

    /// Passeing the substitute to confirm to Confirm mode.  The SnapshotSpan is the first
    /// match to process and the range is the full range to consider for a replace
    | Subsitute of SnapshotSpan * SnapshotLineRange * SubstituteData

type ModeSwitch =
    | NoSwitch
    | SwitchMode of ModeKind
    | SwitchModeWithArgument of ModeKind * ModeArgument
    | SwitchPreviousMode 

[<RequireQualifiedAccess>]
type CommandResult =   
    | Completed  of ModeSwitch
    | Error of string

[<RequireQualifiedAccess>]
type LongCommandResult =
    | Finished of CommandResult
    | Cancelled
    | NeedMoreInput of KeyRemapMode option * (KeyInput -> LongCommandResult)

[<RequireQualifiedAccess>]
type RunResult = 
    | Completed
    | SubstituteConfirm of SnapshotSpan * SnapshotLineRange * SubstituteData

[<RequireQualifiedAccess>]
type VisualSpan =
    | Single of VisualKind * SnapshotSpan
    | Multiple of VisualKind * NormalizedSnapshotSpanCollection
    with
    member x.VisualKind = 
        match x with
        | Single(kind,_) -> kind
        | Multiple(kind,_) -> kind

/// Information about the attributes of Command
[<System.Flags>]
type CommandFlags =
    | None = 0x0
    /// Relates to the movement of the cursor.  A movement command does not alter the 
    /// last command
    | Movement = 0x1
    /// A Command which can be repeated
    | Repeatable = 0x2
    /// A Command which should not be considered when looking at last changes
    | Special = 0x4
    /// Can handle the escape key if provided as part of a Motion or Long command extra
    /// input
    | HandlesEscape = 0x8
    /// For the purposes of change repeating the command is linked with the following
    /// text change
    | LinkedWithNextTextChange = 0x10
    /// For Visual Mode commands which should reset the cursor to the original point
    /// after completing
    | ResetCaret = 0x20

/// Representation of commands within Vim.  
[<DebuggerDisplay("{ToString(),nq}")>]
type Command = 
    
    /// Represents a Command which has no motion modifiers.  The  delegate takes 
    /// an optional count and a Register.  If unspecified the default register
    /// will be used
    | SimpleCommand of KeyInputSet * CommandFlags * (int option -> Register -> CommandResult)

    /// Represents a Command prefix which has an associated motion.  The delegate takes
    /// an optional count, a Register and a MotionData value.  If unspecified the default
    /// register will be used
    | MotionCommand of KeyInputSet * CommandFlags * (int option -> Register -> MotionData -> CommandResult)

    /// Represents a command which has a Name but then has additional unspecified input
    /// which needs to be dealt with specially by the command.  These commands are not
    /// repeatable.  
    | LongCommand of KeyInputSet * CommandFlags * (int option -> Register -> LongCommandResult) 

    /// Represents a command which has a name and relies on the Visual Mode Span to 
    /// execute the command
    | VisualCommand of KeyInputSet * CommandFlags * VisualKind * (int option -> Register -> VisualSpan -> CommandResult) 

    with 

    /// The raw command inputs
    member x.KeyInputSet = 
        match x with
        | SimpleCommand(value,_,_ ) -> value
        | MotionCommand(value,_,_) -> value
        | LongCommand(value,_,_) -> value
        | VisualCommand(value,_,_,_) -> value

    /// The kind of the Command
    member x.CommandFlags =
        match x with
        | SimpleCommand(_,value,_ ) -> value
        | MotionCommand(_,value,_) -> value
        | LongCommand(_,value,_) -> value
        | VisualCommand(_,value,_,_) -> value

    /// Is the Repeatable flag set
    member x.IsRepeatable = Util.IsFlagSet x.CommandFlags CommandFlags.Repeatable

    /// Is the HandlesEscape flag set
    member x.HandlesEscape = Util.IsFlagSet x.CommandFlags CommandFlags.HandlesEscape

    /// Is the Movement flag set
    member x.IsMovement = Util.IsFlagSet x.CommandFlags CommandFlags.Movement

    /// Is the Special flag set
    member x.IsSpecial = Util.IsFlagSet x.CommandFlags CommandFlags.Special

    override x.ToString() = System.String.Format("{0} -> {1}", x.KeyInputSet, x.CommandFlags)


/// Flags about specific motions
[<RequireQualifiedAccess>]
[<System.Flags>]
type MotionFlags =

    /// This type of motion can be used to move the cursor
    | CursorMovement = 0x1 

    /// Text object selection motions.  These can be used for cursor movement inside of 
    /// Visual Mode but otherwise need to be used only after operators.  
    /// :help text-objects
    | TextObjectSelection = 0x2

    /// The motion function wants to specially handle the esape function.  This is used 
    /// on Complex motions such as / and ? 
    | HandlesEscape = 0x4

type MotionFunction = MotionArgument -> MotionData option

type ComplexMotionResult =
    /// The complex motion was completed and produced potentially a function that given
    /// a MotionArgument will attempt to calculate a MotionData.  It can also optionally
    /// return a MotionFunction used to calculate the very first instance of the 
    /// MotionData function (useful for caching).  
    | Finished of MotionFunction * MotionFunction option
    | Cancelled
    | Error of string
    | NeedMoreInput of KeyRemapMode option * (KeyInput -> ComplexMotionResult)

type ComplexMotionFunction = unit -> ComplexMotionResult

/// Represents the types of MotionCommands which exist
type MotionCommand = 

    /// Simple motion which comprises of a single KeyInput and a function which given 
    /// a start point and count will produce the motion.  None is returned in the 
    /// case the motion is not valid
    | SimpleMotionCommand of KeyInputSet * MotionFlags * MotionFunction

    /// Complex motion commands take more than one KeyInput to complete.  For example 
    /// the f,t,F and T commands all require at least one additional input.  The bool
    /// in the middle of the tuple indicates whether or not the motion can be 
    /// used as a cursor movement operation  
    | ComplexMotionCommand of KeyInputSet * MotionFlags * ComplexMotionFunction

    with

    member x.KeyInputSet = 
        match x with
        | SimpleMotionCommand(name,_,_) -> name
        | ComplexMotionCommand(name,_,_) -> name

    member x.MotionFlags =
        match x with 
        | SimpleMotionCommand(_,flags,_) -> flags
        | ComplexMotionCommand(_,flags,_) -> flags

/// Data about the run of a given MotionData
type MotionRunData = {
    MotionCommand : MotionCommand
    MotionArgument : MotionArgument
    MotionFunction : MotionFunction
}

/// The information about the particular run of a Command
type CommandRunData = {
    Command : Command;
    Register : Register;
    Count : int option;

    /// For commands which took a motion this will hold the relevant information
    /// on how the motion was ran
    MotionRunData : MotionRunData option

    /// For visual commands this holds the relevant span information
    VisualRunData : VisualSpan option
}

[<RequireQualifiedAccess>]
type MotionResult = 
    /// Motion is complete.  Returns both the information about the result of the motion and the 
    /// data necessary to repeat the motion later
    | Complete of MotionData * MotionRunData

    /// Motion needs more input.  
    | NeedMoreInput of KeyRemapMode option * (KeyInput -> MotionResult)

    | Error of string
    | Cancelled

/// Holds the data which is global to all IMotionCapture instances
type IMotionCaptureGlobalData =

    /// Motion function used with the last f, F, t or T motion.  The 
    // first item in the tuple is the forward version and the second item
    // is the backwards version
    abstract LastCharSearch : (MotionFunction * MotionFunction) option with get,set

/// Responsible for capturing motions on a given ITextView
type IMotionCapture =

    /// Associated ITextView
    abstract TextView : ITextView
    
    /// Set of supported MotionCommand
    abstract MotionCommands : seq<MotionCommand>

    /// Get the motion starting with the given KeyInput
    abstract GetOperatorMotion : KeyInput -> int option -> MotionResult

module CommandUtil = 

    let CountOrDefault opt = 
        match opt with 
        | Some(count) -> count
        | None -> 1

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

    /// There exist many pairs of commands where one is a Motion and another is a Simple command
    /// where the name of the Motion is a prefix of the Simple command.  The MotionCommand is 
    /// captured in the first item of the tuple and all other commands with a matching prefix are
    /// captured in the list
    | NotEnoughMatchingPrefix of Command * Command list * KeyRemapMode option

    /// Waiting for a Motion or Long Command to complete.  Enough input is present to determine this
    /// is the command to execute but not enough to complete the execution of the command.  The
    /// bool in the tuple represents whether or not the next input should be processed as language 
    /// input (:help language-mapping)
    | NotFinishWithCommand of Command * KeyRemapMode option

/// Responsible for managing a set of Commands and running them
type ICommandRunner =
    
    /// Set of Commands currently supported
    abstract Commands : Command seq

    /// Current state of the ICommandRunner
    abstract State : CommandRunnerState

    /// In certain circumstances a specific type of key remapping needs to occur for input.  This 
    /// option will have the appropriate value in those circumstances.  For example while processing
    /// the {char} argument to f,F,t or T the Language mapping will be used
    abstract KeyRemapMode : KeyRemapMode option

    /// True if waiting on more input
    abstract IsWaitingForMoreInput : bool

    /// Add a Command.  If there is already a Command with the same name an exception will
    /// be raised
    abstract Add : Command -> unit

    /// Remove a command with the specified name
    abstract Remove : KeyInputSet -> unit

    /// Process the given KeyInput.  If the command completed it will return a result.  A
    /// None value implies more input is needed to finish the operation
    abstract Run : KeyInput -> RunKeyInputResult

    /// If currently waiting for more input on a Command, reset to the 
    /// initial state
    abstract ResetState : unit -> unit

    /// Raised when a command is successfully run
    [<CLIEvent>]
    abstract CommandRan : IEvent<CommandRunData * CommandResult>

/// Manages the key map for Vim.  Responsible for handling all key remappings
type IKeyMap =

    /// Get all mappings for the specified mode
    abstract GetKeyMappingsForMode : KeyRemapMode -> (KeyInputSet * KeyInputSet) seq 

    /// Get the mapping for the provided KeyInput for the given mode.  If no mapping exists
    /// then a sequence of a single element containing the passed in key will be returned.  
    /// If a recursive mapping is detected it will not be persued and treated instead as 
    /// if the recursion did not exist
    abstract GetKeyMapping : KeyInputSet -> KeyRemapMode -> KeyMappingResult

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

    /// Gets all of the available register name values
    abstract RegisterNames : seq<RegisterName>

    /// Get the register with the specified name
    abstract GetRegister : RegisterName -> Register

/// Result of an individual search
type SearchResult =
    | SearchFound of SnapshotSpan
    | SearchNotFound 

[<System.Flags>]
type SearchOptions = 
    | None = 0x0

    /// Consider the "ignorecase" option when doing the search
    | ConsiderIgnoreCase = 0x1

    /// Consider the "smartcase" option when doing the search
    | ConsiderSmartCase = 0x2

[<RequireQualifiedAccess>]
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
    
    /// Is this a pattern
    member x.IsPatternText = 
        match x with
        | Pattern(_) -> true
        | WholeWord(_) -> false
        | StraightText(_) -> false

type SearchData = {
    Text : SearchText;
    Kind : SearchKind;
    Options : SearchOptions
}

type SearchProcessResult =
    | SearchNotStarted 
    | SearchComplete of SearchData * SearchResult
    | SearchCancelled 
    | SearchNeedMore

/// Global information about searches within Vim
type ISearchService = 

    /// Find the next occurrence of the pattern in the buffer starting at the 
    /// given SnapshotPoint
    abstract FindNext : SearchData -> SnapshotPoint -> ITextStructureNavigator -> SnapshotSpan option

    /// Find the next Nth occurrence of the pattern
    abstract FindNextMultiple : SearchData -> SnapshotPoint -> ITextStructureNavigator -> count:int -> SnapshotSpan option

type IIncrementalSearch = 

    /// True when a search is occuring
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
    | SwitchModeWithArgument of ModeKind * ModeArgument
    | SwitchPreviousMode
    with
    static member OfModeSwitch mode =
        match mode with
        | ModeSwitch.NoSwitch -> ProcessResult.Processed
        | ModeSwitch.SwitchMode(kind) -> ProcessResult.SwitchMode kind
        | ModeSwitch.SwitchModeWithArgument(kind,arg) -> ProcessResult.SwitchModeWithArgument (kind,arg)
        | ModeSwitch.SwitchPreviousMode -> ProcessResult.SwitchPreviousMode

    // Is this any type of mode switch
    member x.IsAnySwitch =
        match x with
        | Processed -> false
        | ProcessNotHandled -> false
        | SwitchMode(_) -> true
        | SwitchModeWithArgument(_,_) -> true
        | SwitchPreviousMode -> true

[<RequireQualifiedAccess>]
type TextChange = 
    | Insert of string
    | Delete of int

type SettingKind =
    | NumberKind
    | StringKind    
    | ToggleKind

type SettingValue =
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
        match x.Value, x.DefaultValue with
        | CalculatedValue(_), CalculatedValue(_) -> true
        | NumberValue(left), NumberValue(right) -> left = right
        | StringValue(left), StringValue(right) -> left = right
        | ToggleValue(left), ToggleValue(right) -> left = right
        | _ -> false

module GlobalSettingNames = 

    let CaretOpacityName = "vsvimcaret"
    let HighlightSearchName = "hlsearch"
    let IgnoreCaseName = "ignorecase"
    let MagicName = "magic"
    let ScrollOffsetName = "scrolloff"
    let SelectionName = "selection"
    let ShiftWidthName = "shiftwidth"
    let SmartCaseName = "smartcase"
    let StartOfLineName = "startofline"
    let TabStopName = "tabstop"
    let TildeOpName = "tildeop"
    let VisualBellName = "visualbell"
    let VirtualEditName = "virtualedit"
    let VimRcName = "vimrc"
    let VimRcPathsName = "vimrcpaths"

module LocalSettingNames =

    let AutoIndentName = "autoindent"
    let CursorLineName = "cursorline"
    let NumberName = "number"
    let ScrollName = "scroll"
    let QuoteEscapeName = "quoteescape"

/// Holds mutable data available to all buffers
type IVimData = 

    /// Data for the last substitute command performed
    abstract LastSubstituteData : SubstituteData option with get,set

    /// Last pattern searched for in any buffer
    abstract LastSearchData : SearchData with get,set

    /// Raised when the LastSearch value changes
    [<CLIEvent>]
    abstract LastSearchDataChanged : IEvent<SearchData>

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

    /// Opacity of the caret.  This must be an integer between values 0 and 100 which
    /// will be converted into a double for the opacity of the caret
    abstract CaretOpacity : int with get, set

    /// Whether or not to highlight previous search patterns matching cases
    abstract HighlightSearch : bool with get,set

    /// Whether or not the magic option is set
    abstract Magic : bool with get,set

    /// Is the onemore option inside of VirtualEdit set
    abstract IsVirtualEditOneMore : bool with get

    /// Controls how many spaces a tab counts for.  
    abstract TabStop : int with get,set

    /// Controls the behavior of ~ in normal mode
    abstract TildeOp : bool with get,set

    /// Holds the scroll offset value which is the number of lines to keep visible
    /// above the cursor after a move operation
    abstract ScrollOffset : int with get,set

    /// Holds the Selection option
    abstract Selection : string with get,set

    /// Overrides the IgnoreCase setting in certain cases if the pattern contains
    /// any upper case letters
    abstract SmartCase : bool with get,set

    /// Retrieves the location of the loaded VimRC file.  Will be the empty string if the load 
    /// did not succeed or has not been tried
    abstract VimRc : string with get, set

    /// Set of paths considered when looking for a .vimrc file.  Will be the empty string if the 
    /// load has not been attempted yet
    abstract VimRcPaths : string with get, set

    /// Holds the VirtualEdit string.  
    abstract VirtualEdit : string with get,set

    /// Whether or not to use a visual indicator of errors instead of a beep
    abstract VisualBell : bool with get,set

    abstract DisableCommand: KeyInput;

    inherit IVimSettings

/// Settings class which is local to a given IVimBuffer.  This will hide the work of merging
/// global settings with non-global ones
and IVimLocalSettings =

    abstract AutoIndent : bool with get, set

    /// Whether or not to highlight the line the cursor is on
    abstract CursorLine : bool with get,set

    /// Return the handle to the global IVimSettings instance
    abstract GlobalSettings : IVimGlobalSettings

    abstract Scroll : int with get,set

    /// Which characters escape quotes for certain motion types
    abstract QuoteEscape : string with get,set

    inherit IVimSettings

/// Vim instance.  Global for a group of buffers
and IVim =

    /// Buffer actively processing input.  This has no relation to the IVimBuffer
    /// which has focus 
    abstract ActiveBuffer : IVimBuffer option

    /// IChangeTracker for this IVim instance
    abstract ChangeTracker : IChangeTracker

    /// Is the VimRc loaded
    abstract IsVimRcLoaded : bool

    /// IKeyMap for this IVim instance
    abstract KeyMap : IKeyMap

    abstract MarkMap : IMarkMap

    abstract RegisterMap : IRegisterMap

    /// ISearchService for this IVim instance
    abstract SearchService : ISearchService

    abstract Settings : IVimGlobalSettings

    abstract VimData : IVimData 

    abstract VimHost : IVimHost

    /// The Local Setting's which were persisted from loading VimRc.  Empty if
    /// VimRc isn't loaded yet
    abstract VimRcLocalSettings : Setting list

    /// Create an IVimBuffer for the given IWpfTextView
    abstract CreateBuffer : ITextView -> IVimBuffer

    /// Get the IVimBuffer associated with the given view
    abstract GetBuffer : ITextView -> IVimBuffer option

    /// Get the IVimBuffer associated with the given view
    abstract GetBufferForBuffer : ITextBuffer -> IVimBuffer option

    /// Get or create an IVimBuffer for the given IWpfTextView
    abstract GetOrCreateBuffer : ITextView -> IVimBuffer

    /// Load the VimRc file.  If the file was previously loaded a new load will be 
    /// attempted.  This method will throw if the 
    abstract LoadVimRc : IFileSystem -> createViewFunc:(unit -> ITextView) -> bool

    /// Remove the IVimBuffer associated with the given view.  This will not actually close
    /// the IVimBuffer but instead just removes it's association with the given view
    abstract RemoveBuffer : ITextView -> bool

and SwitchModeEventArgs 
    (
        _previousMode : IMode option,
        _currentMode : IMode ) = 
    inherit System.EventArgs()

    /// Current IMode 
    member x.CurrentMode = _currentMode

    /// Previous IMode.  Expressed as an Option because the first mode switch
    /// has no previous one
    member x.PreviousMode = _previousMode


/// Main interface for the Vim editor engine so to speak. 
and IVimBuffer =

    /// Sequence of available Modes
    abstract AllModes : seq<IMode>

    /// Buffered KeyInput list.  When a key remapping has multiple source elements the input 
    /// is buffered until it is completed or the ambiguity is removed.  
    abstract BufferedRemapKeyInputs : KeyInput list

    /// IIncrementalSearch instance associated with this IVimBuffer
    abstract IncrementalSearch : IIncrementalSearch

    /// Whether or not the IVimBuffer is currently processing input
    abstract IsProcessingInput : bool

    /// Jump list
    abstract JumpList : IJumpList

    /// Associated IMarkMap
    abstract MarkMap : IMarkMap

    /// Current mode of the buffer
    abstract Mode : IMode

    /// ModeKind of the current IMode in the buffer
    abstract ModeKind : ModeKind

    /// Name of the buffer.  Used for items like Marks
    abstract Name : string

    /// Local settings for the buffer
    abstract Settings : IVimLocalSettings

    /// Register map for IVim.  Global to all IVimBuffer instances but provided here
    /// for convenience
    abstract RegisterMap : IRegisterMap

    /// Underyling ITextBuffer Vim is operating under
    abstract TextBuffer : ITextBuffer

    /// Current ITextSnapshot of the ITextBuffer
    abstract TextSnapshot : ITextSnapshot

    /// View of the file
    abstract TextView : ITextView

    /// Owning IVim instance
    abstract Vim : IVim

    /// Associated IVimData instance
    abstract VimData : IVimData

    // Mode accessors
    abstract NormalMode : INormalMode
    abstract CommandMode : ICommandMode 
    abstract DisabledMode : IDisabledMode
    abstract VisualLineMode : IVisualMode
    abstract VisualBlockMode : IVisualMode
    abstract VisualCharacterMode : IVisualMode
    abstract InsertMode : IMode
    abstract ReplaceMode : IMode
    abstract SubstituteConfirmMode : ISubstituteConfirmMode
    abstract ExternalEditMode : IMode


    abstract GetRegister : RegisterName -> Register

    /// Get the specified Mode
    abstract GetMode : ModeKind -> IMode
    
    /// Process the KeyInput and return whether or not the input was completely handled
    abstract Process : KeyInput -> bool

    /// Can the passed in KeyInput be consumed by the current state of IVimBuffer.  The
    /// provided KeyInput will participate in remapping based on the current mode
    abstract CanProcess: KeyInput -> bool

    /// Switch the current mode to the provided value
    abstract SwitchMode : ModeKind -> ModeArgument -> IMode

    /// Switch the buffer back to the previous mode which is returned
    abstract SwitchPreviousMode : unit -> IMode

    /// Called when the view is closed and the IVimBuffer should uninstall itself
    /// and it's modes
    abstract Close : unit -> unit
    
    /// Raised when the mode is switched.  Returns the old and new mode 
    [<CLIEvent>]
    abstract SwitchedMode : IEvent<SwitchModeEventArgs>

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
    abstract KeyInputStart : IEvent<KeyInput>

    /// Raised when a key is received but not immediately processed.  Occurs when a
    /// key remapping has more than one source key strokes
    [<CLIEvent>]
    abstract KeyInputBuffered : IEvent<KeyInput>

    /// Raised when a KeyInput is completed processing within the IVimBuffer.  This happens 
    /// if the KeyInput is buffered or processed
    [<CLIEvent>]
    abstract KeyInputEnd : IEvent<KeyInput>

    /// Raised when an error is encountered
    [<CLIEvent>]
    abstract ErrorMessage : IEvent<string>

    /// Raised when a status message is encountered
    [<CLIEvent>]
    abstract StatusMessage : IEvent<string>

    /// Raised when a long status message is encountered
    [<CLIEvent>]
    abstract StatusMessageLong : IEvent<string seq>

    /// Raised when the IVimBuffer is being closed
    [<CLIEvent>]
    abstract Closed : IEvent<System.EventArgs>

    inherit IPropertyOwner

and IMode =

    /// Owning IVimBuffer
    abstract VimBuffer : IVimBuffer 

    /// What type of Mode is this
    abstract ModeKind : ModeKind

    /// Sequence of commands handled by the Mode.  
    abstract CommandNames : seq<KeyInputSet>

    /// Can the mode process this particular KeyIput at the current time
    abstract CanProcess : KeyInput -> bool

    /// Process the given KeyInput
    abstract Process : KeyInput -> ProcessResult

    /// Called when the mode is entered
    abstract OnEnter : ModeArgument -> unit

    /// Called when the mode is left
    abstract OnLeave : unit -> unit

    /// Called when the owning IVimBuffer is closed so that the mode can free up 
    /// any resources including event handlers
    abstract OnClose : unit -> unit


and INormalMode =

    /// Buffered input for the current command
    abstract Command : string 

    /// The ICommandRunner implementation associated with NormalMode
    abstract CommandRunner : ICommandRunner 

    /// Mode keys need to be remapped with currently
    abstract KeyRemapMode : KeyRemapMode

    /// Is normal mode in the middle of a character replace operation
    abstract IsInReplace : bool

    /// If we are a one-time normal mode, the mode kind we will return to
    abstract OneTimeMode : ModeKind option

    inherit IMode

and ICommandMode = 

    /// buffered input for the current command
    abstract Command : string

    /// Run the specified command
    abstract RunCommand : string -> RunResult

    inherit IMode

and IVisualMode = 

    /// True during the duration of an explicit caret move from within Visual Mode.  Will be 
    /// false in cases where the caret is moved by a non-Vim item such as the user clicking
    /// or a third party component repositioning the caret
    abstract InExplicitMove : bool

    /// The ICommandRunner implementation associated with NormalMode
    abstract CommandRunner : ICommandRunner 

    /// Asks Visual Mode to reset what it perceives to be the original selection.  Instead it 
    /// views the current selection as the original selection for entering the mode
    abstract SyncSelection : unit -> unit

    inherit IMode 
    
and IDisabledMode =
    
    /// Help message to display 
    abstract HelpMessage : string 

    inherit IMode

and ISubstituteConfirmMode =

    /// The SnapshotSpan of the current matching piece of text
    abstract CurrentMatch : SnapshotSpan option

    /// The string which will replace the current match
    abstract CurrentSubstitute : string option

    /// Raised when the current match changes
    [<CLIEvent>]
    abstract CurrentMatchChanged : IEvent<SnapshotSpan option> 

    inherit IMode 

and IChangeTracker =
    
    abstract LastChange : RepeatableChange option

/// Represents a change which is repeatable 
and [<RequireQualifiedAccess>] RepeatableChange =
    | CommandChange of CommandRunData
    | TextChange of TextChange
    | LinkedChange of RepeatableChange * RepeatableChange

/// Responsible for calculating the new Span for a VisualMode change
type IVisualSpanCalculator =

    /// Calculate the new VisualSpan 
    abstract CalculateForTextView : textView:ITextView -> oldspan:VisualSpan -> VisualSpan

    /// Calculate the new VisualSpan for the the given point
    abstract CalculateForPoint : SnapshotPoint -> oldSpan:VisualSpan  -> VisualSpan


    
