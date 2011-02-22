#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Utilities
open System.Diagnostics

[<RequireQualifiedAccess>]
type JoinKind = 
    | RemoveEmptySpaces
    | KeepEmptySpaces

[<RequireQualifiedAccess>]
type ChangeCharacterKind =
    /// Switch the characters to upper case
    | ToUpperCase

    /// Switch the characters to lower case
    | ToLowerCase

    /// Toggle the case of the characters
    | ToggleCase

    /// Rot13 encode the letters
    | Rot13

/// Map containing the various VIM registers
type IRegisterMap = 

    /// Gets all of the available register name values
    abstract RegisterNames : seq<RegisterName>

    /// Get the register with the specified name
    abstract GetRegister : RegisterName -> Register

    /// Update the register with the specified value
    abstract SetRegisterValue : Register -> RegisterOperation -> RegisterValue -> unit

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

    /// Adds an ITextUndoPrimitive which will reset the selection to the current
    /// state when redoing this edit
    abstract AddAfterTextBufferChangePrimitive : unit -> unit

    /// Adds an ITextUndoPrimitive which will reset the selection to the current
    /// state when undoing this change
    abstract AddBeforeTextBufferChangePrimitive : unit -> unit

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

/// Result of an individual search
[<RequireQualifiedAccess>]
type SearchResult =
    | SearchFound of SearchData * SnapshotSpan
    | SearchNotFound of SearchData

    with 

    member x.SearchData = 
        match x with 
        | SearchResult.SearchFound (searchData, _) -> searchData
        | SearchResult.SearchNotFound searchData -> searchData

/// Global information about searches within Vim
type ISearchService = 

    /// Find the next occurrence of the pattern in the buffer starting at the 
    /// given SnapshotPoint
    abstract FindNext : SearchData -> SnapshotPoint -> ITextStructureNavigator -> SnapshotSpan option

    /// Find the next Nth occurrence of the pattern
    abstract FindNextMultiple : SearchData -> SnapshotPoint -> ITextStructureNavigator -> count:int -> SnapshotSpan option


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

/// Char searches are interesting because they are definide in one IVimBuffer
/// and can be repeated in any IVimBuffer.  Use a discriminated union here 
/// to name the motion without tieing it to a given IVimBuffer or ITextView 
/// which would increase the chance of an accidental memory leak
[<RequireQualifiedAccess>]
type CharSearchKind  =

    /// Used for the 'f' and 'F' motion.  To the specified char 
    | ToChar

    /// Used for the 't' and 'T' motion.  Till the specified char
    | TillChar

/// A discriminated union of the Motion types supported.  These are the primary
/// repeat mechanisms for Motion arguments so it's very important that these 
/// are ITextView / IVimBuffer agnostic.  It will be very common for a Motion 
/// item to be stored and then applied to many IVimBuffer instances.
[<RequireQualifiedAccess>]
type Motion =

    /// Implement the 'aw' motion.  This is called once the a key is seen.
    | AllWord of WordKind

    /// Implement the 'ap' motion
    | AllParagraph 

    /// Gets count full sentences from the cursor.  If used on a blank line this will
    /// not return a value
    | AllSentence

    /// Move to the begining of the line.  Interestingly since this command is bound to the '0' it 
    /// can't be associated with a count.  Doing a command like 30 binds as count 30 vs. count 3 
    /// for command '0'
    | BeginingOfLine

    /// The left motion for h, <Left>, etc ...
    | CharLeft 

    /// The right motion for l, <Right>, etc ...
    | CharRight

    /// Implements the f, F, t and T motions
    | CharSearch of CharSearchKind * Direction * char

    /// Implement the 'e' motion.  This goes to the end of the current word.  If we're
    /// not currently on a word it will find the next word and then go to the end of that
    | EndOfWord of WordKind
    
    /// Implement an end of line motion.  Typically in response to the $ key.  Even though
    /// this motion deals with lines, it's still a character wise motion motion. 
    | EndOfLine

    /// Find the first non-whitespace character as the start of the span.  This is an exclusive
    /// motion so be careful we don't go to far forward
    | FirstNonWhiteSpaceOnLine

    /// Find the last non-whitespace character on the line.  Count causes it to go "count" lines
    /// down and perform the search
    | LastNonWhiteSpaceOnLine

    /// Handle the lines down to first non-whitespace motion.  This is one of the motions which 
    /// can accept a count of 0.
    | LineDownToFirstNonWhiteSpace

    /// Handle the - motion
    | LineUpToFirstNonWhiteSpace

    /// Get the span of "count" lines upward careful not to run off the beginning of the
    /// buffer.  Implementation of the "k" motion
    | LineUp

    /// Get the span of "count" lines downward careful not to run off the end of the
    /// buffer.  Implementation of the "j" motion
    | LineDown

    /// Go to the specified line number or the first line if no line number is provided 
    | LineOrFirstToFirstNonWhiteSpace

    /// Go to the specified line number or the last line of no line number is provided
    | LineOrLastToFirstNonWhiteSpace

    /// Go to the "count - 1" line from the top of the visible window.  If the count exceeds
    /// the number of visible lines it will end on the last visible line
    | LineFromTopOfVisibleWindow

    /// Go to the "count -1" line from the bottom of the visible window.  If the count 
    /// exceeds the number of visible lines it will end on the first visible line
    | LineFromBottomOfVisibleWindow

    /// Go to the middle line in the visible window.  
    | LineInMiddleOfVisibleWindow

    /// Get the motion to the specified mark.  This is typically accessed via
    /// the ` (backtick) operator and results in an exclusive motion
    | Mark of char

    /// Get the motion to the line of the specified mark.  This is typically
    /// accessed via the ' (single quote) operator and results in a 
    /// linewise motion
    | MarkLine of char

    /// Get the matching token from the next token on the line.  This is used to implement
    /// the % motion
    | MatchingToken 

    /// Count pargraphs backwards
    | ParagraphBackward

    /// Count paragraphs forward
    | ParagraphForward

    /// The quoted string including the quotes
    | QuotedString

    /// The quoted string excluding the quotes
    | QuotedStringContents

    /// Repeat the last CharSearch value
    | RepeatLastCharSearch

    /// Repeat the last CharSearch value in the opposite direction
    | RepeatLastCharSearchOpposite

    /// A search for the specified pattern
    | Search of SearchData

    /// Backward a section in the editor or to a close brace
    | SectionBackwardOrCloseBrace
    
    /// Backward a section in the editor or to an open brace
    | SectionBackwardOrOpenBrace

    /// Forward a section in the editor
    | SectionForwardOrCloseBrace

    /// Forward a section in the editor
    | SectionForwardOrOpenBrace

    /// Count sentences backward 
    | SentenceBackward

    /// Count sentences forward
    | SentenceForward

    /// Implement the b/B motion
    | WordBackward of WordKind

    /// Implement the w/W motion
    | WordForward of WordKind 

/// Interface for running Motion instances against an ITextView
and ITextViewMotionUtil =

    /// The associated ITextView instance
    abstract TextView : ITextView

    /// Get the specified Motion value 
    abstract GetMotion : Motion -> MotionArgument -> MotionResult option 

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
    static member OfModeKind kind = 
        match kind with 
        | ModeKind.VisualBlock -> VisualKind.Block |> Some
        | ModeKind.VisualLine -> VisualKind.Line |> Some
        | ModeKind.VisualCharacter -> VisualKind.Character |> Some
        | _ -> None
    static member IsAnyVisual kind = VisualKind.OfModeKind kind |> Option.isSome

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
    member x.Add ki =
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

    member x.CompareTo (other : KeyInputSet) = 
        let rec inner (left:KeyInput list) (right:KeyInput list) =
            if left.IsEmpty && right.IsEmpty then 0
            elif left.IsEmpty then -1
            elif right.IsEmpty then 1
            elif left.Head < right.Head then -1
            elif left.Head > right.Head then 1
            else inner (List.tail left) (List.tail right)
        inner x.KeyInputs other.KeyInputs

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
            | :? KeyInputSet as y -> x.CompareTo y
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

    /// Begins insert mode with a specified count.  This means the text inserted should
    /// be repeated a total of 'count - 1' times when insert mode exits
    | InsertWithCount of int

type ModeSwitch =
    | NoSwitch
    | SwitchMode of ModeKind
    | SwitchModeWithArgument of ModeKind * ModeArgument
    | SwitchPreviousMode 

[<RequireQualifiedAccess>]
type CommandResult =   

    /// The command completed and requested a switch to the provided Mode which 
    /// may just be a no-op
    | Completed  of ModeSwitch

    /// An error was encountered and the command was unable to run
    | Error

[<RequireQualifiedAccess>]
type RunResult = 
    | Completed
    | SubstituteConfirm of SnapshotSpan * SnapshotLineRange * SubstituteData

[<RequireQualifiedAccess>]
type VisualSpan =

    /// A characterwise span
    | Character of SnapshotSpan

    /// A linewise span
    | Line of SnapshotLineRange

    /// A block span
    | Block of NonEmptyCollection<SnapshotSpan>

    with

    /// Return the Spans which make up this VisualSpan instance
    member x.Spans = 
        match x with 
        | VisualSpan.Character span -> [span] |> Seq.ofList
        | VisualSpan.Line range -> [range.ExtentIncludingLineBreak] |> Seq.ofList
        | VisualSpan.Block col -> col.All

    /// Returns the EditSpan for this VisualSpan
    member x.EditSpan = 
        match x with
        | VisualSpan.Character span -> EditSpan.Single span
        | VisualSpan.Line range -> EditSpan.Single range.ExtentIncludingLineBreak
        | VisualSpan.Block col -> EditSpan.Block col

    /// Returns the start point of the Visual Span.  This can be None in the case
    /// of an empty Block selection.
    member x.Start =
        match x with
        | Character span -> span.Start
        | Line range ->  range.Start
        | Block col -> col.Head.Start

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

/// Data about the run of a given MotionResult
type MotionData = {

    /// The associated Motion value
    Motion : Motion

    /// The argument which should be supplied to the given Motion
    MotionArgument : MotionArgument
}

/// REPEAT TODO: Need to clean up the terminology around Motion structures.  Most
/// use the Run modifier to represent what was run.  For now use these type defs

/// Data needed to execute a command
type CommandData = {

    /// The raw count provided to the command
    Count : int option 

    /// The register name specified for the command 
    RegisterName : RegisterName option

} with

    /// Return the provided count or the default value of 1
    member x.CountOrDefault = 
        match x.Count with 
        | Some count -> count
        | None -> 1

    /// Get the provided register name or the default name (Unnamed)
    member x.RegisterNameOrDefault =
        match x.RegisterName with
        | Some name -> name
        | None -> RegisterName.Unnamed

    /// Get the applicable register
    member x.GetRegister (map : IRegisterMap) = map.GetRegister x.RegisterNameOrDefault

/// We want the NormalCommand discriminated union to have structural equality in order
/// to ease testing requirements.  In order to do this and support Ping we need a 
/// separate type here to wrap the Func to be comparable.  Does so in a reference 
/// fashion
type PingData (_func : CommandData -> unit) = 

    member x.Function = _func

    static member op_Equality (this, other) = System.Object.ReferenceEquals(this, other)
    static member op_Inequality (this, other) = not (System.Object.ReferenceEquals(this, other))
    override x.GetHashCode() = 1
    override x.Equals(obj) = System.Object.ReferenceEquals(x, obj)
    interface System.IEquatable<PingData> with
        member x.Equals other = x.Equals(other)

/// We want the Command discriminated union to have structural equality in order
/// to ease testing requirements.  In order to do this and support LegacyCommands 
/// for the time being we need a separate type here to wrap the Func to be comparable.  
/// Does so in a reference fashion
/// REPEAT TODO: Delete this when legacy commands go away
type LegacyData (_func : unit -> CommandResult) = 

    member x.Function = _func

    static member op_Equality (this, other) = System.Object.ReferenceEquals(this, other)
    static member op_Inequality (this, other) = not (System.Object.ReferenceEquals(this, other))
    override x.GetHashCode() = 1
    override x.Equals(obj) = System.Object.ReferenceEquals(x, obj)
    interface System.IEquatable<PingData> with
        member x.Equals other = x.Equals(other)

/// Normal mode commands which can be executed by the user 
[<RequireQualifiedAccess>]
[<StructuralEquality>]
[<NoComparison>]
type NormalCommand = 

    /// Deletes the text specified by the motion and begins insert mode. Implements the "c" 
    /// command
    | ChangeMotion of MotionData

    /// Change the characters on the caret line 
    | ChangeCaseCaretLine of ChangeCharacterKind

    /// Change the characters on the caret line 
    | ChangeCaseCaretPoint of ChangeCharacterKind

    /// Change case of the specified motion
    | ChangeCaseMotion of ChangeCharacterKind * MotionData

    /// Delete 'count' lines and begin insert mode
    | ChangeLines

    /// Delete the text till the end of the line in the same manner as DeleteTillEndOfLine
    /// and start Insert Mode
    | ChangeTillEndOfLine

    /// Delete the character at the current cursor position.  Implements the "x" command
    | DeleteCharacterAtCaret

    /// Delete the character before the cursor. Implements the "X" command
    | DeleteCharacterBeforeCaret

    /// Delete lines from the buffer: dd
    | DeleteLines

    /// Delete the specified motion of text
    | DeleteMotion of MotionData

    /// Delete till the end of the line and 'count - 1' more lines down
    | DeleteTillEndOfLine

    /// Format the specified lines
    | FormatLines

    /// Format the specified motion
    | FormatMotion of MotionData

    /// Switch to insert mode
    | Insert

    /// Insert text at the first non-blank line in the current line
    | InsertAtFirstNonBlank

    /// Join the specified lines
    | JoinLines of JoinKind

    /// Jump to the specified mark 
    | JumpToMark of char

    /// Move the caret to the result of the given Motion
    | MoveCaretToMotion of Motion

    /// Not actually a Vim Command.  This is a simple ping command which makes 
    /// testing items like complex repeats significantly easier
    | Ping of PingData

    /// Put the contents of the register into the buffer after the cursor.  The bool is 
    /// whether or not the caret should be placed after the inserted text
    | PutAfterCaret of bool

    /// Put the contents of the register into the buffer before the cursor.  The bool is 
    /// whether or not the caret should be placed after the inserted text
    | PutBeforeCaret of bool

    /// Repeat the last command
    | RepeatLastCommand

    /// Replace the char under the cursor with the given char
    | ReplaceChar of KeyInput

    /// Set the specified mark to the current value of the caret
    | SetMarkToCaret of char

    /// Shift 'count' lines from the cursor left
    | ShiftLinesLeft

    /// Shift 'count' lines from the cursor right
    | ShiftLinesRight

    /// Shift 'motion' lines from the cursor left
    | ShiftMotionLinesLeft of MotionData

    /// Shift 'motion' lines from the cursor right
    | ShiftMotionLinesRight of MotionData

    /// Substitute the character at the cursor
    | SubstituteCharacterAtCaret

    /// Yank the given motion into a register
    | Yank of MotionData

/// Visual mode commands which can be executed by the user 
[<RequireQualifiedAccess>]
type VisualCommand = 

    /// Change the case of the selected text in the specified manner
    | ChangeCase of ChangeCharacterKind

    /// Delete the selected lines
    | DeleteLineSelection

    /// Delete the selected text and put it into a register
    | DeleteSelection

    /// Format the selected text
    | FormatLines

    /// Put the contents of the register into the buffer after the selection.  The bool is 
    /// whether or not the caret should be placed after the inserted text
    | PutOverSelection of bool

    /// Replace the visual span with the provided character
    | ReplaceSelection of KeyInput

    /// Shift the selected lines left
    | ShiftLinesLeft

    /// Shift the selected lines to the right
    | ShiftLinesRight

/// Commands which can be executed by the user
[<RequireQualifiedAccess>]
[<StructuralEquality>]
[<NoComparison>]
type Command =

    /// A Normal Mode Command
    | NormalCommand of NormalCommand * CommandData

    /// A Visual Mode Command
    | VisualCommand of VisualCommand * CommandData * VisualSpan

    /// A Legacy command was run
    /// 
    /// REPEAT TODO: Delete this once legacy commands are eliminated
    | LegacyCommand of LegacyData

/// The result of binding to a Motion value.
[<RequireQualifiedAccess>]
type BindResult<'T> = 

    /// Successfully bound to a value
    | Complete of 'T 

    /// More input is needed to complete the binding operation
    | NeedMoreInput of BindData<'T>

    /// There was an error completing the binding operation
    | Error

    /// Motion was cancelled via user input
    | Cancelled

    with

    static member CreateNeedMoreInput keyRemapModeOpt bindFunc =
        let data = { KeyRemapMode = keyRemapModeOpt; BindFunction = bindFunc }
        NeedMoreInput data

    /// Used to convert a BindResult<'T> to BindResult<'U> through a conversion
    /// function
    member x.Convert mapFunc = 
        match x with
        | Complete value -> Complete (mapFunc value)
        | NeedMoreInput bindData -> NeedMoreInput (bindData.Convert mapFunc)
        | Error -> Error
        | Cancelled -> Cancelled

and BindData<'T> = {

    /// The optional KeyRemapMode which should be used when binding
    /// the next KeyInput in the sequence
    KeyRemapMode : KeyRemapMode option
    
    /// Function to call to get the BindResult for this data
    BindFunction : KeyInput -> BindResult<'T>

} with

    /// Many bindings are simply to get a single KeyInput.  Centralize that logic 
    /// here so it doesn't need to be repeated
    static member CreateForSingle keyRemapModeOpt completeFunc =
        let inner keyInput =
            if keyInput = KeyInputUtil.EscapeKey then
                BindResult.Cancelled
            else
                let data = completeFunc keyInput
                BindResult<'T>.Complete data
        { KeyRemapMode = keyRemapModeOpt; BindFunction = inner }

    /// Many bindings are simply to get a single char.  Centralize that logic 
    /// here so it doesn't need to be repeated
    static member CreateForSingleChar keyRemapModeOpt completeFunc = 
        BindData<_>.CreateForSingle keyRemapModeOpt (fun keyInput -> completeFunc keyInput.Char)

    /// Create for a function which doesn't require any remapping
    static member CreateForSimple bindFunc =
        { KeyRemapMode = None; BindFunction = bindFunc }

    /// Often types bindings need to compose together because we need an inner binding
    /// to succeed so we can create a projected value.  This function will allow us
    /// to translate a BindData.Completed<'T> -> BindData.Completed<'U>
    member x.Convert convertFunc = 

        let rec inner bindFunction keyInput = 
            match x.BindFunction keyInput with
            | BindResult.Cancelled -> BindResult.Cancelled
            | BindResult.Complete value -> BindResult.Complete (convertFunc value)
            | BindResult.Error -> BindResult.Error
            | BindResult.NeedMoreInput bindData -> BindResult.NeedMoreInput (bindData.Convert convertFunc)

        { KeyRemapMode = x.KeyRemapMode; BindFunction = inner x.BindFunction }

    /// Very similar to the Convert function.  This will instead map a BindData<'T>.Completed
    /// to a BindData<'U> of any form 
    member x.Map mapFunc = 

        let rec inner bindFunction keyInput = 
            match x.BindFunction keyInput with
            | BindResult.Cancelled -> BindResult.Cancelled
            | BindResult.Complete value -> mapFunc value
            | BindResult.Error -> BindResult.Error
            | BindResult.NeedMoreInput bindData -> BindResult.NeedMoreInput (bindData.Map mapFunc)

        { KeyRemapMode = x.KeyRemapMode; BindFunction = inner x.BindFunction }

/// Several types of BindData<'T> need to take an actiov when a binding begins against
/// themselves.  This action needs to occur before the first KeyInput value is processed
/// and hence they need a jump start.  The most notable is IncrementalSearch which 
/// needs to enter 'Search' mode before processing KeyInput values so the cursor can
/// be updated
[<RequireQualifiedAccess>]
type BindDataStorage<'T> =

    /// Simple BindData<'T> which doesn't require activation
    | Simple of BindData<'T> 

    /// Complex BindData<'T> which does require activation
    | Complex of (unit -> BindData<'T>)

    with

    /// Creates the BindData
    member x.CreateBindData () = 
        match x with
        | Simple bindData -> bindData
        | Complex func -> func()

    /// Convert from a BindDataStorage<'T> -> BindDataStorage<'U>.  The 'mapFunc' value
    /// will run on the final 'T' data if it eventually is completed
    member x.Convert mapFunc = 
        match x with
        | Simple bindData -> Simple (bindData.Convert mapFunc)
        | Complex func -> Complex (fun () -> func().Convert mapFunc)

/// Representation of commands within Vim.  
/// 
/// REPEAT TODO: This should evolve into CommandBinding which has 
///     Simple of KeyInputSet * Command
///     Motion of KeyInputSet * (MotionData -> Command)
/// REPEAT TODO: This should have requires qualified access
[<DebuggerDisplay("{ToString(),nq}")>]
[<RequireQualifiedAccess>]
type CommandBinding = 

    /// Represents a Command which has no motion modifiers.  The  delegate takes 
    /// an optional count and a Register.  If unspecified the default register
    /// will be used
    | LegacySimpleCommand of KeyInputSet * CommandFlags * (int option -> Register -> CommandResult)

    /// Represents a Command prefix which has an associated motion.  The delegate takes
    /// an optional count, a Register and a MotionResult value.  If unspecified the default
    /// register will be used
    | LegacyMotionCommand of KeyInputSet * CommandFlags * (int option -> Register -> MotionResult -> CommandResult)

    /// Represents a command which has a name and relies on the Visual Mode Span to 
    /// execute the command
    | LegacyVisualCommand of KeyInputSet * CommandFlags * VisualKind * (int option -> Register -> VisualSpan -> CommandResult) 

    /// KeyInputSet bound to a particular NormalCommand instance
    | NormalCommand of KeyInputSet * CommandFlags * NormalCommand

    /// KeyInputSet bound to a complex NormalCommand instance
    | ComplexNormalCommand of KeyInputSet * CommandFlags * BindDataStorage<NormalCommand>

    /// KeyInputSet bound to a particular NormalCommand instance which takes a Motion Argument
    | MotionCommand of KeyInputSet * CommandFlags * (MotionData -> NormalCommand)

    /// KeyInputSet bound to a particular VisualCommand instance
    | VisualCommand of KeyInputSet * CommandFlags * VisualCommand

    /// KeyInputSet bound to a complex VisualCommand instance
    | ComplexVisualCommand of KeyInputSet * CommandFlags * BindDataStorage<VisualCommand>

    with 

    /// The raw command inputs
    member x.KeyInputSet = 
        match x with
        | LegacySimpleCommand(value, _, _ ) -> value
        | LegacyMotionCommand(value, _, _) -> value
        | LegacyVisualCommand(value, _, _, _) -> value
        | NormalCommand (value, _, _) -> value
        | MotionCommand (value, _, _) -> value
        | VisualCommand (value, _, _) -> value
        | ComplexNormalCommand (value, _, _) -> value
        | ComplexVisualCommand (value, _, _) -> value

    /// The kind of the Command
    member x.CommandFlags =
        match x with
        | LegacySimpleCommand(_, value, _ ) -> value
        | LegacyMotionCommand(_, value, _) -> value
        | LegacyVisualCommand(_, value, _, _) -> value
        | NormalCommand (_, value, _) -> value
        | MotionCommand (_, value, _) -> value
        | VisualCommand (_, value, _) -> value
        | ComplexNormalCommand (_, value, _) -> value
        | ComplexVisualCommand (_, value, _) -> value

    /// Is the Repeatable flag set
    member x.IsRepeatable = Util.IsFlagSet x.CommandFlags CommandFlags.Repeatable

    /// Is the HandlesEscape flag set
    member x.HandlesEscape = Util.IsFlagSet x.CommandFlags CommandFlags.HandlesEscape

    /// Is the Movement flag set
    member x.IsMovement = Util.IsFlagSet x.CommandFlags CommandFlags.Movement

    /// Is the Special flag set
    member x.IsSpecial = Util.IsFlagSet x.CommandFlags CommandFlags.Special

    override x.ToString() = System.String.Format("{0} -> {1}", x.KeyInputSet, x.CommandFlags)

/// Used to exceute commands
and ICommandUtil = 

    /// Run a normal command
    abstract RunNormalCommand : NormalCommand -> CommandData -> CommandResult

    /// Run a visual command
    abstract RunVisualCommand : VisualCommand -> CommandData -> VisualSpan -> CommandResult

    /// Run a command
    abstract RunCommand : Command -> CommandResult

/// Contains the stored information about a Visual Span.  This instance *will* be 
/// stored for long periods of time and used to erpeat a Command instance across
/// mulitiple IVimBuffer instances so it must be buffer agnostic
[<RequireQualifiedAccess>]
type StoredVisualSpan = 

    /// Storing a character wise span.  Only need to know how many lines down is 
    /// the end point and what is the offset of the end point
    | Character of int * int

    /// Storing a linewise span just stores the count of lines
    | Line of int

    /// Storing of a block span records the length of the span and the number of
    /// lines which should be affected by the Span
    | Block of int * int

    with

    /// Create a StoredVisualSpan from the provided VisualSpan value
    static member OfVisualSpan visualSpan = 
        match visualSpan with
        | VisualSpan.Character span ->

            // Break the span into leading, middle and trailing edges and use
            // those to calculate the offsets
            let startSpan, middle, endSpan = SnapshotSpanUtil.GetLinesAndEdges span

            // How many full lines in the middle
            let middleLines = 
                match middle with
                | None -> 0
                | Some range -> 

                    // Deal with the special case where there is no start span because
                    // the first line is a full line
                    match startSpan with
                    | None -> range.Count - 1
                    | Some _ -> range.Count

            let endLineOffset, endOffset = 
                match endSpan with
                | None -> 
                    if middleLines > 0 then 
                        // No end span but we have middle lines.  The end offset should
                        // be at column 0 one line further down
                        (middleLines + 1, 0)
                    else
                        // No middle lines or end span.  Single line guy
                        (0, span.Length)
                | Some endSpan ->
                    // There is an end span.  It's 1 line further down from the middle guys
                    // and ends at the length
                    (middleLines + 1, SnapshotPointUtil.GetColumn endSpan.End)
            StoredVisualSpan.Character (endLineOffset, endOffset)
        | VisualSpan.Line range ->
            StoredVisualSpan.Line range.Count
        | VisualSpan.Block col -> 
            let length = 
                match SeqUtil.tryHeadOnly col with
                | None -> 0
                | Some span -> span.Length
            let count = col.Count
            StoredVisualSpan.Block (length, count)

[<RequireQualifiedAccess>]
type TextChange = 
    | Insert of string
    | Delete of int

/// Contains information about an executed Command.  This instance *will* be stored
/// for long periods of time and used to repeat a Command instance across multiple
/// IVimBuffer instances so it simply cannot store any state specific to an 
/// ITextView instance.  It must be completely agnostic of such information 
[<RequireQualifiedAccess>]
type StoredCommand =

    /// The stored information about a NormalCommand
    | NormalCommand of NormalCommand * CommandData * CommandFlags

    /// The stored information about a VisualCommand
    | VisualCommand of VisualCommand * CommandData * StoredVisualSpan * CommandFlags


    /// A Text Change which ocurred 
    | TextChangeCommand of TextChange

    /// A Linked Command links together 2 other StoredCommand objects so they
    /// can be repeated together.
    | LinkedCommand of StoredCommand * StoredCommand

    /// A Legacy command.  These cannot be repeated but we store them because it 
    /// makes it easier to spot a Legacy command linked with a repeat text change.  
    ///
    /// REPEAT TODO: Delete this when all legacy commands are done
    | LegacyCommand of KeyInputSet * CommandFlags

    with

    /// The CommandFlags associated with this StoredCommand
    member x.CommandFlags =
        match x with 
        | NormalCommand (_, _, flags) -> flags
        | VisualCommand (_, _, _, flags) -> flags
        | TextChangeCommand _ -> CommandFlags.None
        | LinkedCommand _ -> CommandFlags.None
        | LegacyCommand (_, flags) -> flags

    /// Create a StoredCommand instance from the given Command value
    static member OfCommand command (commandBinding : CommandBinding) = 
        match command with 
        | Command.NormalCommand (command, data) -> 
            StoredCommand.NormalCommand (command, data, commandBinding.CommandFlags)
        | Command.VisualCommand (command, data, visualSpan) ->
            let storedVisualSpan = StoredVisualSpan.OfVisualSpan visualSpan
            StoredCommand.VisualCommand (command, data, storedVisualSpan, commandBinding.CommandFlags)
        | Command.LegacyCommand _ ->
            StoredCommand.LegacyCommand (commandBinding.KeyInputSet, commandBinding.CommandFlags)

/// Flags about specific motions
[<RequireQualifiedAccess>]
[<System.Flags>]
type MotionFlags =

    | None = 0x0

    /// This type of motion can be used to move the cursor
    | CursorMovement = 0x1 

    /// Text object selection motions.  These can be used for cursor movement inside of 
    /// Visual Mode but otherwise need to be used only after operators.  
    /// :help text-objects
    | TextObjectSelection = 0x2

    /// The motion function wants to specially handle the esape function.  This is used 
    /// on Complex motions such as / and ? 
    | HandlesEscape = 0x4

/// Represents the types of MotionCommands which exist
[<RequireQualifiedAccess>]
type MotionBinding =

    /// Simple motion which comprises of a single KeyInput and a function which given 
    /// a start point and count will produce the motion.  None is returned in the 
    /// case the motion is not valid
    | Simple of KeyInputSet * MotionFlags * Motion

    /// Complex motion commands take more than one KeyInput to complete.  For example 
    /// the f,t,F and T commands all require at least one additional input.  The bool
    /// in the middle of the tuple indicates whether or not the motion can be 
    /// used as a cursor movement operation  
    | Complex of KeyInputSet * MotionFlags * BindDataStorage<Motion>

    with

    member x.KeyInputSet = 
        match x with
        | Simple (name, _, _) -> name
        | Complex (name, _, _) -> name

    member x.MotionFlags =
        match x with 
        | Simple (_, flags, _) -> flags
        | Complex (_, flags, _) -> flags

/// The information about the particular run of a Command
type CommandRunData = {

    /// The binding which the command was invoked from
    CommandBinding : CommandBinding

    /// The Command which was run
    Command : Command

    /// The result of the Command Run
    CommandResult : CommandResult

} with

    /// The ModeSwitch associated with this CommandRunData instance
    member x.ModeSwitch = 
        match x.CommandResult with
        | CommandResult.Completed modeSwitch -> modeSwitch
        | CommandResult.Error -> ModeSwitch.NoSwitch

/// Responsible for binding key input to a Motion and MotionArgument tuple.  Does
/// not actually run the motions
type IMotionCapture =

    /// Associated ITextView
    abstract TextView : ITextView
    
    /// Set of MotionBinding values supported
    abstract MotionBindings : seq<MotionBinding>

    /// Get the motion starting with the given KeyInput
    abstract GetOperatorMotion : KeyInput -> BindResult<Motion * int option>

module CommandUtil2 = 

    let CountOrDefault opt = 
        match opt with 
        | Some(count) -> count
        | None -> 1

/// Responsible for managing a set of Commands and running them
type ICommandRunner =

    /// Set of Commands currently supported
    abstract Commands : CommandBinding seq

    /// In certain circumstances a specific type of key remapping needs to occur for input.  This 
    /// option will have the appropriate value in those circumstances.  For example while processing
    /// the {char} argument to f,F,t or T the Language mapping will be used
    abstract KeyRemapMode : KeyRemapMode option

    /// Is the command runner currently binding a command which needs to explicitly handly escape
    abstract IsHandlingEscape : bool

    /// True if waiting on more input
    abstract IsWaitingForMoreInput : bool

    /// Add a Command.  If there is already a Command with the same name an exception will
    /// be raised
    abstract Add : CommandBinding -> unit

    /// Remove a command with the specified name
    abstract Remove : KeyInputSet -> unit

    /// Process the given KeyInput.  If the command completed it will return a result.  A
    /// None value implies more input is needed to finish the operation
    abstract Run : KeyInput -> BindResult<CommandRunData>

    /// If currently waiting for more input on a Command, reset to the 
    /// initial state
    abstract ResetState : unit -> unit

    /// Raised when a command is successfully run
    [<CLIEvent>]
    abstract CommandRan : IEvent<CommandRunData>

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

type IIncrementalSearch = 

    /// True when a search is occuring
    abstract InSearch : bool

    /// When in the middle of a search this will return the SearchData for 
    /// the search
    abstract CurrentSearch : SearchData option

    /// ISearchInformation instance this incremental search is associated with
    abstract SearchService : ISearchService

    /// The ITextStructureNavigator used for finding 'word' values in the ITextBuffer
    abstract WordNavigator : ITextStructureNavigator

    /// Begin an incremental search in the ITextBuffer
    abstract Begin : SearchKind -> BindData<SearchResult>

    [<CLIEvent>]
    abstract CurrentSearchUpdated : IEvent<SearchResult>

    [<CLIEvent>]
    abstract CurrentSearchCompleted : IEvent<SearchResult>

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

    /// Is the value calculated
    member x.IsValueCalculated =
        match x.Value with
        | CalculatedValue(_) -> true
        | _ -> false

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
    let UseEditorIndentName = "vsvim_useeditorindent"
    let UseEditorTabSettingsName = "vsvim_useeditortabsettings"
    let VisualBellName = "visualbell"
    let VirtualEditName = "virtualedit"
    let VimRcName = "vimrc"
    let VimRcPathsName = "vimrcpaths"
    let WrapScanName = "wrapscan"

module LocalSettingNames =

    let AutoIndentName = "autoindent"
    let CursorLineName = "cursorline"
    let ExpandTabName = "expandtab"
    let NumberName = "number"
    let ScrollName = "scroll"
    let TabStopName = "tabstop"
    let QuoteEscapeName = "quoteescape"

/// Holds mutable data available to all buffers
type IVimData = 

    /// Data for the last substitute command performed
    abstract LastSubstituteData : SubstituteData option with get, set

    /// Last pattern searched for in any buffer
    abstract LastSearchData : SearchData with get, set

    /// Motion function used with the last f, F, t or T motion.  The 
    /// first item in the tuple is the forward version and the second item
    /// is the backwards version
    abstract LastCharSearch : (CharSearchKind * Direction * char) option with get, set

    /// The last command which was ran 
    abstract LastCommand : StoredCommand option with get, set

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

    /// Is the Selection setting set to a value which calls for inclusive 
    /// selection.  This does not directly track if Setting = "inclusive" 
    /// although that would cause this value to be true
    abstract IsSelectionInclusive : bool with get

    /// Is the Selection setting set to a value which permits the selection
    /// to extend past the line
    abstract IsSelectionPastLine : bool with get

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

    /// Let the editor control indentation of lines instead.  Overrides the AutoIndent
    /// setting
    abstract UseEditorIndent : bool with get, set

    /// Use the editor tab setting over the ExpandTab one
    abstract UseEditorTabSettings : bool with get, set

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

    /// Whether or not searches should wrap at the end of the file
    abstract WrapScan : bool with get, set

    abstract DisableCommand: KeyInput;

    inherit IVimSettings

/// Settings class which is local to a given IVimBuffer.  This will hide the work of merging
/// global settings with non-global ones
and IVimLocalSettings =

    abstract AutoIndent : bool with get, set

    /// Whether or not to highlight the line the cursor is on
    abstract CursorLine : bool with get, set

    /// Whether or not to expand tabs into spaces
    abstract ExpandTab : bool with get, set

    /// Return the handle to the global IVimSettings instance
    abstract GlobalSettings : IVimGlobalSettings

    /// How many spaces a tab counts for 
    abstract TabStop : int with get, set

    abstract Scroll : int with get, set

    /// Which characters escape quotes for certain motion types
    abstract QuoteEscape : string with get, set

    inherit IVimSettings

/// Vim instance.  Global for a group of buffers
and IVim =

    /// Buffer actively processing input.  This has no relation to the IVimBuffer
    /// which has focus 
    abstract ActiveBuffer : IVimBuffer option

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

    /// The Local Setting's which were persisted from loading VimRc.  If the 
    /// VimRc isn't loaded yet or if no VimRc was loaded a IVimLocalSettings
    /// with all default values will be returned.  Will store a copy of whatever 
    /// is passed in order to prevent memory leaks from captured ITextView`s
    abstract VimRcLocalSettings : IVimLocalSettings with get, set

    /// Create an IVimBuffer for the given IWpfTextView
    abstract CreateBuffer : ITextView -> IVimBuffer

    /// Get the IVimBuffer associated with the given view
    abstract GetBuffer : ITextView -> IVimBuffer option

    /// Get the IVimBuffer associated with the given view
    abstract GetBufferForBuffer : ITextBuffer -> IVimBuffer option

    /// Get or create an IVimBuffer for the given IWpfTextView
    abstract GetOrCreateBuffer : ITextView -> IVimBuffer

    /// Load the VimRc file.  If the file was previously loaded a new load will be 
    /// attempted.  Returns true if a VimRc was actually loaded
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

    /// The ITextViewMotionUtil associated with this IVimBuffer instance
    abstract TextViewMotionUtil : ITextViewMotionUtil

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

