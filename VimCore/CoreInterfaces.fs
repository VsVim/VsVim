#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Utilities
open System.Diagnostics
open System.Runtime.CompilerServices

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

    /// Raised when there is a warning message that needs to be reported
    abstract OnWarning : string -> unit 

/// Factory for getting IStatusUtil instances.  This is an importable MEF component
type IStatusUtilFactory =

    /// Get the IStatusUtil instance for the given ITextView
    abstract GetStatusUtil : ITextView -> IStatusUtil

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

/// Utility functions relating to Word values in an ITextBuffer
type IWordUtil = 

    /// The ITextBuffer associated with this word utility
    abstract TextBuffer : ITextBuffer

    /// Get the full word span for the word value which crosses the given SnapshotPoint
    abstract GetFullWordSpan : WordKind -> SnapshotPoint -> SnapshotSpan option

    /// Get the SnapshotSpan for Word values from the given point.  If the provided point is 
    /// in the middle of a word the span of the entire word will be returned
    abstract GetWords : WordKind -> Path -> SnapshotPoint -> SnapshotSpan seq

    /// Create an ITextStructureNavigator where the extent of words is calculated for
    /// the specified WordKind value
    abstract CreateTextStructureNavigator : WordKind -> ITextStructureNavigator

/// Factory for getting IWordUtil instances.  This is an importable MEF component
type IWordUtilFactory = 

    /// Get the IWordUtil instance for the given ITextView
    abstract GetWordUtil : ITextBuffer -> IWordUtil

/// Used to display a word completion list to the user
type IWordCompletionSession =

    /// Is the session dismissed
    abstract IsDismissed : bool

    /// The associated ITextView instance
    abstract TextView : ITextView

    /// Select the next word in the session
    abstract MoveNext : unit -> bool

    /// Select the previous word in the session.
    abstract MovePrevious : unit -> bool

    /// Dismiss the completion session 
    abstract Dismiss : unit -> unit

    /// Raised when the session is dismissed
    [<CLIEvent>]
    abstract Dismissed: IEvent<System.EventArgs>

/// Factory service for creating IWordCompletionSession instances
type IWordCompletionSessionFactoryService = 

    /// Create a session with the given set of words
    abstract CreateWordCompletionSession : textView : ITextView -> wordSpan : SnapshotSpan -> words : string seq -> isForward : bool -> IWordCompletionSession

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

/// Wraps a set of IUndoTransaction items such that they undo and redo as a single
/// entity.
type ILinkedUndoTransaction =

    /// Complete the linked operation
    abstract Complete : unit -> unit

    inherit System.IDisposable

/// Wraps all of the undo and redo operations
type IUndoRedoOperations = 

    /// StatusUtil instance that is used to report errors
    abstract StatusUtil : IStatusUtil

    /// Close the IUndoRedoOperations and remove any attached event handlers
    abstract Close : unit -> unit

    /// Creates an Undo Transaction
    abstract CreateUndoTransaction : name:string -> IUndoTransaction

    /// Creates a linked undo transaction
    abstract CreateLinkedUndoTransaction : unit -> ILinkedUndoTransaction

    /// Wrap the passed in "action" inside an undo transaction.  This is needed
    /// when making edits such as paste so that the cursor will move properly 
    /// during an undo operation
    abstract EditWithUndoTransaction<'T> : name : string -> action : (unit -> 'T) -> 'T

    /// Redo the last "count" operations
    abstract Redo : count:int -> unit

    /// Undo the last "count" operations
    abstract Undo : count:int -> unit

/// Represents the type of text change operations we recognize in the ITextBuffer.  These
/// items are repeatable
[<RequireQualifiedAccess>]
type TextChange = 
    | Insert of string
    | Delete of int
    | Combination of TextChange * TextChange

    with 

    /// Get the last / most recent change in the TextChange tree
    member x.LastChange = 
        match x with
        | Insert _ -> x
        | Delete _ -> x
        | Combination (_, right) -> right.LastChange

    /// Merge two TextChange values together.  The goal is to produce a the smallest TextChange
    /// value possible
    static member Merge left right =
        match left, right with
        | Insert leftStr, Insert rightStr -> 
            Insert (leftStr + rightStr)
        | Delete leftCount, Delete rightCount -> 
            Delete (leftCount + rightCount)
        | Insert leftStr, Delete rightCount ->
            let diff = leftStr.Length - rightCount
            if diff >= 0 then
                let value = leftStr.Substring(0, diff)
                Insert value
            else
                Delete (-diff)
        | Delete _, Insert _ ->
            // Can't reduce a left delete any further so we just create a Combination value
            Combination (left, right)
        | _ -> 
            Combination (left, right)

    static member Replace str =
        let left = str |> StringUtil.length |> TextChange.Delete
        let right = TextChange.Insert str
        TextChange.Combination (left, right)

[<System.Flags>]
type SearchOptions = 
    | None = 0x0

    /// Consider the "ignorecase" option when doing the search
    | ConsiderIgnoreCase = 0x1

    /// Consider the "smartcase" option when doing the search
    | ConsiderSmartCase = 0x2

/// Information about a search of a pattern
type PatternData = {

    /// The Pattern to search for
    Pattern : string

    /// The direction in which the pattern was searched for
    Path : Path
}
    with 

    /// The default search options when looking at a specific pattern
    static member DefaultSearchOptions = SearchOptions.ConsiderIgnoreCase ||| SearchOptions.ConsiderSmartCase

type SearchData = {

    /// The pattern being searched for in the buffer
    Pattern : string

    Kind : SearchKind;

    Options : SearchOptions
} with

    member x.PatternData = { Pattern = x.Pattern; Path = x.Kind.Path }

    static member OfPatternData (patternData : PatternData) wrap = 
        {
            Pattern = patternData.Pattern
            Kind = SearchKind.OfPathAndWrap patternData.Path wrap
            Options = PatternData.DefaultSearchOptions
        }

/// Result of an individual search
[<RequireQualifiedAccess>]
type SearchResult =

    /// The pattern was found.  The bool at the end of the tuple represents whether not
    /// a wrap occurred while searching for the value
    | Found of SearchData * SnapshotSpan * bool

    /// The pattern was not found.  The bool is true if the word was present in the ITextBuffer
    /// but wasn't found do to the lack of a wrap in the SearchData value
    | NotFound of SearchData * bool

    with

    /// Returns the SearchData which was searched for
    member x.SearchData = 
        match x with 
        | SearchResult.Found (searchData, _, _) -> searchData
        | SearchResult.NotFound (searchData, _) -> searchData

/// Global information about searches within Vim
type ISearchService = 

    /// Find the next occurrence of the pattern in the buffer starting at the 
    /// given SnapshotPoint
    abstract FindNext : SearchData -> SnapshotPoint -> ITextStructureNavigator -> SearchResult

    /// Find the next Nth occurrence of the search data
    abstract FindNextMultiple : SearchData -> SnapshotPoint -> ITextStructureNavigator -> count:int -> SearchResult

    /// Find the next 'count' occurrence of the specified pattern.  Note: The first occurrence won't
    /// match anything at the provided start point.  That will be adjusted appropriately
    abstract FindNextPattern : PatternData -> SnapshotPoint -> ITextStructureNavigator -> count : int -> SearchResult

/// Column information about the caret in relation to this Motion Result
[<RequireQualifiedAccess>]
type CaretColumn = 

    /// No column information was provided
    | None

    /// Caret should be placed in the specified column on the last line in 
    /// the MotionResult
    | InLastLine of int

    /// Caret should be placed at the start of the line after the last line
    /// in the motion
    | AfterLastLine

/// These are the types of motions which must be handled separately
[<RequireQualifiedAccess>]
type MotionResultFlags = 

    /// No special information for this motion
    | None = 0

    /// Any of the word motions
    | AnyWord = 0x1

    /// This was promoted under rule #1 listed in ':help exclusive'
    | ExclusivePromotion = 0x2

    /// This is to cover cases where the last line is blank and an exclusive promotion
    /// under rule #1 occurs.  It's impossible for the caret movement code to tell the 
    /// difference between a blank which should be consider the last line or if the 
    /// line above is last.  This helps differentiate the two
    | ExclusivePromotionPlusOne = 0x4

    /// This motion was promoted under rule #2 to a line wise motion
    | ExclusiveLineWise = 0x8

/// Information about the type of the motion this was.
[<RequireQualifiedAccess>]
type MotionKind =

    | CharacterWiseInclusive

    | CharacterWiseExclusive

    /// In addition to recording the Span certain line wise operations like j and k also
    /// record data about the desired column within the span.  This value may or may not
    /// be a valid point within the line
    | LineWise of CaretColumn

/// Data about a complete motion operation. 
type MotionResult = {

    /// Span of the motion.
    Span : SnapshotSpan

    /// Was the motion forwards towards the end of the buffer
    IsForward : bool 

    /// Kind of the motion
    MotionKind : MotionKind

    /// The flags on the MotionRelult
    MotionResultFlags : MotionResultFlags

} with

    /// The possible column of the MotionResult
    member x.CaretColumn = 
        match x.MotionKind with
        | MotionKind.CharacterWiseExclusive -> CaretColumn.None
        | MotionKind.CharacterWiseInclusive -> CaretColumn.None
        | MotionKind.LineWise column -> column

    /// The Span as an EditSpan value
    member x.EditSpan = EditSpan.Single x.Span

    /// The OperationKind of the MotionResult
    member x.OperationKind = 
        match x.MotionKind with
        | MotionKind.CharacterWiseExclusive -> OperationKind.CharacterWise
        | MotionKind.CharacterWiseInclusive -> OperationKind.CharacterWise
        | MotionKind.LineWise _ -> OperationKind.LineWise

    /// Is this a word motion 
    member x.IsAnyWordMotion = Util.IsFlagSet x.MotionResultFlags MotionResultFlags.AnyWord

    /// Is this an exclusive motion
    member x.IsExclusive =
        match x.MotionKind with
        | MotionKind.CharacterWiseExclusive -> true
        | MotionKind.CharacterWiseInclusive -> false
        | MotionKind.LineWise _ -> false

    /// Is this an inclusive motion
    member x.IsInclusive = not x.IsExclusive

    /// The Span as a SnapshotLineRange value 
    member x.LineRange = SnapshotLineRangeUtil.CreateForSpan x.Span

/// Context on how the motion is being used.  Several motions (]] for example)
/// change behavior based on how they are being used
[<RequireQualifiedAccess>]
type MotionContext =
    | Movement
    | AfterOperator

/// Arguments necessary to building a Motion
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
    | CharSearch of CharSearchKind * Path * char

    /// Implement the 'e' motion.  This goes to the end of the current word.  If we're
    /// not currently on a word it will find the next word and then go to the end of that
    | EndOfWord of WordKind
    
    /// Implement an end of line motion.  Typically in response to the $ key.  Even though
    /// this motion deals with lines, it's still a character wise motion motion. 
    | EndOfLine

    /// Find the first non-blank character as the start of the span.  This is an exclusive
    /// motion so be careful we don't go to far forward.  Providing a count to this motion has
    /// no affect
    | FirstNonBlankOnCurrentLine

    /// Find the first non-blank character on the (count - 1) line below this line
    | FirstNonBlankOnLine

    /// Inner word motion
    | InnerWord of WordKind

    /// Find the last non-blank character on the line.  Count causes it to go "count" lines
    /// down and perform the search
    | LastNonBlankOnLine

    /// Find the next occurrence of the last search.  The bool parameter is true if the search
    /// should be in the opposite direction
    | LastSearch of bool

    /// Handle the lines down to first non-blank motion.  This is one of the motions which 
    /// can accept a count of 0.
    | LineDownToFirstNonBlank

    /// Handle the - motion
    | LineUpToFirstNonBlank

    /// Get the span of "count" lines upward careful not to run off the beginning of the
    /// buffer.  Implementation of the "k" motion
    | LineUp

    /// Get the span of "count" lines downward careful not to run off the end of the
    /// buffer.  Implementation of the "j" motion
    | LineDown

    /// Go to the specified line number or the first line if no line number is provided 
    | LineOrFirstToFirstNonBlank

    /// Go to the specified line number or the last line of no line number is provided
    | LineOrLastToFirstNonBlank

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

    /// Search for the next occurrence of the word under the caret
    | NextWord of Path

    /// Search for the next partial occurrence of the word under the caret
    | NextPartialWord of Path

    /// Count paragraphs backwards
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
    | Search of PatternData

    /// Backward a section in the editor or to a close brace
    | SectionBackwardOrCloseBrace
    
    /// Backward a section in the editor or to an open brace
    | SectionBackwardOrOpenBrace

    /// Forward a section in the editor or to a close brace
    | SectionForwardOrCloseBrace

    /// Forward a section in the editor
    | SectionForward

    /// Count sentences backward 
    | SentenceBackward

    /// Count sentences forward
    | SentenceForward

    /// Implement the b/B motion
    | WordBackward of WordKind

    /// Implement the w/W motion
    | WordForward of WordKind 

/// Interface for running Motion instances against an ITextView
and IMotionUtil =

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

    /// Returns the first KeyInput if present
    member x.FirstKeyInput = 
        match x with 
        | Empty -> None
        | OneKeyInput(ki) -> Some ki
        | TwoKeyInputs(ki,_) -> Some ki
        | ManyKeyInputs(list) -> ListUtil.tryHeadOnly list

    /// Returns the rest of the KeyInput values after the first
    member x.Rest = 
        match x with
        | Empty -> List.empty
        | OneKeyInput _ -> List.empty
        | TwoKeyInputs (_, keyInput2) -> [ keyInput2 ]
        | ManyKeyInputs list -> List.tail list

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
            if ki.Key = VimKey.RawCharacter then ki.Char.ToString()
            elif ki.Key = VimKey.None then "<None>"
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

    let Combine (left : KeyInputSet) (right : KeyInputSet) =
        let all = left.KeyInputs @ right.KeyInputs
        OfList all

/// Modes for a key remapping
[<RequireQualifiedAccess>]
[<DebuggerDisplay("{ToString(),nq}")>]
type KeyRemapMode =
    | Normal 
    | Visual 
    | Select 
    | OperatorPending 
    | Insert 
    | Command 
    | Language 

    with 

    override x.ToString() =
        match x with 
        | Normal -> "Normal"
        | Visual -> "Visual"
        | Select -> "Select"
        | OperatorPending -> "OperatorPending"
        | Insert -> "Insert"
        | Command -> "Command"
        | Language -> "Language"

[<RequireQualifiedAccess>]
type KeyMappingResult =

    /// No mapping exists 
    | NoMapping 

    /// Mapped to the specified KeyInputSet
    | Mapped of KeyInputSet 

    /// The mapping encountered a recursive element that had to be broken 
    | Recursive

    /// More input is needed to resolve this mapping
    | NeedsMoreInput

/// Flags for the substitute command
[<System.Flags>]
type SubstituteFlags = 
    | None = 0

    /// Replace all occurrences on the line
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

/// Represents the span for a Visual Character mode selection.  If it weren't for the
/// complications of tracking a visual character selection across edits to the buffer
/// there would really be no need for this and we could instead just represent it as 
/// a SnapshotSpan
[<StructuralEquality>]
[<NoComparison>]
[<Struct>]
type CharacterSpan
    (
        _start : SnapshotPoint,
        _lineCount : int,
        _lastLineLength : int
    ) =

    member x.Snapshot = _start.Snapshot

    member x.StartLine = SnapshotPointUtil.GetContainingLine x.Start

    member x.Start =  _start

    member x.LineCount =_lineCount

    member x.LastLineLength = _lastLineLength

    /// Get the EndPoint of the Character Span
    member x.End =
        let snapshot = x.Start.Snapshot
        let endPoint = 
            if x.LineCount = 1 then
                x.Start
                |> SnapshotPointUtil.TryAdd x.LastLineLength 
                |> OptionUtil.getOrDefault x.StartLine.End
            else 
                SnapshotUtil.GetLineOrLast snapshot (x.StartLine.LineNumber + x.LineCount - 1)
                |> SnapshotLineUtil.GetOffsetOrEnd x.LastLineLength

        // Handle the case where StartPoint is in the line break.  Need to ensure
        // StartPoint.Position < EndPoint.Position.
        SnapshotPointUtil.OrderAscending x.Start endPoint |> snd

    member x.Span = SnapshotSpan(x.Start, x.End)

    member x.Length = x.Span.Length

    static member op_Equality(this,other) = System.Collections.Generic.EqualityComparer<CharacterSpan>.Default.Equals(this,other)
    static member op_Inequality(this,other) = not (System.Collections.Generic.EqualityComparer<CharacterSpan>.Default.Equals(this,other))

    static member CreateForSpan (span : SnapshotSpan) = 
        let lineRange = SnapshotLineRangeUtil.CreateForSpan span
        let lastLineLength = 
            if lineRange.Count = 1 then
                span.Length
            else
                let diff = span.End.Position - lineRange.EndLine.Start.Position
                max 0 diff
        CharacterSpan(span.Start, lineRange.Count, lastLineLength)

/// Represents the span for a Visual Block mode selection
[<StructuralEquality>]
[<NoComparison>]
[<Struct>]
type BlockSpan
    (
        _start : SnapshotPoint,
        _width : int,
        _height : int
    ) = 

    member x.Start = _start

    /// In what column does this block span begin
    member x.Column = SnapshotPointUtil.GetColumn x.Start

    member x.Width = _width

    member x.Height = _height

    /// Get the EndPoint (exclusive) of the BlockSpan
    member x.End = 
        let line = 
            let lineNumber = SnapshotPointUtil.GetLineNumber x.Start
            SnapshotUtil.GetLineOrLast x.Snasphot (lineNumber + (_height - 1))
        let offset = x.Column + _width
        if offset >= line.Length then
            line.End
        else
            line.Start.Add offset

    member x.Snasphot = x.Start.Snapshot

    member x.TextBuffer =  x.Start.Snapshot.TextBuffer

    /// Get the NonEmptyCollection<SnapshotSpan> for the given block information
    member x.BlockSpans : NonEmptyCollection<SnapshotSpan> =
        let snapshot = SnapshotPointUtil.GetSnapshot x.Start
        let lineNumber = SnapshotPointUtil.GetLineNumber x.Start
        let list = System.Collections.Generic.List<SnapshotSpan>()
        for i = lineNumber to ((_height - 1)+ lineNumber) do
            match SnapshotUtil.TryGetLine snapshot i with
            | None -> ()
            | Some line -> list.Add (SnapshotLineUtil.GetSpanInLine line x.Column _width)

        list
        |> NonEmptyCollectionUtil.OfSeq 
        |> Option.get

    override x.ToString() =
        sprintf "Point: %s Width: %d Height: %d" (x.Start.ToString()) _width _height

    static member op_Equality(this,other) = System.Collections.Generic.EqualityComparer<BlockSpan>.Default.Equals(this,other)
    static member op_Inequality(this,other) = not (System.Collections.Generic.EqualityComparer<BlockSpan>.Default.Equals(this,other))

[<RequireQualifiedAccess>]
type BlockCaretLocation =
    | TopLeft
    | TopRight
    | BottomLeft
    | BottomRight

/// Represents the visual selection for any of the visual modes
[<RequireQualifiedAccess>]
[<StructuralEquality>]
[<NoComparison>]
type VisualSpan =

    /// A characterwise span
    | Character of CharacterSpan

    /// A linewise span
    | Line of SnapshotLineRange

    /// A block span.  The first int in the number of lines and the second one is the 
    /// width of the selection
    | Block of BlockSpan

    with

    /// Return the Spans which make up this VisualSpan instance
    member x.Spans = 
        match x with 
        | VisualSpan.Character characterSpan -> [characterSpan.Span] |> Seq.ofList
        | VisualSpan.Line range -> [range.ExtentIncludingLineBreak] |> Seq.ofList
        | VisualSpan.Block blockSpan -> blockSpan.BlockSpans :> SnapshotSpan seq

    /// Returns the EditSpan for this VisualSpan
    member x.EditSpan = 
        match x with
        | VisualSpan.Character characterSpan -> EditSpan.Single characterSpan.Span
        | VisualSpan.Line range -> EditSpan.Single range.ExtentIncludingLineBreak
        | VisualSpan.Block blockSpan -> EditSpan.Block blockSpan.BlockSpans

    /// Returns the SnapshotLineRange for the VisualSpan.  For Character this will
    /// just expand out the Span.  For Line this is an identity.  For Block it will
    /// return the overarching span
    member x.LineRange = 
        match x with
        | VisualSpan.Character characterSpan -> SnapshotLineRangeUtil.CreateForSpan characterSpan.Span
        | VisualSpan.Line range -> range
        | VisualSpan.Block _ -> x.EditSpan.OverarchingSpan |> SnapshotLineRangeUtil.CreateForSpan

    /// Returns the start point of the Visual Span.  This can be None in the case
    /// of an empty Block selection.
    member x.Start =
        match x with
        | Character characterSpan -> characterSpan.Start
        | Line range ->  range.Start
        | Block blockSpan -> blockSpan.Start

    /// What type of OperationKind does this VisualSpan represent
    member x.OperationKind =
        match x with
        | VisualSpan.Character _ -> OperationKind.CharacterWise
        | VisualSpan.Line _ -> OperationKind.LineWise
        | VisualSpan.Block _ -> OperationKind.CharacterWise

    /// What type of ModeKind does this VisualSpan represent
    member x.ModeKind =
        match x with
        | VisualSpan.Character _ -> ModeKind.VisualCharacter
        | VisualSpan.Line _ -> ModeKind.VisualLine
        | VisualSpan.Block _ -> ModeKind.VisualBlock

    static member Create textView visualKind =
        let visualSelection : VisualSelection = VisualSelection.CreateForSelection textView visualKind
        visualSelection.VisualSpan

/// Represents both the VisualSpan and selection information for Visual Mode
and [<RequireQualifiedAccess>] [<StructuralEquality>] [<NoComparison>] VisualSelection =

    /// The bool represents whether or not the caret is at the start of the SnapshotSpan
    | Character of CharacterSpan * bool

    /// The bool represents whether or not the caret is at the start of the line range
    /// and the int is which column in the range the caret should be placed in
    | Line of SnapshotLineRange * bool * int 

    /// Just keep the BlockSpan and the caret information for the block
    | Block of BlockSpan * BlockCaretLocation

    with

    /// Get the corresponding Visual Span
    member x.VisualSpan = 
        match x with
        | Character (characterSpan, _) -> VisualSpan.Character characterSpan
        | Line (snapshotLineRange, _, _) -> VisualSpan.Line snapshotLineRange
        | Block (blockSpan, _) -> VisualSpan.Block blockSpan

    /// Get the ModeKind for the VisualSelection
    member x.ModeKind =
        x.VisualSpan.ModeKind

    /// Get the EditSpan for the given VisualSpan
    member x.EditSpan =
        x.VisualSpan.EditSpan

    member x.IsCharacterForward =
        match x with
        | Character (_, isForward) -> isForward
        | _ -> false

    member x.IsLineForward = 
        match x with
        | Line (_, isForward, _) -> isForward
        | _ -> false

    /// Gets the SnapshotPoint for the caret as it should appear in the given VisualSelection
    member x.CaretPoint = 
        match x with
        | Character (characterSpan, isForward) ->
            // The caret is either positioned at the start or the end of the selected
            // SnapshotSpan
            if isForward && characterSpan.Length > 0 then
                SnapshotPointUtil.SubtractOne characterSpan.End
            else
                characterSpan.Start

        | Line (snapshotLineRange, isForward, column) ->
            // The caret is either positioned at the start or the end of the selected range
            // and can be on any column in either
            let line = 
                if isForward then
                    snapshotLineRange.EndLine
                else
                    snapshotLineRange.StartLine

            if column <= line.LengthIncludingLineBreak then
                SnapshotPointUtil.Add column line.Start
            else
                line.End

        | Block (blockSpan, blockCaretLocation) ->

            let getSpanEnd (span : SnapshotSpan) =
                if span.Length = 0 then
                    span.End
                else
                    span.End.Subtract 1

            match blockCaretLocation with
            | BlockCaretLocation.TopLeft -> blockSpan.Start
            | BlockCaretLocation.TopRight -> getSpanEnd blockSpan.BlockSpans.Head
            | BlockCaretLocation.BottomLeft -> blockSpan.BlockSpans |> SeqUtil.last |> SnapshotSpanUtil.GetStartPoint
            | BlockCaretLocation.BottomRight -> blockSpan.BlockSpans |> SeqUtil.last |> getSpanEnd

    static member CreateForSelection textView visualKind =
        let caretPoint = TextViewUtil.GetCaretPoint textView
        match visualKind with
        | VisualKind.Character -> 

            // This is just represented by looking at the Stream SelectionSpan and checking the position
            // of the caret
            let span = textView.Selection.StreamSelectionSpan.SnapshotSpan
            let isBackward = caretPoint = span.Start
            VisualSelection.Character (CharacterSpan.CreateForSpan span, not isBackward)
        | VisualKind.Line-> 

            let column = SnapshotPointUtil.GetColumn caretPoint
            let snapshotLineRange = textView.Selection.StreamSelectionSpan.SnapshotSpan |> SnapshotLineRangeUtil.CreateForSpan
            let isForward = 
                if snapshotLineRange.StartLine.ExtentIncludingLineBreak.Contains caretPoint then
                    false
                else
                    true
            VisualSelection.Line (snapshotLineRange, isForward, column)
        | VisualKind.Block -> 

            let caretPoint = TextViewUtil.GetCaretPoint textView
            let spanCollection = textView.Selection.SelectedSpans
            if spanCollection.Count = 0 then
                // Just like we would treat Character as a single empty SnapshotSpan we do the same here
                // for block selection
                let blockSpan = BlockSpan(caretPoint, 0, 1)
                VisualSelection.Block (blockSpan, BlockCaretLocation.TopLeft)
            else
                let firstSpan = spanCollection.[0]
                let lastSpan = spanCollection |> SeqUtil.last
                let blockSpan = BlockSpan(firstSpan.Start, firstSpan.Length, spanCollection.Count)
                let blockCaretLocation = 
                    
                    if firstSpan.Start = caretPoint then
                        BlockCaretLocation.TopLeft
                    elif firstSpan.Contains(caretPoint) then
                        BlockCaretLocation.TopRight
                    elif lastSpan.Start = caretPoint then
                        BlockCaretLocation.BottomLeft
                    elif lastSpan.Contains(caretPoint) then
                        BlockCaretLocation.BottomRight
                    else
                        BlockCaretLocation.TopLeft

                VisualSelection.Block (blockSpan, blockCaretLocation)

    /// Create for the given VisualSpan.  Assumes this was a forward created VisualSpan
    static member CreateForVisualSpan visualSpan = 
        match visualSpan with
        | VisualSpan.Character span -> 
            VisualSelection.Character (span, true)
        | VisualSpan.Line lineRange ->
            let column = SnapshotPointUtil.GetColumn lineRange.EndLine.End
            VisualSelection.Line (lineRange, true, column)
        | VisualSpan.Block blockSpan ->
            VisualSelection.Block (blockSpan, BlockCaretLocation.BottomRight)

[<RequireQualifiedAccess>]
type ModeArgument =
    | None

    /// Used for transitions from Visual Mode directly to Command mode
    | FromVisual 

    /// The initial span which should be selected and the SnapshotPoint where the 
    /// caret should be placed within the VisualSpan
    | InitialVisualSelection of VisualSelection

    /// Begins insert mode with a specified count.  This means the text inserted should
    /// be repeated a total of 'count - 1' times when insert mode exits
    | InsertWithCount of int

    /// Begins insert mode with a specified count.  This means the text inserted should
    /// be repeated a total of 'count - 1' times when insert mode exits.  Each extra time
    /// should be on a new line
    | InsertWithCountAndNewLine of int

    /// Begins insert mode with an existing UndoTransaction.  This is used to link 
    /// change commands with text changes.  For example C, c, etc ...
    | InsertWithTransaction of ILinkedUndoTransaction

    /// When the given mode is to execute a single command then return to 
    /// the previous mode.  The provided mode kind is the value which needs
    /// to be switched to upon completion of the command
    | OneTimeCommand of ModeKind

    /// Passing the substitute to confirm to Confirm mode.  The SnapshotSpan is the first
    /// match to process and the range is the full range to consider for a replace
    | Substitute of SnapshotSpan * SnapshotLineRange * SubstituteData


type ModeSwitch =
    | NoSwitch
    | SwitchMode of ModeKind
    | SwitchModeWithArgument of ModeKind * ModeArgument
    | SwitchPreviousMode 

// TODO: Should be succeeded or something other than Completed.  Error also completed just not
// well
[<RequireQualifiedAccess>]
type CommandResult =   

    /// The command completed and requested a switch to the provided Mode which 
    /// may just be a no-op
    | Completed  of ModeSwitch

    /// An error was encountered and the command was unable to run.  If this is encountered
    /// during a macro run it will cause the macro to stop executing
    | Error

[<RequireQualifiedAccess>]
type RunResult = 
    | Completed
    | SubstituteConfirm of SnapshotSpan * SnapshotLineRange * SubstituteData

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
    | LinkedWithNextCommand = 0x10

    /// For the purposes of change repeating the command is linked with the previous
    /// text change if it exists
    | LinkedWithPreviousCommand = 0x20

    /// For Visual Mode commands which should reset the cursor to the original point
    /// after completing
    | ResetCaret = 0x40

    /// Vim allows for special handling of the 'd' command in normal mode such that it can
    /// have the pattern 'd#d'.  This flag is used to tag the 'd' command to allow such
    /// a pattern
    | Delete = 0x80

    /// Vim allows for special handling of the 'y' command in normal mode such that it can
    /// have the pattern 'y#y'.  This flag is used to tag the 'd' command to allow such
    /// a pattern
    | Yank = 0x100

    /// Represents an insert edit action which can be linked with other insert edit actions
    | InsertEdit = 0x200

/// Data about the run of a given MotionResult
type MotionData = {

    /// The associated Motion value
    Motion : Motion

    /// The argument which should be supplied to the given Motion
    MotionArgument : MotionArgument
}

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
type PingData (_func : CommandData -> CommandResult) = 

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

    /// Add 'count' to the word close to the caret
    | AddToWord

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

    /// Close all folds under the caret
    | CloseAllFoldsUnderCaret

    /// Close 'count' folds under the caret
    | CloseFoldUnderCaret

    /// Delete all of the folds that are in the ITextBuffer
    | DeleteAllFoldsInBuffer

    /// Delete the character at the current cursor position.  Implements the "x" command
    | DeleteCharacterAtCaret

    /// Delete the character before the cursor. Implements the "X" command
    | DeleteCharacterBeforeCaret

    /// Delete the fold under the caret
    | DeleteFoldUnderCaret

    /// Delete all folds under the caret
    | DeleteAllFoldsUnderCaret

    /// Delete lines from the buffer: dd
    | DeleteLines

    /// Delete the specified motion of text
    | DeleteMotion of MotionData

    /// Delete till the end of the line and 'count - 1' more lines down
    | DeleteTillEndOfLine

    /// Fold 'count' lines in the ITextBuffer
    | FoldLines

    /// Create a fold over the specified motion 
    | FoldMotion of MotionData

    /// Format the specified lines
    | FormatLines

    /// Format the specified motion
    | FormatMotion of MotionData

    /// Go to the definition of hte word under the caret.
    | GoToDefinition

    /// GoTo the file under the cursor.  The bool represents whether or not this should occur in
    /// a different window
    | GoToFileUnderCaret of bool

    /// Go to the global declaration of the word under the caret
    | GoToGlobalDeclaration

    /// Go to the local declaration of the word under the caret
    | GoToLocalDeclaration

    /// Go to the next tab in the specified direction
    | GoToNextTab of Path

    /// GoTo the ITextView in the specified direction
    | GoToView of Direction

    /// Switch to insert after the caret position
    | InsertAfterCaret

    /// Switch to insert mode
    | InsertBeforeCaret

    /// Switch to insert mode at the end of the line
    | InsertAtEndOfLine

    /// Insert text at the first non-blank line in the current line
    | InsertAtFirstNonBlank

    /// Insert text at the start of a line (column 0)
    | InsertAtStartOfLine

    /// Insert a line above the cursor and begin insert mode
    | InsertLineAbove

    /// Insert a line below the cursor and begin insert mode
    | InsertLineBelow

    /// Join the specified lines
    | JoinLines of JoinKind

    /// Jump to the specified mark 
    | JumpToMark of char

    /// Jump to the next older item in the tag list
    | JumpToOlderPosition

    /// Jump to the next new item in the tag list
    | JumpToNewerPosition

    /// Move the caret to the result of the given Motion.
    | MoveCaretToMotion of Motion

    /// Undo count operations in the ITextBuffer
    | Undo

    /// Open all of the folds under the caret
    | OpenAllFoldsUnderCaret

    /// Open a fold under the caret
    | OpenFoldUnderCaret

    /// Not actually a Vim Command.  This is a simple ping command which makes 
    /// testing items like complex repeats significantly easier
    | Ping of PingData

    /// Put the contents of the register into the buffer after the cursor.  The bool is 
    /// whether or not the caret should be placed after the inserted text
    | PutAfterCaret of bool

    /// Put the contents of the register into the buffer after the cursor and respecting 
    /// the indent of the current line
    | PutAfterCaretWithIndent

    /// Put the contents of the register into the buffer before the cursor.  The bool is 
    /// whether or not the caret should be placed after the inserted text
    | PutBeforeCaret of bool

    /// Put the contents of the register into the buffer before the cursor and respecting 
    /// the indent of the current line
    | PutBeforeCaretWithIndent

    /// Start the recording of a macro to the specified Register
    | RecordMacroStart of char

    /// Stop the recording of a macro to the specified Register
    | RecordMacroStop

    /// Redo count operations in the ITextBuffer
    | Redo

    /// Repeat the last command
    | RepeatLastCommand

    /// Repeat the last substitute command.  The bool value is for whether or not the flags
    /// from the last substitute should be reused as well
    | RepeatLastSubstitute of bool

    /// Replace the text starting at the text by starting insert mode
    | ReplaceAtCaret

    /// Replace the char under the cursor with the given char
    | ReplaceChar of KeyInput

    /// Run the macro contained in the register specified by the char value
    | RunMacro of char

    /// Set the specified mark to the current value of the caret
    | SetMarkToCaret of char

    /// Scroll the screen in the specified direction.  The bool is whether or not to use
    /// the 'scroll' option or 'count'
    | ScrollLines of ScrollDirection * bool

    /// Move the display a single page in the specified direction
    | ScrollPages of ScrollDirection

    /// Scroll the current line to the top of the ITextView.  The bool is whether or not
    /// to leave the caret in the same column
    | ScrollCaretLineToTop of bool

    /// Scroll the caret line to the middle of the ITextView.  The bool is whether or not
    /// to leave the caret in the same column
    | ScrollCaretLineToMiddle of bool

    /// Scroll the caret line to the bottom of the ITextView.  The bool is whether or not
    /// to leave the caret in the same column
    | ScrollCaretLineToBottom of bool

    /// Shift 'count' lines from the cursor left
    | ShiftLinesLeft

    /// Shift 'count' lines from the cursor right
    | ShiftLinesRight

    /// Shift 'motion' lines from the cursor left
    | ShiftMotionLinesLeft of MotionData

    /// Shift 'motion' lines from the cursor right
    | ShiftMotionLinesRight of MotionData

    /// Split the view horizontally
    | SplitViewHorizontally

    /// Split the view vertically
    | SplitViewVertically

    /// Substitute the character at the cursor
    | SubstituteCharacterAtCaret

    /// Subtract 'count' from the word at the caret
    | SubtractFromWord

    /// Switch modes with the specified information
    | SwitchMode of ModeKind * ModeArgument

    /// Switch to the previous Visual Mode selection
    | SwitchPreviousVisualMode

    /// Write out the ITextBuffer and quit
    | WriteBufferAndQuit

    /// Yank the given motion into a register
    | Yank of MotionData

    /// Yank the specified number of lines
    | YankLines

/// Visual mode commands which can be executed by the user 
[<RequireQualifiedAccess>]
type VisualCommand = 

    /// Change the case of the selected text in the specified manner
    | ChangeCase of ChangeCharacterKind

    /// Delete the selection and begin insert mode.  Implements the 'c' and 's' commands
    | ChangeSelection

    /// Delete the selected lines and begin insert mode ('S' and 'C' commands).  The bool parameter
    /// is whether or not to treat block selection as a special case
    | ChangeLineSelection of bool

    /// Close a fold in the selection
    | CloseFoldInSelection

    /// Close all folds in the selection
    | CloseAllFoldsInSelection

    /// Delete a fold in the selection
    | DeleteFoldInSelection

    /// Delete all folds in the selection
    | DeleteAllFoldsInSelection

    /// Delete the selected lines
    | DeleteLineSelection

    /// Delete the selected text and put it into a register
    | DeleteSelection

    /// Fold the current selected lines
    | FoldSelection

    /// Format the selected text
    | FormatLines

    /// Join the selected lines
    | JoinSelection of JoinKind

    /// Open all folds in the selection
    | OpenAllFoldsInSelection

    /// Open one fold in the selection
    | OpenFoldInSelection

    /// Put the contents af the register after the selection.  The bool is for whether or not the
    // caret should be placed after the inserted text
    | PutOverSelection of bool

    /// Replace the visual span with the provided character
    | ReplaceSelection of KeyInput

    /// Shift the selected lines left
    | ShiftLinesLeft

    /// Shift the selected lines to the right
    | ShiftLinesRight

    /// Yank the lines which are specified by the selection
    | YankLineSelection

    /// Yank the selection into the specified register
    | YankSelection

/// Insert mode commands that can be executed by the user
[<RequireQualifiedAccess>]
[<StructuralEquality>]
[<NoComparison>]
type InsertCommand  =

    /// Back space at the current caret position
    | Back

    /// This is an insert command which is a combination of other insert commands
    | Combined of InsertCommand * InsertCommand

    /// Delete the character under the caret
    | Delete

    /// Delete all indentation on the current line
    | DeleteAllIndent

    /// Insert a new line into the ITextBuffer
    | InsertNewLine

    /// Insert a tab into the ITextBuffer
    | InsertTab

    /// Move the caret in the given direction
    | MoveCaret of Direction

    /// Shift the current line one indent width to the left
    | ShiftLineLeft 

    /// Shift the current line one indent width to the right
    | ShiftLineRight

    /// Text Change 
    | TextChange of TextChange

/// Commands which can be executed by the user
[<RequireQualifiedAccess>]
[<StructuralEquality>]
[<NoComparison>]
type Command =

    /// A Normal Mode Command
    | NormalCommand of NormalCommand * CommandData

    /// A Visual Mode Command
    | VisualCommand of VisualCommand * CommandData * VisualSpan

    /// An Insert / Replace Mode Command
    | InsertCommand of InsertCommand

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

    /// Used to compose to BindResult<'T> functions together by forwarding from
    /// one to the other once the value is completed
    member x.Map mapFunc =
        match x with
        | Complete value -> mapFunc value
        | NeedMoreInput bindData -> NeedMoreInput (bindData.Map mapFunc)
        | Error -> Error
        | Cancelled -> Cancelled

    /// Used to convert a BindResult<'T>.Completed to BindResult<'U>.Completed through a conversion
    /// function
    member x.Convert convertFunc = 
        x.Map (fun value -> convertFunc value |> BindResult.Complete)

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
    /// to translate a BindData<'T>.Completed -> BindData<'U>.Completed
    member x.Convert convertFunc = 
        x.Map (fun value -> convertFunc value |> BindResult.Complete)

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

/// Several types of BindData<'T> need to take an action when a binding begins against
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

    /// Many bindings are simply to get a single char.  Centralize that logic 
    /// here so it doesn't need to be repeated
    static member CreateForSingleChar keyRemapModeOpt completeFunc = 
        let data = BindData<_>.CreateForSingle keyRemapModeOpt (fun keyInput -> completeFunc keyInput.Char)
        BindDataStorage<_>.Simple data

/// Representation of binding of Command's to KeyInputSet values and flags which correspond
/// to the execution of the command
[<DebuggerDisplay("{ToString(),nq}")>]
[<RequireQualifiedAccess>]
type CommandBinding = 

    /// KeyInputSet bound to a particular NormalCommand instance
    | NormalBinding of KeyInputSet * CommandFlags * NormalCommand

    /// KeyInputSet bound to a complex NormalCommand instance
    | ComplexNormalBinding of KeyInputSet * CommandFlags * BindDataStorage<NormalCommand>

    /// KeyInputSet bound to a particular NormalCommand instance which takes a Motion Argument
    | MotionBinding of KeyInputSet * CommandFlags * (MotionData -> NormalCommand)

    /// KeyInputSet bound to a particular VisualCommand instance
    | VisualBinding of KeyInputSet * CommandFlags * VisualCommand

    /// KeyInputSet bound to an insert mode command
    | InsertBinding of KeyInputSet * CommandFlags * InsertCommand

    /// KeyInputSet bound to a complex VisualCommand instance
    | ComplexVisualBinding of KeyInputSet * CommandFlags * BindDataStorage<VisualCommand>

    with 

    /// The raw command inputs
    member x.KeyInputSet = 
        match x with
        | NormalBinding (value, _, _) -> value
        | MotionBinding (value, _, _) -> value
        | VisualBinding (value, _, _) -> value
        | InsertBinding (value, _, _) -> value
        | ComplexNormalBinding (value, _, _) -> value
        | ComplexVisualBinding (value, _, _) -> value

    /// The kind of the Command
    member x.CommandFlags =
        match x with
        | NormalBinding (_, value, _) -> value
        | MotionBinding (_, value, _) -> value
        | VisualBinding (_, value, _) -> value
        | InsertBinding (_, value, _) -> value
        | ComplexNormalBinding (_, value, _) -> value
        | ComplexVisualBinding (_, value, _) -> value

    /// Is the Repeatable flag set
    member x.IsRepeatable = Util.IsFlagSet x.CommandFlags CommandFlags.Repeatable

    /// Is the HandlesEscape flag set
    member x.HandlesEscape = Util.IsFlagSet x.CommandFlags CommandFlags.HandlesEscape

    /// Is the Movement flag set
    member x.IsMovement = Util.IsFlagSet x.CommandFlags CommandFlags.Movement

    /// Is the Special flag set
    member x.IsSpecial = Util.IsFlagSet x.CommandFlags CommandFlags.Special

    override x.ToString() = System.String.Format("{0} -> {1}", x.KeyInputSet, x.CommandFlags)

/// Used to execute commands
and ICommandUtil = 

    /// Run a normal command
    abstract RunNormalCommand : NormalCommand -> CommandData -> CommandResult

    /// Run a visual command
    abstract RunVisualCommand : VisualCommand -> CommandData -> VisualSpan -> CommandResult

    /// Run a insert command
    abstract RunInsertCommand : InsertCommand -> CommandResult

    /// Run a command
    abstract RunCommand : Command -> CommandResult

type internal IInsertUtil = 

    /// Run a insert command
    abstract RunInsertCommand : InsertCommand -> CommandResult

    /// Repeat the given edit series. 
    abstract RepeatEdit : InsertCommand -> addNewLines : bool -> count : int -> unit

/// Contains the stored information about a Visual Span.  This instance *will* be 
/// stored for long periods of time and used to repeat a Command instance across
/// multiple IVimBuffer instances so it must be buffer agnostic
[<RequireQualifiedAccess>]
type StoredVisualSpan = 

    /// Storing a character wise span.  Need to know the line count and the offset 
    /// in the last line for the end
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
        | VisualSpan.Character characterSpan ->
            StoredVisualSpan.Character (characterSpan.LineCount, characterSpan.LastLineLength)
        | VisualSpan.Line range ->
            StoredVisualSpan.Line range.Count
        | VisualSpan.Block blockSpan -> 
            StoredVisualSpan.Block (blockSpan.Width, blockSpan.Height)

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

    /// The stored information about a InsertCommand
    | InsertCommand of InsertCommand * CommandFlags

    /// A Linked Command links together 2 other StoredCommand objects so they
    /// can be repeated together.
    | LinkedCommand of StoredCommand * StoredCommand

    with

    /// The CommandFlags associated with this StoredCommand
    member x.CommandFlags =
        match x with 
        | NormalCommand (_, _, flags) -> flags
        | VisualCommand (_, _, _, flags) -> flags
        | InsertCommand (_, flags) -> flags
        | LinkedCommand (_, rightCommand) -> rightCommand.CommandFlags

    /// Returns the last command.  For most StoredCommand values this is just an identity 
    /// function but for LinkedCommand values it returns the right most
    member x.LastCommand =
        match x with
        | NormalCommand _ -> x
        | VisualCommand _ -> x
        | InsertCommand _ -> x
        | LinkedCommand (_, right) -> right.LastCommand

    /// Create a StoredCommand instance from the given Command value
    static member OfCommand command (commandBinding : CommandBinding) = 
        match command with 
        | Command.NormalCommand (command, data) -> 
            StoredCommand.NormalCommand (command, data, commandBinding.CommandFlags)
        | Command.VisualCommand (command, data, visualSpan) ->
            let storedVisualSpan = StoredVisualSpan.OfVisualSpan visualSpan
            StoredCommand.VisualCommand (command, data, storedVisualSpan, commandBinding.CommandFlags)
        | Command.InsertCommand command ->
            StoredCommand.InsertCommand (command, commandBinding.CommandFlags)

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

}

/// Responsible for binding key input to a Motion and MotionArgument tuple.  Does
/// not actually run the motions
type IMotionCapture =

    /// Associated ITextView
    abstract TextView : ITextView
    
    /// Set of MotionBinding values supported
    abstract MotionBindings : seq<MotionBinding>

    /// Get the motion and count starting with the given KeyInput
    abstract GetMotionAndCount : KeyInput -> BindResult<Motion * int option>

    /// Get the motion with the provided KeyInput
    abstract GetMotion : KeyInput -> BindResult<Motion>

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

    /// Current value in the jump list.  Will be none when there are no values in the 
    /// jump list
    abstract Current : SnapshotPoint option

    /// Current index into the jump list
    abstract CurrentIndex : int option

    /// Get all of the jumps in the jump list.  Returns in order of most recent to oldest
    abstract Jumps : VirtualSnapshotPoint list

    /// Move to the previous point in the jump list
    abstract MoveOlder : int -> bool

    /// Move to the next point in the jump list
    abstract MoveNewer : int -> bool

    /// Add a given SnapshotPoint to the jump list
    abstract Add : SnapshotPoint -> unit

type IIncrementalSearch = 

    /// True when a search is occurring
    abstract InSearch : bool

    /// When in the middle of a search this will return the SearchData for 
    /// the search
    abstract CurrentSearch : SearchData option

    /// The ITextStructureNavigator used for finding 'word' values in the ITextBuffer
    abstract WordNavigator : ITextStructureNavigator

    /// Begin an incremental search in the ITextBuffer
    abstract Begin : Path -> BindData<SearchResult>

    [<CLIEvent>]
    abstract CurrentSearchUpdated : IEvent<SearchResult>

    [<CLIEvent>]
    abstract CurrentSearchCompleted : IEvent<SearchResult>

    [<CLIEvent>]
    abstract CurrentSearchCancelled : IEvent<SearchData>

/// Used to record macros in a Vim 
type IMacroRecorder =

    /// Is a macro currently recording
    abstract IsRecording : bool

    /// Start recording a macro into the specified Register.  Will fail if the recorder
    /// is already recording
    abstract StartRecording : Register -> isAppend : bool -> unit

    /// Stop recording a macro.  Will fail if it's not actually recording
    abstract StopRecording : unit -> unit

    /// Raised when a macro recording is started.  Passes the Register where the recording
    /// will take place
    [<CLIEvent>]
    abstract RecordingStarted : IEvent<unit>

    /// Raised when a macro recording is completed.
    [<CLIEvent>]
    abstract RecordingStopped : IEvent<unit>

[<RequireQualifiedAccess>]
type ProcessResult = 

    /// The input was processed and provided the given ModeSwitch
    | Handled of ModeSwitch

    /// The operation did not handle the input
    | NotHandled

    /// The input was processed and resulted in an error
    | Error

    /// Is this any type of mode switch
    member x.IsAnySwitch =
        match x with
        | Handled modeSwitch ->
            match modeSwitch with
            | ModeSwitch.NoSwitch -> false
            | ModeSwitch.SwitchMode _ -> true
            | ModeSwitch.SwitchModeWithArgument _ -> true
            | ModeSwitch.SwitchPreviousMode -> true
        | NotHandled -> 
            false
        | Error -> 
            false

    /// Did this actually handle the KeyInput
    member x.IsAnyHandled = 
        match x with
        | Handled _ -> true
        | Error -> true
        | NotHandled -> false

    static member OfModeKind kind = 
        let switch = ModeSwitch.SwitchMode kind
        Handled switch

    static member OfCommandResult commandResult = 
        match commandResult with
        | CommandResult.Completed modeSwitch -> Handled modeSwitch
        | CommandResult.Error -> Error

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
    let HistoryName = "history"
    let IgnoreCaseName = "ignorecase"
    let IncrementalSearchName = "incsearch"
    let MagicName = "magic"
    let ParagraphsName = "paragraphs"
    let ScrollOffsetName = "scrolloff"
    let SectionsName = "sections"
    let SelectionName = "selection"
    let ShiftWidthName = "shiftwidth"
    let SmartCaseName = "smartcase"
    let StartOfLineName = "startofline"
    let TildeOpName = "tildeop"
    let UseEditorIndentName = "vsvim_useeditorindent"
    let UseEditorSettingsName = "vsvim_useeditorsettings"
    let VisualBellName = "visualbell"
    let VirtualEditName = "virtualedit"
    let VimRcName = "vimrc"
    let VimRcPathsName = "vimrcpaths"
    let WrapScanName = "wrapscan"

module LocalSettingNames =

    let AutoIndentName = "autoindent"
    let ExpandTabName = "expandtab"
    let NumberName = "number"
    let NumberFormatsName = "nrformats"
    let TabStopName = "tabstop"
    let QuoteEscapeName = "quoteescape"

module WindowSettingNames =

    let CursorLineName = "cursorline"
    let ScrollName = "scroll"

/// Types of number formats supported by CTRL-A CTRL-A
[<RequireQualifiedAccess>]
type NumberFormat =
    | Alpha
    | Decimal
    | Hex
    | Octal

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

    /// Opacity of the caret.  This must be an integer between values 0 and 100 which
    /// will be converted into a double for the opacity of the caret
    abstract CaretOpacity : int with get, set

    /// Whether or not to highlight previous search patterns matching cases
    abstract HighlightSearch : bool with get,set

    /// The number of items to keep in the history lists
    abstract History : int with get, set

    /// Whether or not the magic option is set
    abstract Magic : bool with get,set

    /// Whether or not we should be ignoring case in the ITextBuffer
    abstract IgnoreCase : bool with get, set

    /// Whether or not incremental searches should be highlighted and focused 
    /// in the ITextBuffer
    abstract IncrementalSearch : bool with get, set

    /// Is the 'onemore' option inside of VirtualEdit set
    abstract IsVirtualEditOneMore : bool with get

    /// Is the Selection setting set to a value which calls for inclusive 
    /// selection.  This does not directly track if Setting = "inclusive" 
    /// although that would cause this value to be true
    abstract IsSelectionInclusive : bool with get

    /// Is the Selection setting set to a value which permits the selection
    /// to extend past the line
    abstract IsSelectionPastLine : bool with get

    /// The nrooff macros that separate paragraphs
    abstract Paragraphs : string with get, set

    /// The nrooff macros that separate sections
    abstract Sections : string with get, set

    abstract ShiftWidth : int with get, set

    abstract StartOfLine : bool with get, set

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
    abstract UseEditorSettings : bool with get, set

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

    /// Whether or not to expand tabs into spaces
    abstract ExpandTab : bool with get, set

    /// Return the handle to the global IVimSettings instance
    abstract GlobalSettings : IVimGlobalSettings

    /// Whether or not to put the numbers on the left column of the display
    abstract Number : bool with get, set

    /// Fromats that vim considers a number for CTRL-A and CTRL-X
    abstract NumberFormats : string with get, set

    /// How many spaces a tab counts for 
    abstract TabStop : int with get, set

    /// Which characters escape quotes for certain motion types
    abstract QuoteEscape : string with get, set

    /// Is the provided NumberFormat supported by the current options
    abstract IsNumberFormatSupported : NumberFormat -> bool

    inherit IVimSettings

/// Settings which are local to a given window.
and IVimWindowSettings = 

    /// Whether or not to highlight the line the cursor is on
    abstract CursorLine : bool with get, set

    /// Return the handle to the global IVimSettings instance
    abstract GlobalSettings : IVimGlobalSettings

    /// The scroll size 
    abstract Scroll : int with get, set

    inherit IVimSettings

/// Implements a list for storing history items.  This is used for the 5 types
/// of history lists in Vim (:help history).  
type HistoryList () = 

    let mutable _list : string list = List.empty
    let mutable _limit = Constants.DefaultHistoryLength

    /// Limit of the items stored in the list
    member x.Limit 
        with get () = 
            _limit
        and set value = 
            _limit <- value
            x.MaybeTruncateList()

    member x.Items = _list

    /// Adds an item to the top of the history list
    member x.Add value = 
        if not (StringUtil.isNullOrEmpty value) then
            let list =
                _list
                |> Seq.filter (fun x -> not (StringUtil.isEqual x value))
                |> Seq.truncate (_limit - 1)
                |> List.ofSeq
            _list <- value :: list

    /// Clear all of the items from the collection
    member x.Clear () = 
        _list <- List.empty

    member private x.MaybeTruncateList () = 
        if _list.Length > _limit then
            _list <-
                _list
                |> Seq.truncate _limit
                |> List.ofSeq

    interface System.Collections.IEnumerable with
        member x.GetEnumerator () = 
            let seq = _list :> string seq
            seq.GetEnumerator() :> System.Collections.IEnumerator

    interface System.Collections.Generic.IEnumerable<string> with
        member x.GetEnumerator () = 
            let seq = _list :> string seq
            seq.GetEnumerator()

/// Used for helping history editing 
type internal IHistoryClient<'TData, 'TResult> =

    /// History list used by this client
    abstract HistoryList : HistoryList

    /// What remapping mode if any should be used for key input
    abstract RemapMode : KeyRemapMode option

    /// Beep
    abstract Beep : unit -> unit

    /// Process the new command with the previous TData value
    abstract ProcessCommand : 'TData -> string -> 'TData

    /// Called when the command is completed.  The last valid TData and command
    /// string will be provided
    abstract Completed : 'TData -> string -> 'TResult

    /// Called when the command is cancelled.  The last valid TData value will
    /// be provided
    abstract Cancelled : 'TData -> unit

/// Represents shared state which is available to all IVimBuffer instances.
type IVimData = 

    /// The history of the : command list
    abstract CommandHistory : HistoryList with get, set

    /// The ordered list of incremental search values
    abstract SearchHistory : HistoryList with get, set

    /// Motion function used with the last f, F, t or T motion.  The 
    /// first item in the tuple is the forward version and the second item
    /// is the backwards version
    abstract LastCharSearch : (CharSearchKind * Path * char) option with get, set

    /// The last command which was ran 
    abstract LastCommand : StoredCommand option with get, set

    /// The last macro register which was run
    abstract LastMacroRun : char option with get, set

    /// Last pattern searched for in any buffer.
    abstract LastPatternData : PatternData with get, set

    /// Data for the last substitute command performed
    abstract LastSubstituteData : SubstituteData option with get, set

    /// Raise the highlight search one time disabled event
    abstract RaiseHighlightSearchOneTimeDisable : unit -> unit

    /// Raised when the 'LastPatternData' value changes
    [<CLIEvent>]
    abstract LastPatternDataChanged : IEvent<PatternData>

    /// Raised when highlight search is disabled one time via the :noh command
    [<CLIEvent>]
    abstract HighlightSearchOneTimeDisabled : IEvent<unit>

/// Core parts of an IVimBuffer
type VimBufferData = {

    TextView : ITextView

    StatusUtil : IStatusUtil

    UndoRedoOperations : IUndoRedoOperations

    VimTextBuffer : IVimTextBuffer
} with

    member x.JumpList = x.VimTextBuffer.JumpList

    member x.LocalSettings = x.VimTextBuffer.LocalSettings

    member x.Vim = x.VimTextBuffer.Vim

/// Vim instance.  Global for a group of buffers
and IVim =

    /// Buffer actively processing input.  This has no relation to the IVimBuffer
    /// which has focus 
    abstract ActiveBuffer : IVimBuffer option

    /// Get the set of tracked IVimBuffer instances
    abstract Buffers : IVimBuffer list

    /// Get the IVimBuffer which currently has KeyBoard focus
    abstract FocusedBuffer : IVimBuffer option

    /// Is the VimRc loaded
    abstract IsVimRcLoaded : bool

    /// IKeyMap for this IVim instance
    abstract KeyMap : IKeyMap

    /// IMacroRecorder for the IVim instance
    abstract MacroRecorder : IMacroRecorder

    /// IMarkMap for the IVim instance
    abstract MarkMap : IMarkMap

    /// IRegisterMap for the IVim instance
    abstract RegisterMap : IRegisterMap

    /// ISearchService for this IVim instance
    abstract SearchService : ISearchService

    abstract GlobalSettings : IVimGlobalSettings

    abstract VimData : IVimData 

    abstract VimHost : IVimHost

    /// The Local Setting's which were persisted from loading VimRc.  If the 
    /// VimRc isn't loaded yet or if no VimRc was loaded a IVimLocalSettings
    /// with all default values will be returned.  Will store a copy of whatever 
    /// is passed in order to prevent memory leaks from captured ITextView`s
    abstract VimRcLocalSettings : IVimLocalSettings with get, set

    /// Create an IVimBuffer for the given ITextView
    abstract CreateVimBuffer : ITextView -> IVimBuffer

    /// Create an IVimTextBuffer for the given ITextBuffer
    abstract CreateVimTextBuffer : ITextBuffer -> IVimTextBuffer

    /// Get the IVimBuffer associated with the given ITextView
    abstract GetVimBuffer : ITextView -> IVimBuffer option

    /// Get the IVimTextBuffer associated with the given ITextBuffer
    abstract GetVimTextBuffer : ITextBuffer -> IVimTextBuffer option

    /// Get or create an IVimBuffer for the given ITextView
    abstract GetOrCreateVimBuffer : ITextView -> IVimBuffer

    /// Get or create an IVimTextBuffer for the given ITextBuffer
    abstract GetOrCreateVimTextBuffer : ITextBuffer -> IVimTextBuffer

    /// Load the VimRc file.  If the file was previously loaded a new load will be 
    /// attempted.  Returns true if a VimRc was actually loaded
    ///
    /// TODO: Should rethink this API for pushing a func down to create the 
    /// ITextView.  Forces awkward code in host factory start methods
    abstract LoadVimRc : createViewFunc:(unit -> ITextView) -> bool

    /// Remove the IVimBuffer associated with the given view.  This will not actually close
    /// the IVimBuffer but instead just removes it's association with the given view
    abstract RemoveBuffer : ITextView -> bool

and SwitchModeEventArgs 
    (
        _previousMode : IMode option,
        _currentMode : IMode
    ) = 

    inherit System.EventArgs()

    /// Current IMode 
    member x.CurrentMode = _currentMode

    /// Previous IMode.  Expressed as an Option because the first mode switch
    /// has no previous one
    member x.PreviousMode = _previousMode

/// This is the interface which represents the parts of a vim buffer which are shared amongst all
/// of it's views
and IVimTextBuffer = 

    /// The associated ITextBuffer instance
    abstract TextBuffer : ITextBuffer

    /// The associated IVimGlobalSettings instance
    abstract GlobalSettings : IVimGlobalSettings

    /// The associated IJumpList instance
    abstract JumpList : IJumpList

    /// The last VisualSpan selection for the IVimTextBuffer.  This is a combination of a VisualSpan
    /// and the SnapshotPoint within the span where the caret should be positioned
    abstract LastVisualSelection : VisualSelection option with get, set

    /// The associated IVimLocalSettings instance
    abstract LocalSettings : IVimLocalSettings

    /// ModeKind of the current mode of the IVimTextBuffer.  It may seem odd at first to put ModeKind
    /// at this level but it is indeed shared amongst all views.  This can be demonstrated by opening
    /// the same file in multiple tabs, switch to insert in one and then move to the other via the
    /// mouse and noting it is also in Insert mode.  Actual IMode values are ITextView specific though
    /// and only live at the ITextView level
    abstract ModeKind : ModeKind

    /// Name of the buffer.  Used for items like Marks
    abstract Name : string

    /// The associated IVim instance
    abstract Vim : IVim

    /// The ITextStructureNavigator for word values in the ITextBuffer
    abstract WordNavigator : ITextStructureNavigator

    /// Switch the current mode to the provided value
    abstract SwitchMode : ModeKind -> ModeArgument -> unit

    /// Raised when the mode is switched.  Returns the old and new mode 
    [<CLIEvent>]
    abstract SwitchedMode : IEvent<ModeKind * ModeArgument>

/// Main interface for the Vim editor engine so to speak. 
and IVimBuffer =

    /// Sequence of available Modes
    abstract AllModes : seq<IMode>

    /// Buffered KeyInput list.  When a key remapping has multiple source elements the input 
    /// is buffered until it is completed or the ambiguity is removed.  
    abstract BufferedRemapKeyInputs : KeyInput list

    /// IIncrementalSearch instance associated with this IVimBuffer
    abstract IncrementalSearch : IIncrementalSearch

    /// Whether or not the IVimBuffer is currently processing a KeyInput value
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

    /// Global settings for the buffer
    abstract GlobalSettings : IVimGlobalSettings

    /// Local settings for the buffer
    abstract LocalSettings : IVimLocalSettings

    /// Register map for IVim.  Global to all IVimBuffer instances but provided here
    /// for convenience
    abstract RegisterMap : IRegisterMap

    /// Underlying ITextBuffer Vim is operating under
    abstract TextBuffer : ITextBuffer

    /// Current ITextSnapshot of the ITextBuffer
    abstract TextSnapshot : ITextSnapshot

    /// View of the file
    abstract TextView : ITextView

    /// The IMotionUtil associated with this IVimBuffer instance
    abstract MotionUtil : IMotionUtil

    /// The IUndoRedoOperations associated with this IVimBuffer instance
    abstract UndoRedoOperations : IUndoRedoOperations

    /// Owning IVim instance
    abstract Vim : IVim

    /// Associated IVimTextBuffer
    abstract VimTextBuffer : IVimTextBuffer

    /// VimBufferData for the given IVimBuffer
    abstract VimBufferData : VimBufferData

    /// The ITextStructureNavigator for word values in the buffer
    abstract WordNavigator : ITextStructureNavigator

    /// Associated IVimWindowSettings
    abstract WindowSettings : IVimWindowSettings

    /// Associated IVimData instance
    abstract VimData : IVimData

    /// INormalMode instance for normal mode
    abstract NormalMode : INormalMode

    /// ICommandMode instance for command mode
    abstract CommandMode : ICommandMode 

    /// IDisabledMode instance for disabled mode
    abstract DisabledMode : IDisabledMode

    /// IVisualMode for visual line mode
    abstract VisualLineMode : IVisualMode

    /// IVisualMode for visual block mode
    abstract VisualBlockMode : IVisualMode

    /// IVisualMode for visual character mode
    abstract VisualCharacterMode : IVisualMode

    /// IInsertMode instance for insert mode
    abstract InsertMode : IInsertMode

    /// IInsertMode instance for replace mode
    abstract ReplaceMode : IInsertMode

    /// ISubstituteConfirmDoe instance for substitute confirm mode
    abstract SubstituteConfirmMode : ISubstituteConfirmMode

    /// IMode instance for external edits
    abstract ExternalEditMode : IMode

    /// Get the register of the given name
    abstract GetRegister : RegisterName -> Register

    /// Get the specified Mode
    abstract GetMode : ModeKind -> IMode

    /// Get the KeyInput value produced by this KeyInput in the current state of the
    /// IVimBuffer.  This will consider any buffered KeyInput values.
    abstract GetKeyInputMapping : KeyInput -> KeyMappingResult

    /// Process the KeyInput and return whether or not the input was completely handled
    abstract Process : KeyInput -> ProcessResult

    /// Can the passed in KeyInput be processed by the current state of IVimBuffer.  The
    /// provided KeyInput will participate in remapping based on the current mode
    abstract CanProcess: KeyInput -> bool

    /// Can the passed in KeyInput be processed as a Vim command by the current state of
    /// the IVimBuffer.  The provided KeyInput will participate in remapping based on the
    /// current mode
    ///
    /// This is very similar to CanProcess except it will return false for any KeyInput
    /// which would be processed as a direct insert.  In other words commands like 'a',
    /// 'b' when handled by insert / replace mode
    abstract CanProcessAsCommand : KeyInput -> bool

    /// Switch the current mode to the provided value
    abstract SwitchMode : ModeKind -> ModeArgument -> IMode

    /// Switch the buffer back to the previous mode which is returned
    abstract SwitchPreviousMode : unit -> IMode

    /// Add a processed KeyInput value.  This is a way for a host which is intercepting 
    /// KeyInput and custom processing it to still participate in items like Macro 
    /// recording.  The provided value will not go through any remapping
    abstract SimulateProcessed : KeyInput -> unit

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

    /// Raised when a warning is encountered
    [<CLIEvent>]
    abstract WarningMessage : IEvent<string>

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

/// Interface for a given Mode of Vim.  For example normal, insert, etc ...
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

/// This is the interface implemented by Insert and Replace mode
and IInsertMode =

    /// The active IWordCompletionSession if one is active
    abstract ActiveWordCompletionSession : IWordCompletionSession option

    /// Is InsertMode currently processing a Text Input value
    abstract IsProcessingDirectInsert : bool

    /// Is this KeyInput value considered to be a direct insert command in the current
    /// state of the IVimBuffer.  This does not apply to commands which edit the buffer
    /// like 'CTRL-D' but instead commands like 'a', 'b' which directly edit the 
    /// ITextBuffer
    abstract IsDirectInsert : KeyInput -> bool

    /// Raised when a command is successfully run
    [<CLIEvent>]
    abstract CommandRan : IEvent<CommandRunData>

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

[<Extension>]
module VimExtensions = 
    
    /// Is this ModeKind any type of Insert: Insert or Replace
    [<Extension>]
    let IsAnyInsert modeKind = 
        modeKind = ModeKind.Insert ||
        modeKind = ModeKind.Replace

