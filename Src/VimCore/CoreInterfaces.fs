#light

namespace Vim
open EditorUtils
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Utilities
open System.Diagnostics
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections.Generic
open Vim.Interpreter

[<RequireQualifiedAccess>]
[<NoComparison>]
type CaretMovement =
    | Up
    | Right
    | Down
    | Left
    | Home
    | End
    | PageUp
    | PageDown

    with 

    static member OfDirection direction =
        match direction with
        | Direction.Up -> CaretMovement.Up
        | Direction.Right -> CaretMovement.Right
        | Direction.Down -> CaretMovement.Down
        | Direction.Left -> CaretMovement.Left
        | _ -> raise (Contract.GetInvalidEnumException direction)

type TextViewEventArgs(_textView : ITextView) =
    inherit System.EventArgs()

    member x.TextView = _textView

[<RequireQualifiedAccess>]
type VimRcState =
    /// The VimRc file has not been processed at this point
    | None

    /// The load succeeded and the specified file was used 
    | LoadSucceeded of string

    /// The load failed 
    | LoadFailed

[<RequireQualifiedAccess>]
[<NoComparison>]
[<NoEquality>]
type HostResult =
    | Success
    | Error of string

[<RequireQualifiedAccess>]
[<NoComparison>]
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

type FileContents = {

    /// Full path to the file which the contents were loaded from
    FilePath : string

    /// Actual lines in the file
    Lines : string[]
}

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
    abstract LoadVimRcContents : unit -> FileContents option

    /// Attempt to read all of the lines from the given file 
    abstract ReadAllLines : filePath : string -> string[] option

/// Utility function for searching for Word values.  This is a MEF importable
/// component
[<UsedInBackgroundThread>]
type IWordUtil = 

    /// Get the full word span for the word value which crosses the given SnapshotPoint
    abstract GetFullWordSpan : wordKind : WordKind -> point : SnapshotPoint -> SnapshotSpan option

    /// Get the SnapshotSpan for Word values from the given point.  If the provided point is 
    /// in the middle of a word the span of the entire word will be returned
    abstract GetWords : wordKind : WordKind -> path : Path -> point : SnapshotPoint -> SnapshotSpan seq

    /// Create an ITextStructureNavigator where the extent of words is calculated for
    /// the specified WordKind value
    abstract CreateTextStructureNavigator : wordKind : WordKind -> contentType : IContentType -> ITextStructureNavigator

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
    abstract Dismissed: IDelegateEvent<System.EventHandler>

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

    /// Is there an open linked undo transaction
    abstract InLinkedUndoTransaction : bool

    /// StatusUtil instance that is used to report errors
    abstract StatusUtil : IStatusUtil

    /// Close the IUndoRedoOperations and remove any attached event handlers
    abstract Close : unit -> unit

    /// Creates an Undo Transaction
    abstract CreateUndoTransaction : name : string -> IUndoTransaction

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

/// Represents a set of changes to a contiguous region. 
[<RequireQualifiedAccess>]
type TextChange = 
    | DeleteLeft of int
    | DeleteRight of int
    | Insert of string
    | Combination of TextChange * TextChange

    with 

    /// Get the insert text resulting from the change if there is any
    member x.InsertText = 
        let rec inner textChange (text : string) = 
            match textChange with 
            | Insert data -> text + data |> Some
            | DeleteLeft count -> 
                if count > text.Length then
                    None
                else 
                    text.Substring(0, text.Length - count) |> Some
            | DeleteRight _ -> None
            | Combination (left, right) ->
                match inner left text with
                | None -> None
                | Some text -> inner right text

        inner x StringUtil.empty

    /// Get the last / most recent change in the TextChange tree
    member x.LastChange = 
        match x with
        | DeleteLeft _ -> x
        | DeleteRight _ -> x
        | Insert _ -> x
        | Combination (_, right) -> right.LastChange

    member x.IsEmpty = 
        match x with 
        | Insert text -> StringUtil.isNullOrEmpty text
        | DeleteLeft count -> count = 0
        | DeleteRight count -> count = 0
        | Combination (left, right) -> left.IsEmpty && right.IsEmpty

    member x.Reduce = 
        match x with 
        | Combination (left, right) -> 
            match TextChange.ReduceCore left right with
            | Some reduced -> reduced
            | None -> x
        | _ -> x

    /// Merge two TextChange values together.  The goal is to produce a the smallest TextChange
    /// value possible.  This will return Some if a reduction is made and None if there is 
    /// no possible reduction
    static member private ReduceCore left right =

        // This is called for combination merges.  Some progress was made but it's possible further
        // progress could be made by reducing the specified values.  If further progress can be made
        // keep it else keep at least the progress already made
        let tryReduceAgain left right = 
            let value = 
                match TextChange.ReduceCore left right with
                | None -> Combination (left, right) 
                | Some reducedTextChange -> reducedTextChange
            Some value 

        // Insert can only merge with a previous insert operation.  It can't 
        // merge with any deletes that came before it
        let reduceInsert before (text : string) =
            match before with
            | Insert otherText -> Some (Insert (otherText + text))
            | _ -> None 

        // DeleteLeft can merge with deletes and previous insert operations. 
        let reduceDeleteLeft before count = 
            match before with
            | Insert otherText -> 
                if count <= otherText.Length then
                    let text = otherText.Substring(0, otherText.Length - count)
                    Some (Insert text)
                else
                    let count = count - otherText.Length
                    Some (DeleteLeft count)
            | DeleteLeft beforeCount -> Some (DeleteLeft (count + beforeCount))
            | _ -> None

        // Delete right can merge only with other DeleteRight operations
        let reduceDeleteRight before count = 
            match before with 
            | DeleteRight beforeCount -> Some (DeleteRight (beforeCount + count))
            | _ -> None 

        // The item on the left isn't a Combination item.  So do simple merge semantics
        let simpleMerge () = 
            match right with
            | Insert text -> reduceInsert left text
            | DeleteLeft count -> reduceDeleteLeft left count
            | DeleteRight count -> reduceDeleteRight left count 
            | Combination (rightSubLeft, rightSubRight) ->
                // First just see if the combination itself can be reduced 
                match TextChange.ReduceCore rightSubLeft rightSubRight with
                | Some reducedTextChange -> tryReduceAgain left reducedTextChange
                | None -> 
                    match TextChange.ReduceCore left rightSubLeft with
                    | None -> None
                    | Some reducedTextChange -> tryReduceAgain reducedTextChange rightSubRight

        // The item on the left is a Combination item.  
        let complexMerge leftSubLeft leftSubRight = 

            // First check if the left can be merged against itself.  This can easily happen
            // for hand built trees
            match TextChange.ReduceCore leftSubLeft leftSubRight with
            | Some reducedTextChange -> tryReduceAgain reducedTextChange right
            | None -> 
                // It can't be reduced.  Still a change that the right can be reduced against 
                // the subRight value 
                match TextChange.ReduceCore leftSubRight right with
                | None -> None
                | Some reducedTextChange -> tryReduceAgain leftSubLeft reducedTextChange

        if left.IsEmpty then
            Some right
        elif right.IsEmpty then
            Some left
        else
            match left with
            | Insert _ -> simpleMerge ()
            | DeleteLeft _ -> simpleMerge ()
            | DeleteRight _ -> simpleMerge ()
            | Combination (leftSubLeft, leftSubRight) -> complexMerge leftSubLeft leftSubRight

    static member Replace str =
        let left = str |> StringUtil.length |> TextChange.DeleteLeft
        let right = TextChange.Insert str
        TextChange.Combination (left, right)

    static member CreateReduced left right = 
        match TextChange.ReduceCore left right with
        | None -> Combination (left, right)
        | Some textChange -> textChange

type TextChangeEventArgs(_textChange : TextChange) =
    inherit System.EventArgs()

    member x.TextChange = _textChange

[<System.Flags>]
type SearchOptions = 
    | None = 0x0

    /// Consider the "ignorecase" option when doing the search
    | ConsiderIgnoreCase = 0x1

    /// Consider the "smartcase" option when doing the search
    | ConsiderSmartCase = 0x2

    /// ConsiderIgnoreCase ||| ConsiderSmartCase
    | Default = 0x3

/// Information about a search of a pattern
type PatternData = {

    /// The Pattern to search for
    Pattern : string

    /// The direction in which the pattern was searched for
    Path : Path
}

type PatternDataEventArgs(_patternData : PatternData) =
    inherit System.EventArgs()

    member x.PatternData = _patternData

/// An incremental search can be augmented with a offset of characters or a line
/// count.  This is described in full in :help searh-offset'
[<RequireQualifiedAccess>]
[<StructuralEquality>]
[<NoComparison>]
type SearchOffsetData =
    | None
    | Line of int
    | Start of int
    | End of int
    | Search of PatternData

    with 

    static member private ParseCore (offset : string) =
        Contract.Requires (offset.Length > 0)

        let index = ref 0
        let isForward = ref true
        let movePastPlusOrMinus () = 
            if index.Value < offset.Length then
                match offset.[index.Value] with
                | '+' -> 
                    isForward := true
                    index := index.Value + 1
                    true
                | '-' ->
                    isForward := false
                    index := index.Value + 1
                    true
                | _ -> false
            else
                false

        let parseNumber () = 
            if movePastPlusOrMinus () && index.Value = offset.Length then
                // a single + or - counts as a value if it's not followed by
                // a number 
                if isForward.Value then 1
                else -1
            else
                // parse out the digits after the value
                let mutable num = 0
                let mutable isBad = index.Value >= offset.Length
                while index.Value < offset.Length do
                    num <- num * 10
                    match CharUtil.GetDigitValue (offset.[index.Value]) with
                    | Option.None -> 
                        isBad <- true
                        index := offset.Length
                    | Option.Some d ->
                        num <- num + d
                        index := index.Value + 1
                if isBad then
                    0
                elif isForward.Value then
                    num
                else    
                    -num

        let parseLine () = 
            let number = parseNumber ()
            SearchOffsetData.Line number

        let parseEnd () = 
            index := index.Value + 1
            let number = parseNumber ()
            SearchOffsetData.End number
            
        let parseStart () = 
            index := index.Value + 1
            let number = parseNumber ()
            SearchOffsetData.Start number

        let parseSearch () = 
            index := index.Value + 1
            match StringUtil.charAtOption index.Value offset with
            | Option.Some '/' -> 
                let path = Path.Forward
                let pattern = offset.Substring(index.Value + 1)
                SearchOffsetData.Search ({ Pattern = pattern; Path = path})
            | Option.Some '?' -> 
                let path = Path.Backward
                let pattern = offset.Substring(index.Value + 1)
                SearchOffsetData.Search ({ Pattern = pattern; Path = path})
            | _ ->
                SearchOffsetData.None

        if CharUtil.IsDigit (offset.[0]) then
            parseLine ()
        else
            match offset.[0] with
            | '-' -> parseLine ()
            | '+' -> parseLine ()
            | 'e' -> parseEnd ()
            | 's' -> parseStart ()
            | 'b' -> parseStart ()
            | ';' -> parseSearch () 
            | _ -> SearchOffsetData.None

    static member Parse (offset : string) =
        if StringUtil.isNullOrEmpty offset then
            SearchOffsetData.None
        else
            SearchOffsetData.ParseCore offset

[<Sealed>]
type SearchData
    (
        _pattern : string,
        _offset : SearchOffsetData,
        _kind : SearchKind,
        _options : SearchOptions
    ) = 

    new (pattern : string, path : Path, isWrap : bool) =
        let kind = SearchKind.OfPathAndWrap path isWrap
        SearchData(pattern, SearchOffsetData.None, kind, SearchOptions.Default)

    new (pattern : string, path : Path) = 
        let kind = SearchKind.OfPathAndWrap path true
        SearchData(pattern, SearchOffsetData.None, kind, SearchOptions.Default)

    /// The pattern being searched for in the buffer
    member x.Pattern = _pattern

    /// The offset that is applied to the search
    member x.Offset = _offset

    member x.Kind = _kind

    member x.Options = _options

    member x.Path = x.Kind.Path

    /// The PatternData which was searched for.  This does not include the pattern searched
    /// for in the offset
    member x.PatternData = { Pattern = x.Pattern; Path = x.Kind.Path }

    /// The PatternData which should be used for IVimData.LastPatternData if this search needs
    /// to update that value.  It takes into account the search pattern in an offset string
    /// as specified in ':help //;'
    member x.LastPatternData =
        let path = x.Path
        let pattern = 
            match x.Offset with
            | SearchOffsetData.Search patternData -> patternData.Pattern
            | _ -> x.Pattern
        { Pattern = pattern; Path = path }

    member x.Equals(other: SearchData) =
        if obj.ReferenceEquals(other, null) then
            false
        else
            _pattern = other.Pattern &&
            _offset = other.Offset &&
            _kind = other.Kind &&
            _options = other.Options

    override x.Equals(other : obj) = 
        match other with
        | :? SearchData as otherSearchData -> x.Equals(otherSearchData);
        | _ -> false 

    override x.GetHashCode() =
        _pattern.GetHashCode()

    static member op_Equality(this, other) = System.Collections.Generic.EqualityComparer<SearchData>.Default.Equals(this, other)
    static member op_Inequality(this, other) = not (System.Collections.Generic.EqualityComparer<SearchData>.Default.Equals(this, other))

    /// Parse out a SearchData value given the specific pattern and SearchKind.  The pattern should
    /// not include a beginning / or ?.  That should be removed by this point
    static member Parse (pattern : string) (searchKind : SearchKind) searchOptions =
        let mutable index = -1
        let mutable i = 1
        let targetChar = 
            if searchKind.IsAnyForward then '/'
            else '?'
        while i < pattern.Length do
            if pattern.[i] = targetChar && pattern.[i-1] <> '\\' then
                index <- i
                i <- pattern.Length
            else
                i <- i + 1

        if index < 0 then
            SearchData(pattern, SearchOffsetData.None, searchKind, searchOptions)
        else
            let offset = SearchOffsetData.Parse (pattern.Substring(index + 1))
            let pattern = pattern.Substring(0, index)
            SearchData(pattern, offset, searchKind, searchOptions)

    interface System.IEquatable<SearchData> with
        member x.Equals other = x.Equals(other)

type SearchDataEventArgs(_searchData : SearchData) =
    inherit System.EventArgs()

    member x.SearchData = _searchData

/// Result of an individual search
[<RequireQualifiedAccess>]
type SearchResult =

    /// The pattern was found.  The two spans here represent the following pieces of data
    /// respectively 
    ///
    ///     - the span of the pattern + the specified offset
    ///     - the span of the found pattern 
    ///
    /// In general the first should be used by consumers.  The second is only interesting
    /// to items that need to tag the value 
    ///
    /// The bool at the end of the tuple represents whether not
    /// a wrap occurred while searching for the value
    | Found of SearchData * SnapshotSpan * SnapshotSpan * bool

    /// The pattern was not found.  The bool is true if the word was present in the ITextBuffer
    /// but wasn't found do to the lack of a wrap in the SearchData value
    | NotFound of SearchData * bool

    with

    /// Returns the SearchData which was searched for
    member x.SearchData = 
        match x with 
        | SearchResult.Found (searchData, _, _, _) -> searchData
        | SearchResult.NotFound (searchData, _) -> searchData

type SearchResultEventArgs(_searchResult : SearchResult) = 
    inherit System.EventArgs()

    member x.SearchResult = _searchResult

/// Global information about searches within Vim.  
///
/// This interface is usable from any thread
[<UsedInBackgroundThread()>]
type ISearchService = 

    /// Find the next occurrence of the pattern in the buffer starting at the 
    /// given SnapshotPoint
    abstract FindNext : searchPoint : SnapshotPoint -> searchData : SearchData -> navigator : ITextStructureNavigator -> SearchResult

    /// Find the next Nth occurrence of the search data
    abstract FindNextMultiple : searchPoint : SnapshotPoint -> searchData : SearchData -> navigator : ITextStructureNavigator -> count : int -> SearchResult

    /// Find the next 'count' occurrence of the specified pattern.  Note: The first occurrence won't
    /// match anything at the provided start point.  That will be adjusted appropriately
    abstract FindNextPattern : searchPoint : SnapshotPoint -> searchPoint : SearchData -> navigator : ITextStructureNavigator -> count : int -> SearchResult

/// Column information about the caret in relation to this Motion Result
[<RequireQualifiedAccess>]
[<NoComparison>]
type CaretColumn = 

    /// No column information was provided
    | None

    /// Caret should be placed in the specified character on the last line in 
    /// the MotionResult
    ///
    /// This column should be specified in terms of a character offset in the ITextBuffer
    /// and shouldn't consider items like how wide a tab is.  A tab should be a single
    /// character
    | InLastLine of int

    /// Caret should be placed in the specified column on the last line in 
    /// the MotionResult
    ///
    /// This column should be specified in terms number of screen columns, where 
    /// some characters like tabs may span many columns.
    | ScreenColumn of int

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

    /// This motion was promoted under rule #2 to a line wise motion
    | ExclusiveLineWise = 0x2

    /// This motion when used as a movement should maintain the caret column
    /// setting.
    | MaintainCaretColumn = 0x4

    /// This flag is needed to disambiguate the cases which come up when there
    /// is an empty last line in the buffer.  When that happens it's ambiguous 
    /// if a line wise motion meant to include the last line or merely just 
    /// the line above.  The End is the same in both cases.
    | IncludeEmptyLastLine = 0x8

    /// Marker for the end of line motion.  This affects how the caret column
    /// is maintained
    | EndOfLine = 0x10

/// Information about the type of the motion this was.
[<RequireQualifiedAccess>]
[<NoComparison>]
type MotionKind =

    | CharacterWiseInclusive

    | CharacterWiseExclusive

    | LineWise

/// Data about a complete motion operation. 
type MotionResult = {

    /// Span of the motion.
    Span : SnapshotSpan

    /// In the case this MotionResult is the result of an exclusive promotion, this will 
    /// hold the original SnapshotSpan
    OriginalSpan : SnapshotSpan

    /// Is the motion forward
    IsForward : bool

    /// Kind of the motion
    MotionKind : MotionKind

    /// The flags on the MotionRelult
    MotionResultFlags : MotionResultFlags

    /// In addition to recording the Span, certain motions like j, k, and | also
    /// record data about the desired column within the span.  This value may or may not
    /// be a valid point within the line
    DesiredColumn : CaretColumn

} with

    /// The possible column of the MotionResult
    member x.CaretColumn = x.DesiredColumn

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

    /// The Start or Last line depending on whether tho motion is forward or not
    member x.DirectionLastLine = 
        if x.IsForward then
            // Need to handle the empty last line case here.  If the flag to include
            // the empty last line is set and we are at the empty line then it gets
            // included as the last line
            if SnapshotPointUtil.IsEndPoint x.Span.End && Util.IsFlagSet x.MotionResultFlags MotionResultFlags.IncludeEmptyLastLine then
                SnapshotPointUtil.GetContainingLine x.Span.End
            else
                SnapshotSpanUtil.GetLastLine x.Span
        else
            SnapshotSpanUtil.GetStartLine x.Span

    member x.Start = x.Span.Start

    member x.End = x.Span.End

    member x.Last = SnapshotSpanUtil.GetLastIncludedPoint x.Span

    member x.LastOrStart = x.Last |> OptionUtil.getOrDefault x.Start

    static member CreateExEx span isForward motionKind motionResultFlags desiredColumn = 
        {
            Span = span
            OriginalSpan = span
            IsForward = isForward
            MotionKind = motionKind
            MotionResultFlags = motionResultFlags 
            DesiredColumn = desiredColumn }

    static member CreateEx span isForward motionKind motionResultFlags = 
        MotionResult.CreateExEx span isForward motionKind motionResultFlags CaretColumn.None

    static member Create span isForward motionKind = MotionResult.CreateEx span isForward motionKind MotionResultFlags.None

/// Context on how the motion is being used.  Several motions (]] for example)
/// change behavior based on how they are being used
[<RequireQualifiedAccess>]
[<NoComparison>]
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

/// Char searches are interesting because they are defined in one IVimBuffer
/// and can be repeated in any IVimBuffer.  Use a discriminated union here 
/// to name the motion without binding it to a given IVimBuffer or ITextView 
/// which would increase the chance of an accidental memory leak
[<RequireQualifiedAccess>]
[<NoComparison>]
type CharSearchKind  =

    /// Used for the 'f' and 'F' motion.  To the specified char 
    | ToChar

    /// Used for the 't' and 'T' motion.  Till the specified char
    | TillChar

[<RequireQualifiedAccess>]
type BlockKind =

    /// A [] block
    | Bracket 

    /// A () block
    | Paren

    /// A <> block
    | AngleBracket

    /// A {} block
    | CurlyBracket

    with

    member x.Characters = 
        match x with 
        | Bracket -> '[', ']'
        | Paren -> '(', ')'
        | AngleBracket -> '<', '>'
        | CurlyBracket -> '{', '}'

[<RequireQualifiedAccess>]
[<NoComparison>]
type UnmatchedTokenKind =
    | Paren
    | CurlyBracket

/// A discriminated union of the Motion types supported.  These are the primary
/// repeat mechanisms for Motion arguments so it's very important that these 
/// are ITextView / IVimBuffer agnostic.  It will be very common for a Motion 
/// item to be stored and then applied to many IVimBuffer instances.
[<RequireQualifiedAccess>]
type Motion =

    /// Implement the all block motion
    | AllBlock of BlockKind

    /// Implement the 'aw' motion.  This is called once the a key is seen.
    | AllWord of WordKind

    /// Implement the 'ap' motion
    | AllParagraph 

    /// Gets count full sentences from the cursor.  If used on a blank line this will
    /// not return a value
    | AllSentence

    /// Move to the beginning of the line.  Interestingly since this command is bound to the '0' it 
    /// can't be associated with a count.  Doing a command like 30 binds as count 30 vs. count 3 
    /// for command '0'
    | BeginingOfLine

    /// The left motion for h
    | CharLeft 

    /// The right motion for l
    | CharRight

    /// The backspace motion
    | SpaceLeft 

    /// The space motion
    | SpaceRight

    /// The arrow motion for <Left>
    | ArrowLeft 

    /// The arrow motion for <Right>
    | ArrowRight

    /// Implements the f, F, t and T motions
    | CharSearch of CharSearchKind * Path * char

    /// Get the span of "count" display lines upward.  Display lines can differ when
    /// wrap is enabled
    | DisplayLineUp

    /// Get the span of "count" display lines downward.  Display lines can differ when
    /// wrap is enabled
    | DisplayLineDown

    /// Implement the 'e' motion.  This goes to the end of the current word.  If we're
    /// not currently on a word it will find the next word and then go to the end of that
    | EndOfWord of WordKind
    
    /// Implement an end of line motion.  Typically in response to the $ key.  Even though
    /// this motion deals with lines, it's still a character wise motion motion. 
    | EndOfLine

    /// Find the first non-blank character as the start of the span.  This is an exclusive
    /// motion so be careful we don't go to far forward.  Providing a count to this motion has
    /// no effect
    | FirstNonBlankOnCurrentLine

    /// Find the first non-blank character on the (count - 1) line below this line
    | FirstNonBlankOnLine

    /// Inner word motion
    | InnerWord of WordKind

    /// Inner block motion
    | InnerBlock of BlockKind

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
    /// the ` (back tick) operator and results in an exclusive motion
    | Mark of LocalMark

    /// Get the motion to the line of the specified mark.  This is typically
    /// accessed via the ' (single quote) operator and results in a 
    /// line wise motion
    | MarkLine of LocalMark

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
    | QuotedString of char

    /// The quoted string excluding the quotes
    | QuotedStringContents of char

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

    /// Forward a section in the editor or to a close brace
    | SectionForwardOrCloseBrace

    /// Forward a section in the editor
    | SectionForward

    /// Count sentences backward 
    | SentenceBackward

    /// Count sentences forward
    | SentenceForward

    /// Move the the specific column of the current line. Typically in response to the | key. 
    | ScreenColumn

    /// The [(, ]), ]}, [{ motions
    | UnmatchedToken of Path * UnmatchedTokenKind

    /// Implement the b/B motion
    | WordBackward of WordKind

    /// Implement the w/W motion
    | WordForward of WordKind 

/// Interface for running Motion instances against an ITextView
and IMotionUtil =

    /// The associated ITextView instance
    abstract TextView : ITextView

    /// Get the specified Motion value 
    abstract GetMotion : motion : Motion -> motionArgument : MotionArgument -> MotionResult option 

    /// Get the specific text object motion from the given SnapshotPoint
    abstract GetTextObject : motion : Motion -> point : SnapshotPoint -> MotionResult option

type ModeKind = 
    | Normal = 1
    | Insert = 2
    | Command = 3
    | VisualCharacter = 4
    | VisualLine = 5
    | VisualBlock = 6 
    | Replace = 7
    | SubstituteConfirm = 8
    | SelectCharacter = 9
    | SelectLine = 10 
    | SelectBlock = 11
    | ExternalEdit = 12

    /// Initial mode for an IVimBuffer.  It will maintain this mode until the underlying
    /// ITextView completes it's initialization and allows the IVimBuffer to properly 
    /// transition to the mode matching it's underlying IVimTextBuffer
    | Uninitialized = 13

    /// Mode when Vim is disabled.  It won't interact with events it otherwise would such
    /// as selection changes
    | Disabled = 42

[<RequireQualifiedAccess>]
type VisualKind =
    | Character
    | Line
    | Block
    with 

    /// The TextSelectionMode this VisualKind would require
    member x.TextSelectionMode = 
        match x with
        | Character _ -> TextSelectionMode.Stream
        | Line _ -> TextSelectionMode.Stream
        | Block _ -> TextSelectionMode.Box

    member x.VisualModeKind = 
        match x with
        | Character _ -> ModeKind.VisualCharacter
        | Line _ -> ModeKind.VisualLine
        | Block _ -> ModeKind.VisualBlock

    member x.SelectModeKind = 
        match x with
        | Character _ -> ModeKind.SelectCharacter
        | Line _ -> ModeKind.SelectLine
        | Block _ -> ModeKind.SelectBlock

    static member All = [ Character; Line; Block ] |> Seq.ofList

    static member OfModeKind kind = 
        match kind with 
        | ModeKind.VisualBlock -> VisualKind.Block |> Some
        | ModeKind.VisualLine -> VisualKind.Line |> Some
        | ModeKind.VisualCharacter -> VisualKind.Character |> Some
        | _ -> None

    static member IsAnyVisual kind = VisualKind.OfModeKind kind |> Option.isSome

    static member IsAnySelect kind = 
        match kind with
        | ModeKind.SelectCharacter -> true
        | ModeKind.SelectLine -> true
        | ModeKind.SelectBlock -> true
        | _ -> false

    static member IsAnyVisualOrSelect kind = VisualKind.IsAnyVisual kind || VisualKind.IsAnySelect kind

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
[<RequireQualifiedAccess>]
[<CustomEquality>]
[<CustomComparison>]
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
        | OneKeyInput _ -> 1
        | TwoKeyInputs _ -> 2
        | ManyKeyInputs list -> list.Length

    /// Add a KeyInput to the end of this KeyInputSet and return the 
    /// resulting value
    member x.Add ki =
        match x with 
        | Empty -> OneKeyInput ki
        | OneKeyInput previous -> TwoKeyInputs(previous,ki)
        | TwoKeyInputs (p1, p2) -> ManyKeyInputs [p1;p2;ki]
        | ManyKeyInputs list -> ManyKeyInputs (list @ [ki])

    /// Does the name start with the given KeyInputSet
    member x.StartsWith (targetName : KeyInputSet) = 
        match targetName,x with
        | Empty, _ -> true
        | OneKeyInput leftKi, OneKeyInput rightKi ->  leftKi = rightKi
        | OneKeyInput leftKi, TwoKeyInputs (rightKi, _) -> leftKi = rightKi
        | _ -> 
            let left = targetName.KeyInputs 
            let right = x.KeyInputs
            if left.Length <= right.Length then
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
        | OneKeyInput ki -> ki.GetHashCode()
        | TwoKeyInputs (k1, k2) -> k1.GetHashCode() ^^^ k2.GetHashCode()
        | ManyKeyInputs list -> 
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

    let OfChar c = c |> KeyInputUtil.CharToKeyInput |> KeyInputSet.OneKeyInput

    let OfCharArray ([<System.ParamArray>] arr) = 
        arr
        |> Seq.ofArray
        |> Seq.map KeyInputUtil.CharToKeyInput
        |> OfSeq

    let OfString (str:string) = str |> Seq.map KeyInputUtil.CharToKeyInput |> OfSeq

    let OfVimKeyArray ([<System.ParamArray>] arr) = 
        arr 
        |> Seq.ofArray 
        |> Seq.map KeyInputUtil.VimKeyToKeyInput
        |> OfSeq

    let Combine (left : KeyInputSet) (right : KeyInputSet) =
        let all = left.KeyInputs @ right.KeyInputs
        OfList all

[<RequireQualifiedAccess>]
[<NoComparison>]
[<NoEquality>]
type KeyMappingResult =

    /// The values were mapped completely and require no further mapping. This 
    /// could be a result of a no-op mapping though
    | Mapped of KeyInputSet

    /// The values were partially mapped but further mapping is required once the
    /// keys which were mapped are processed.  The values are 
    ///
    ///  mapped KeyInputSet * remaining KeyInputSet
    | PartiallyMapped of KeyInputSet * KeyInputSet

    /// The mapping encountered a recursive element that had to be broken 
    | Recursive

    /// More input is needed to resolve this mapping.
    | NeedsMoreInput of KeyInputSet

/// Represents the span for a Visual Character mode selection.  If it weren't for the
/// complications of tracking a visual character selection across edits to the buffer
/// there would really be no need for this and we could instead just represent it as 
/// a SnapshotSpan
///
/// The other reason this type is necessary is to differentiate several empty line 
/// cases.  Essentially how empty line should be handled when the span includes the 
/// line break of the line above 
[<StructuralEquality>]
[<NoComparison>]
[<Struct>]
[<DebuggerDisplay("{ToString()}")>]
type CharacterSpan = 

    val private _start : SnapshotPoint

    val private _lineCount : int

    val private _lastLineLength : int

    new (start : SnapshotPoint, lineCount : int, lastLineLength : int) =

        // Don't let the last line of the CharacterSpan end partially into a line 
        // break.  Encompass the entire line break instead 
        let number = start.GetContainingLine().LineNumber + (lineCount - 1)
        let line = SnapshotUtil.GetLineOrLast start.Snapshot number
        let lastLineLength = 
            if line.Length = 0 then
                line.LengthIncludingLineBreak
            elif lastLineLength > line.Length then
                line.LengthIncludingLineBreak
            else
                lastLineLength
        { 
            _start = start
            _lineCount = lineCount
            _lastLineLength = lastLineLength }

    new (span : SnapshotSpan) = 
        let lineCount = SnapshotSpanUtil.GetLineCount span
        let lastLine = SnapshotSpanUtil.GetLastLine span
        let lastLineLength = 
            if lineCount = 1 then
                span.End.Position - span.Start.Position
            else
                let diff = span.End.Position - lastLine.Start.Position
                max 0 diff
        CharacterSpan(span.Start, lineCount, lastLineLength)

    new (startPoint : SnapshotPoint, endPoint: SnapshotPoint) =
        let span = SnapshotSpan(startPoint, endPoint)
        CharacterSpan(span)

    member x.Snapshot = x._start.Snapshot

    member x.StartLine = SnapshotPointUtil.GetContainingLine x.Start

    member x.Start =  x._start

    member x.LineCount = x._lineCount

    member x.LastLineLength = x._lastLineLength

    /// The last line in the CharacterSpan
    member x.LastLine = 
        let number = x.StartLine.LineNumber + (x._lineCount - 1)
        SnapshotUtil.GetLineOrLast x.Snapshot number

    /// The last point included in the CharacterSpan
    member x.Last = 
        let endPoint : SnapshotPoint = x.End
        if endPoint.Position = x.Start.Position then
            None
        else
            SnapshotPoint(x.Snapshot, endPoint.Position - 1) |> Some

    /// Get the End point of the Character Span.
    member x.End =
        let snapshot = x.Snapshot
        let lastLine = x.LastLine
        let offset = 
            if x._lineCount = 1 then
                // For a single line we need to apply the offset past the start point
                SnapshotPointUtil.GetColumn x._start + x._lastLineLength
            else
                x._lastLineLength

        // The original SnapshotSpan could extend into the line break and hence we must
        // consider that here.  The most common case for this occurring is when the caret
        // in visual mode is on the first column of an empty line.  In that case the caret
        // is really in the line break so End is one past that
        let endPoint = SnapshotLineUtil.GetColumnOrEndIncludingLineBreak offset lastLine

        // Make sure that we don't create a negative SnapshotSpan.  Really we should
        // be verifying the arguments to ensure we don't but until we do fix up
        // potential errors here
        if x._start.Position <= endPoint.Position then
            endPoint
        else
            x._start

    member x.Span = SnapshotSpan(x.Start, x.End)

    member x.Length = x.Span.Length

    member x.IncludeLastLineLineBreak = x.End.Position > x.LastLine.End.Position

    member internal x.MaybeAdjustToIncludeLastLineLineBreak() = 
        if x.End = x.LastLine.End then
            let endPoint = x.LastLine.EndIncludingLineBreak
            CharacterSpan(x.Start, endPoint)
        else
            x

    override x.ToString() = x.Span.ToString()

    static member op_Equality(this,other) = System.Collections.Generic.EqualityComparer<CharacterSpan>.Default.Equals(this,other)
    static member op_Inequality(this,other) = not (System.Collections.Generic.EqualityComparer<CharacterSpan>.Default.Equals(this,other))

/// Represents the span for a Visual Block mode selection. 
[<StructuralEquality>]
[<NoComparison>]
[<Struct>]
type BlockSpan =

    val private _startPoint : SnapshotPoint
    val private _tabStop : int
    val private _spaces : int
    val private _height : int

    new (startPoint, tabStop, spaces, height) = 
        { _startPoint = startPoint; _tabStop = tabStop; _spaces = spaces; _height = height }

    /// The SnapshotPoint which begins this BlockSpan
    member x.Start = x._startPoint

    /// In what column does this BlockSpan begin.  This will not calculate tabs as 
    /// spaces.  It just returns the literal column of the start 
    member x.Column = SnapshotPointUtil.GetColumn x.Start

    /// In what space does this BlockSpan begin
    member x.ColumnSpaces = SnapshotPointUtil.GetSpacesToPoint x._startPoint x._tabStop

    /// How many spaces does this BlockSpan occupy?  Be careful to treat this value as spaces, not columns.  The
    /// different being that tabs count as 'tabStop' spaces but only 1 column.  
    member x.Spaces = x._spaces

    /// How many lines does this BlockSpan encompass
    member x.Height = x._height

    member x.OverlapEnd = 
        let line = 
            let lineNumber = SnapshotPointUtil.GetLineNumber x.Start
            SnapshotUtil.GetLineOrLast x.Snasphot (lineNumber + (x._height - 1))
        let spaces = x.ColumnSpaces + x.Spaces
        SnapshotLineUtil.GetSpaceWithOverlapOrEnd line spaces x._tabStop

    /// Get the EndPoint (exclusive) of the BlockSpan
    member x.End = 
        let point = x.OverlapEnd
        if point.SpacesBefore > 0 then
            SnapshotPointUtil.AddOneOrCurrent point.Point
        else
            point.Point

    /// What is the tab stop this BlockSpan is created off of
    member x.TabStop = x._tabStop

    member x.Snasphot = x.Start.Snapshot

    member x.TextBuffer =  x.Start.Snapshot.TextBuffer

    /// Get the NonEmptyCollection<SnapshotSpan> for the given block information
    member x.BlockSpans : NonEmptyCollection<SnapshotSpan> =
        let snapshot = SnapshotPointUtil.GetSnapshot x.Start
        let offset = x.ColumnSpaces
        let lineNumber = SnapshotPointUtil.GetLineNumber x.Start
        let list = System.Collections.Generic.List<SnapshotSpan>()
        for i = lineNumber to ((x._height - 1) + lineNumber) do
            match SnapshotUtil.TryGetLine snapshot i with
            | None -> ()
            | Some line -> 
                let startPoint = SnapshotLineUtil.GetSpaceOrEnd line offset x._tabStop
                let endPoint = SnapshotLineUtil.GetSpaceOrEnd line (offset + x.Spaces) x._tabStop
                let span = SnapshotSpan(startPoint, endPoint)
                list.Add(span)

        list
        |> NonEmptyCollectionUtil.OfSeq 
        |> Option.get

    /// Get a NonEmptyCollection indicating of the SnapshotSpan that each line of
    /// this block spans, along with the offset (measured in cells) of the block
    /// with respect to the start point and end point.
    member x.BlockOverlapSpans : NonEmptyCollection<SnapshotOverlapSpan> =
        let snapshot = SnapshotPointUtil.GetSnapshot x.Start
        let offset = x.ColumnSpaces
        let lineNumber = SnapshotPointUtil.GetLineNumber x.Start
        let list = System.Collections.Generic.List<SnapshotOverlapSpan>()
        for i = lineNumber to ((x._height - 1) + lineNumber) do
            match SnapshotUtil.TryGetLine snapshot i with
            | None -> ()
            | Some line -> 
                let startPoint = SnapshotLineUtil.GetSpaceWithOverlapOrEnd line offset x._tabStop
                let endPoint = SnapshotLineUtil.GetSpaceWithOverlapOrEnd line (offset + x.Spaces) x._tabStop 
                let span = SnapshotOverlapSpan(startPoint, endPoint)
                list.Add(span)

        list
        |> NonEmptyCollectionUtil.OfSeq 
        |> Option.get

    override x.ToString() =
        sprintf "Point: %s Spaces: %d Height: %d" (x.Start.ToString()) x._spaces x._height

    static member op_Equality(this,other) = System.Collections.Generic.EqualityComparer<BlockSpan>.Default.Equals(this,other)
    static member op_Inequality(this,other) = not (System.Collections.Generic.EqualityComparer<BlockSpan>.Default.Equals(this,other))

    /// Create a BlockSpan for the given SnapshotSpan.  The returned BlockSpan will have a minimum of 1 for
    /// height and width.  The start of the BlockSpan is not necessarily the Start of the SnapshotSpan
    /// as an End column which occurs before the start could cause the BlockSpan start to be before the 
    /// SnapshotSpan start
    static member CreateForSpan (span : SnapshotSpan) tabStop =
        let startPoint, width = 
            let startColumnSpaces = SnapshotPointUtil.GetSpacesToPoint span.Start tabStop
            let endColumnSpaces = SnapshotPointUtil.GetSpacesToPoint span.End tabStop
            let width = endColumnSpaces - startColumnSpaces 

            if width = 0 then
                span.Start, 1
            elif width > 0 then
                span.Start, width
            else 
                let startLine = SnapshotPointUtil.GetContainingLine span.Start
                let start = SnapshotLineUtil.GetColumnOrEnd endColumnSpaces startLine
                let width = abs width
                start, width

        let height = SnapshotSpanUtil.GetLineCount span
        BlockSpan(startPoint, tabStop, width, height)

[<RequireQualifiedAccess>]
[<NoComparison>]
type BlockCaretLocation =
    | TopLeft
    | TopRight
    | BottomLeft
    | BottomRight

/// Represents a visual span of text in the form Vim understands.  This type understands 
/// nothing about the intricacies of Visual Mode selection.  It simply understands how
/// to represent the Spans it can occupy.
///
/// Note: There is no use of inclusive or exclusive in this type.  That is intentional.  This
/// type is simply a measurement.  The context in which it was measured is important for
/// the types which care about the context
[<RequireQualifiedAccess>]
[<StructuralEquality>]
[<NoComparison>]
[<DebuggerDisplay("{ToString()}")>]
type VisualSpan =

    /// A character wise span.  The 'End' of the span is not selected.
    | Character of CharacterSpan

    /// A line wise span
    | Line of SnapshotLineRange

    /// A block span.  
    | Block of BlockSpan

    with

    /// Return the Spans which make up this VisualSpan instance
    member x.Spans = 
        match x with 
        | VisualSpan.Character characterSpan -> [characterSpan.Span] |> Seq.ofList
        | VisualSpan.Line range -> [range.ExtentIncludingLineBreak] |> Seq.ofList
        | VisualSpan.Block blockSpan -> blockSpan.BlockSpans :> SnapshotSpan seq

    member x.OverlapSpans =
        match x with 
        | VisualSpan.Block blockSpan -> seq (blockSpan.BlockOverlapSpans)
        | _ -> x.Spans |> Seq.map (fun span -> SnapshotOverlapSpan(span))

    /// Returns the EditSpan for this VisualSpan
    member x.EditSpan = 
        match x with
        | VisualSpan.Character characterSpan -> EditSpan.Single characterSpan.Span
        | VisualSpan.Line range -> EditSpan.Single range.ExtentIncludingLineBreak
        | VisualSpan.Block blockSpan -> EditSpan.Block blockSpan.BlockOverlapSpans

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

    /// Get the end of the Visual Span
    member x.End = 
        match x with
        | VisualSpan.Character characterSpan -> characterSpan.End
        | VisualSpan.Line lineRange -> lineRange.End
        | VisualSpan.Block blockSpan -> blockSpan.End

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

    /// VisualKind of the VisualSpan
    member x.VisualKind = 
        match x with
        | VisualSpan.Character _ -> VisualKind.Character
        | VisualSpan.Block _ -> VisualKind.Block
        | VisualSpan.Line _ -> VisualKind.Line

    member x.AdjustForExtendIntoLineBreak extendIntoLineBreak = 
        if extendIntoLineBreak then
            match x with
            | Character characterSpan -> characterSpan.MaybeAdjustToIncludeLastLineLineBreak() |> VisualSpan.Character
            | Line _ -> x
            | Block _ -> x
        else
            x

    /// Select the given VisualSpan in the ITextView
    member x.Select (textView : ITextView) path =

        // Select the given SnapshotSpan
        let selectSpan startPoint endPoint = 

            textView.Selection.Mode <- TextSelectionMode.Stream

            let startPoint, endPoint = 
                match path with
                | Path.Forward -> startPoint, endPoint 
                | Path.Backward -> endPoint, startPoint

            // The editor will normalize SnapshotSpan values here which extend into the line break
            // portion of the line to not include the line break.  Must use VirtualSnapshotPoint 
            // values to ensure the proper selection
            let startPoint = startPoint |> VirtualSnapshotPointUtil.OfPointConsiderLineBreak
            let endPoint = endPoint |> VirtualSnapshotPointUtil.OfPointConsiderLineBreak

            textView.Selection.Select(startPoint, endPoint);

        match x with
        | Character characterSpan ->
            let endPoint = 
                if characterSpan.IncludeLastLineLineBreak then
                    SnapshotPointUtil.AddOneOrCurrent characterSpan.LastLine.End
                else
                    characterSpan.End
            selectSpan characterSpan.Start endPoint
        | Line lineRange ->
            selectSpan lineRange.Start lineRange.EndIncludingLineBreak
        | Block blockSpan ->
            // In general the Start and End accurately represent the selection points.  The cases where
            // it doesn't is when the caret ends inside of a point when spaces are considered. The
            // clearest example of this is when the caret is inside a tab (assume tabstop = 4)
            //
            //  truck
            //  \t  cat
            //
            // In this example if the anchor point is 'r' and the caret is under 'u' (2 space selection) then 
            // the block span of the second line is the 2nd and 3rd space.  The tab itself is a single 
            // SnapshotPoint numbering 4 spaces.  Hence the caret will be in the middle of a SnapshotPoint
            //
            // When this happens the anchor point is reset to min of the column of the SnapshotPoint the
            // caret resides in or the column of the anchor point
            let endLine = blockSpan.End.GetContainingLine()
            let endPoint = blockSpan.OverlapEnd
            let startPoint =
                if endPoint.SpacesBefore > 0 then
                    let startColumn = SnapshotPointUtil.GetColumn blockSpan.Start
                    let endColumn = SnapshotPointUtil.GetColumn endPoint.Point
                    let column = min startColumn endColumn
                    let startLine = blockSpan.Start.GetContainingLine()
                    SnapshotLineUtil.GetColumnOrEnd column startLine
                else
                    blockSpan.Start

            // When calculating the actual columns to put the selection on here we need to consider
            // all tabs as the equivalent number of spaces 
        
            textView.Selection.Mode <- TextSelectionMode.Box
            textView.Selection.Select(
                VirtualSnapshotPoint(startPoint),
                VirtualSnapshotPoint(blockSpan.End))

    override x.ToString() =
        match x with
        | VisualSpan.Character characterSpan -> sprintf "Character: %O" characterSpan
        | VisualSpan.Line lineRange -> sprintf "Line: %O" lineRange
        | VisualSpan.Block blockSpan -> sprintf "Block: %O" blockSpan

    /// Create the VisualSpan based on the specified points.  The activePoint is assumed
    /// to be the end of the selection and hence not included (exclusive) just as it is 
    /// in ITextSelection
    static member CreateForSelectionPoints visualKind (anchorPoint : SnapshotPoint) (activePoint : SnapshotPoint) tabStop =

        match visualKind with
        | VisualKind.Character ->
            let startPoint, endPoint = SnapshotPointUtil.OrderAscending anchorPoint activePoint
            let characterSpan = CharacterSpan(startPoint, endPoint)
            Character characterSpan
        | VisualKind.Line ->

            let startPoint, endPoint = SnapshotPointUtil.OrderAscending anchorPoint activePoint
            let startLine = SnapshotPointUtil.GetContainingLine startPoint

            // If endPoint is EndIncludingLineBreak we would get the line after and be 
            // one line too big.  Go back on point to ensure we don't expand the span
            let endLine = 
                if startPoint = endPoint then
                    startLine
                else
                    let endPoint = SnapshotPointUtil.SubtractOneOrCurrent endPoint
                    SnapshotPointUtil.GetContainingLine endPoint
            SnapshotLineRangeUtil.CreateForLineRange startLine endLine |> Line

        | VisualKind.Block -> 
            let startPoint, endPoint = SnapshotPointUtil.OrderAscending anchorPoint activePoint
            let span = SnapshotSpan(startPoint, endPoint)
            BlockSpan.CreateForSpan span tabStop |> Block

    /// Create a VisualSelection based off of the current selection.  If no selection is present
    /// then an empty VisualSpan will be created at the caret
    static member CreateForSelection (textView : ITextView) visualKind tabStop =
        let selection = textView.Selection
        if selection.IsEmpty then
            let caretPoint = TextViewUtil.GetCaretPoint textView
            VisualSpan.CreateForSelectionPoints visualKind caretPoint caretPoint tabStop
        else
            let anchorPoint = selection.AnchorPoint.Position
            let activePoint = selection.ActivePoint.Position 

            // Need to special case the selection ending in, and encompassing, an empty line.  Once you 
            // get rid of the virtual points here it's impossible to distinguish from the case where the 
            // selection ends in the line above instead.  
            if visualKind = VisualKind.Character && selection.End.Position.GetContainingLine().Length = 0 then
                let endPoint = SnapshotPointUtil.AddOneOrCurrent selection.End.Position 
                let characterSpan = CharacterSpan(selection.Start.Position, endPoint)
                Character characterSpan
            else
                let visualSpan = VisualSpan.CreateForSelectionPoints visualKind anchorPoint activePoint tabStop
                visualSpan.AdjustForExtendIntoLineBreak selection.End.IsInVirtualSpace

    static member CreateForSpan (span : SnapshotSpan) visualKind tabStop =
        match visualKind with
        | VisualKind.Character -> CharacterSpan(span) |> Character
        | VisualKind.Line -> span |> SnapshotLineRangeUtil.CreateForSpan |> Line
        | VisualKind.Block -> BlockSpan.CreateForSpan span tabStop |> Block

/// Represents the information for a visual mode selection.  All of the values are expressed
/// in terms of an inclusive selection.
///
/// Note: It's intentional that inclusive and exclusive are not included in this particular
/// structure.  Whether or not the selection is inclusive or exclusive doesn't change the 
/// anchor / caret point.  It just changes what is operated on and what is actually 
/// physically selected by visual mode
[<RequireQualifiedAccess>] 
[<StructuralEquality>]
[<NoComparison>] 
type VisualSelection =

    /// The underlying span and whether or not this is a forward looking span.  
    | Character of CharacterSpan * Path

    /// The underlying range, whether or not is forwards or backwards and the int 
    /// is which column in the range the caret should be placed in
    | Line of SnapshotLineRange * Path * int 

    /// Just keep the BlockSpan and the caret information for the block
    | Block of BlockSpan * BlockCaretLocation

    with

    member x.IsCharacterForward =
        match x with
        | Character (_, path) -> path.IsPathForward
        | _ -> false

    member x.IsLineForward = 
        match x with
        | Line (_, path, _) -> path.IsPathForward
        | _ -> false

    member x.VisualKind = 
        match x with
        | Character _ -> VisualKind.Character
        | Line _ -> VisualKind.Line
        | Block _ -> VisualKind.Block

    /// The underlying VisualSpan
    member x.VisualSpan =
        match x with 
        | Character (characterSpan, _) -> VisualSpan.Character characterSpan
        | Line (lineRange, _, _) -> VisualSpan.Line lineRange
        | Block (blockSpan, _) -> VisualSpan.Block blockSpan

    member x.AdjustForExtendIntoLineBreak extendIntoLineBreak = 
        if extendIntoLineBreak then
            match x with
            | Character (characterSpan, path) -> 
                let characterSpan = characterSpan.MaybeAdjustToIncludeLastLineLineBreak()
                VisualSelection.Character (characterSpan, path)
            | Line _ -> x
            | Block _ -> x
        else
            x

    /// Get the VisualSelection information adjusted for the given selection kind.  This is only useful 
    /// when a VisualSelection is created from the caret position and the selection needs to be adjusted
    /// to include or exclude the caret.  
    /// 
    /// It's incorrect to use when creating from an actual physical selection
    member x.AdjustForSelectionKind selectionKind = 
        match selectionKind with
        | SelectionKind.Inclusive -> x
        | SelectionKind.Exclusive -> 
            match x with
            | Character (characterSpan, path) -> 
                // The span decreases by a single character in exclusive 
                let endPoint = characterSpan.Last |> OptionUtil.getOrDefault characterSpan.Start
                let characterSpan = CharacterSpan(SnapshotSpan(characterSpan.Start, endPoint))
                VisualSelection.Character (characterSpan, path)
            | Line _ ->
                // The span isn't effected
                x
            | Block (blockSpan, blockCaretLocation) -> 
                // The width of a block span decreases by 1 in exclusive.  The minimum though
                // is still 1
                let width = max (blockSpan.Spaces - 1) 1
                let blockSpan = BlockSpan(blockSpan.Start, blockSpan.TabStop, width, blockSpan.Height)
                VisualSelection.Block (blockSpan, blockCaretLocation)

    /// Gets the SnapshotPoint for the caret as it should appear in the given VisualSelection with the 
    /// specified SelectionKind.  
    member x.GetCaretPoint selectionKind = 

        let getAdjustedEnd (span : SnapshotSpan) = 
            if span.Length = 0 then
                span.Start
            else
                match selectionKind with
                | SelectionKind.Exclusive -> span.End
                | SelectionKind.Inclusive -> 
                    if span.Length > 0 then
                        SnapshotPointUtil.SubtractOne span.End
                    else
                        span.End

        match x with
        | Character (characterSpan, path) ->
            // The caret is either positioned at the start or the end of the selected
            // SnapshotSpan
            match path with
            | Path.Forward ->
                if characterSpan.LastLine.Length = 0 then
                    // Need to special case the empty last line because there is no character which
                    // isn't inside the line break here.  Just return the start as the caret position
                    characterSpan.LastLine.Start
                else
                    getAdjustedEnd characterSpan.Span
            | Path.Backward -> characterSpan.Start

        | Line (snapshotLineRange, path, column) ->

            // The caret is either positioned at the start or the end of the selected range
            // and can be on any column in either
            let line = 
                if path.IsPathForward then
                    snapshotLineRange.LastLine
                else
                    snapshotLineRange.StartLine

            if column <= line.LengthIncludingLineBreak then
                SnapshotPointUtil.Add column line.Start
            else
                line.End

        | Block (blockSpan, blockCaretLocation) ->

            match blockCaretLocation with
            | BlockCaretLocation.TopLeft -> blockSpan.Start
            | BlockCaretLocation.TopRight -> getAdjustedEnd blockSpan.BlockSpans.Head
            | BlockCaretLocation.BottomLeft -> blockSpan.BlockSpans |> SeqUtil.last |> SnapshotSpanUtil.GetStartPoint
            | BlockCaretLocation.BottomRight -> blockSpan.BlockSpans |> SeqUtil.last |> getAdjustedEnd


    /// Select the given VisualSpan in the ITextView
    member x.Select (textView : ITextView) =
        let path =
            match x with
            | Character (_, path) -> path
            | Line (_, path, _) -> path
            | Block _ -> Path.Forward
        x.VisualSpan.Select textView path

    /// Create for the given VisualSpan.  Assumes this was a forward created VisualSpan
    static member CreateForward visualSpan = 
        match visualSpan with
        | VisualSpan.Character span -> 
            VisualSelection.Character (span, Path.Forward)
        | VisualSpan.Line lineRange ->
            let column = SnapshotPointUtil.GetColumn lineRange.LastLine.End
            VisualSelection.Line (lineRange, Path.Forward, column)
        | VisualSpan.Block blockSpan ->
            VisualSelection.Block (blockSpan, BlockCaretLocation.BottomRight)

    /// Create the VisualSelection over the VisualSpan with the specified caret location
    static member Create (visualSpan : VisualSpan) path (caretPoint : SnapshotPoint) =
        match visualSpan with
        | VisualSpan.Character characterSpan ->
            Character (characterSpan, path)
        | VisualSpan.Line lineRange ->
            let column = SnapshotPointUtil.GetColumn caretPoint
            Line (lineRange, path, column)

        | VisualSpan.Block blockSpan ->

            // Need to calculate the caret location.  Do this based on the initial anchor and
            // caret locations
            let blockCaretLocation = 
                let startLine, startColumn = SnapshotPointUtil.GetLineColumn blockSpan.Start
                let caretLine, caretColumn = SnapshotPointUtil.GetLineColumn caretPoint
                match caretLine > startLine, caretColumn > startColumn with
                | true, true -> BlockCaretLocation.BottomRight
                | true, false -> BlockCaretLocation.BottomLeft
                | false, true -> BlockCaretLocation.TopRight
                | false, false -> BlockCaretLocation.TopLeft

            Block (blockSpan, blockCaretLocation)

    /// Create a VisualSelection for the given anchor point and caret.  The position, anchorPoint or 
    /// caretPoint, which is greater position wise is the last point included in the selection.  It
    /// is inclusive
    static member CreateForPoints visualKind (anchorPoint : SnapshotPoint) (caretPoint : SnapshotPoint) tabStop =

        let createBlock () =
            let anchorSpaces = SnapshotPointUtil.GetSpacesToPoint anchorPoint tabStop
            let caretSpaces = SnapshotPointUtil.GetSpacesToPoint caretPoint tabStop
            let spaces = (abs (caretSpaces - anchorSpaces)) + 1
            let column = min anchorSpaces caretSpaces
            
            let startPoint = 
                let first, _ = SnapshotPointUtil.OrderAscending anchorPoint caretPoint
                let line = SnapshotPointUtil.GetContainingLine first
                SnapshotLineUtil.GetSpaceOrEnd line column tabStop

            let height = 
                let anchorLine = anchorPoint.GetContainingLine()
                let caretLine = caretPoint.GetContainingLine()
                (abs (anchorLine.LineNumber - caretLine.LineNumber)) + 1

            let path = 
                if anchorSpaces <= caretSpaces then Path.Forward
                else Path.Backward

            let blockSpan = BlockSpan(startPoint, tabStop, spaces, height)
            VisualSpan.Block blockSpan, path

        let createNormal () = 

            let isForward = anchorPoint.Position <= caretPoint.Position
            let anchorPoint, activePoint = 
                if isForward then
                    let activePoint = SnapshotPointUtil.AddOneOrCurrent caretPoint
                    anchorPoint, activePoint
                else
                    let activePoint = SnapshotPointUtil.AddOneOrCurrent anchorPoint
                    caretPoint, activePoint

            let path = Path.Create isForward
            VisualSpan.CreateForSelectionPoints visualKind anchorPoint activePoint tabStop, path

        let visualSpan, path = 
            match visualKind with
            | VisualKind.Block -> createBlock ()
            | VisualKind.Line -> createNormal ()
            | VisualKind.Character -> createNormal ()

        VisualSelection.Create visualSpan path caretPoint

    /// Create a VisualSelection based off of the current selection and position of the caret.  The
    /// SelectionKind should specify what the current mode is (or the mode which produced the 
    /// active ITextSelection)
    static member CreateForSelection (textView : ITextView) visualKind selectionKind tabStop =
        let caretPoint = TextViewUtil.GetCaretPoint textView
        let visualSpan = VisualSpan.CreateForSelection textView visualKind tabStop

        // Get the proper VisualSpan based off of the way in which it was created.  VisualSelection
        // represents all values internally as inclusive
        let visualSpan = 
            match selectionKind with
            | SelectionKind.Inclusive -> visualSpan
            | SelectionKind.Exclusive ->
                match visualSpan with
                | VisualSpan.Character characterSpan ->
                    let endPoint = SnapshotPointUtil.AddOneOrCurrent characterSpan.End
                    CharacterSpan(characterSpan.Start, endPoint) |> VisualSpan.Character
                | VisualSpan.Line _ -> visualSpan
                | VisualSpan.Block blockSpan ->
                    let width = blockSpan.Spaces + 1
                    let blockSpan = BlockSpan(blockSpan.Start, blockSpan.TabStop, width, blockSpan.Height)
                    VisualSpan.Block blockSpan

        let path = 
            if textView.Selection.IsReversed then
                Path.Backward
            else
                Path.Forward

        let caretPoint = TextViewUtil.GetCaretPoint textView
        VisualSelection.Create visualSpan path caretPoint 

    /// Create the initial Visual Selection information for the specified Kind started at 
    /// the specified point
    static member CreateInitial visualKind caretPoint tabStop =
        match visualKind with
        | VisualKind.Character ->
            let characterSpan = 
                let endPoint = SnapshotPointUtil.AddOneOrCurrent caretPoint
                CharacterSpan(caretPoint, endPoint)
            VisualSelection.Character (characterSpan, Path.Forward)
        | VisualKind.Line ->
            let lineRange = 
                let line = SnapshotPointUtil.GetContainingLine caretPoint
                SnapshotLineRangeUtil.CreateForLine line
            let column = SnapshotPointUtil.GetColumn caretPoint
            VisualSelection.Line (lineRange, Path.Forward, column)
        | VisualKind.Block ->
            let blockSpan = BlockSpan(caretPoint, tabStop, 1, 1)
            VisualSelection.Block (blockSpan, BlockCaretLocation.BottomRight)

/// Most text object entries have specific effects on Visual Mode.  They are 
/// described below
[<RequireQualifiedAccess>]
[<NoComparison>]
type TextObjectKind = 
    | None
    | LineToCharacter
    | AlwaysCharacter
    | AlwaysLine

[<RequireQualifiedAccess>]
type ModeArgument =
    | None

    /// Used for transitions from Visual Mode directly to Command mode
    | FromVisual 

    /// Passed to visual mode to indicate what the initial selection should be.  The SnapshotPoint
    /// option provided is meant to be the initial caret point.  If not provided the actual 
    /// caret point is used
    | InitialVisualSelection of VisualSelection * SnapshotPoint option

    /// Begins a block insertion.  This can possibly have a linked undo transaction that needs
    /// to be carried forward through the insert
    | InsertBlock of BlockSpan * ILinkedUndoTransaction

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

    /// Passing the substitute to confirm to Confirm mode.  The SnapshotSpan is the first
    /// match to process and the range is the full range to consider for a replace
    | Substitute of SnapshotSpan * SnapshotLineRange * SubstituteData

[<RequireQualifiedAccess>]
[<NoComparison>]
[<NoEquality>]
type ModeSwitch =
    | NoSwitch
    | SwitchMode of ModeKind
    | SwitchModeWithArgument of ModeKind * ModeArgument
    | SwitchPreviousMode 

    /// Switch to the given mode for a single command.  After the command is processed switch
    /// back to the original mode
    | SwitchModeOneTimeCommand

[<RequireQualifiedAccess>]
[<NoComparison>]
type CommandResult =   

    /// The command completed and requested a switch to the provided Mode which 
    /// may just be a no-op
    | Completed  of ModeSwitch

    /// An error was encountered and the command was unable to run.  If this is encountered
    /// during a macro run it will cause the macro to stop executing
    | Error

/// Information about the attributes of Command
[<System.Flags>]
type CommandFlags =
    | None = 0x0000

    /// Relates to the movement of the cursor.  A movement command does not alter the 
    /// last command
    | Movement = 0x0001

    /// A Command which can be repeated
    | Repeatable = 0x0002

    /// A Command which should not be considered when looking at last changes
    | Special = 0x0004

    /// Can handle the escape key if provided as part of a Motion or Long command extra
    /// input
    | HandlesEscape = 0x0008

    /// For the purposes of change repeating the command is linked with the following
    /// text change
    | LinkedWithNextCommand = 0x0010

    /// For the purposes of change repeating the command is linked with the previous
    /// text change if it exists
    | LinkedWithPreviousCommand = 0x0020

    /// For Visual mode commands which should reset the cursor to the original point
    /// after completing
    | ResetCaret = 0x0040

    /// For Visual mode commands which should reset the anchor point to the current
    /// anchor point of the selection
    | ResetAnchorPoint = 0x0080

    /// Vim allows for special handling of the 'd' command in normal mode such that it can
    /// have the pattern 'd#d'.  This flag is used to tag the 'd' command to allow such
    /// a pattern
    | Delete = 0x0100

    /// Vim allows for special handling of the 'y' command in normal mode such that it can
    /// have the pattern 'y#y'.  This flag is used to tag the 'y' command to allow such
    /// a pattern
    | Yank = 0x0200

    /// Vim allows for special handling of the '>' command in normal mode such that it can
    /// have the pattern '>#>'.  This flag is used to tag the '>' command to allow such
    /// a pattern
    | ShiftRight = 0x0400

    /// Vim allows for special handling of the '<' command in normal mode such that it can
    /// have the pattern '<#<'.  This flag is used to tag the '<' command to allow such
    /// a pattern
    | ShiftLeft = 0x0800

    /// Vim allows for special handling of the 'c' command in normal mode such that it can
    /// have the pattern 'c#c'.  This flag is used to tag the 'c' command to allow such
    /// a pattern
    | Change = 0x1000

    /// Represents an insert edit action which can be linked with other insert edit actions and
    /// hence acts with them in a repeat
    | InsertEdit = 0x2000

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
    member x.CountOrDefault = Util.CountOrDefault x.Count

    static member Default = { Count = None; RegisterName = None }

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
[<NoComparison>]
[<StructuralEquality>]
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

    /// Close all folds in the buffer
    | CloseAllFolds

    /// Close all folds under the caret
    | CloseAllFoldsUnderCaret

    /// Close the IVimBuffer and don't bother to save
    | CloseBuffer

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
    | JumpToMark of Mark

    /// Jump to the start of the line for the specified mark
    | JumpToMarkLine of Mark

    /// Jump to the next older item in the tag list
    | JumpToOlderPosition

    /// Jump to the next new item in the tag list
    | JumpToNewerPosition

    /// Move the caret to the result of the given Motion.
    | MoveCaretToMotion of Motion

    /// Undo count operations in the ITextBuffer
    | Undo

    /// Open all folds in the buffer
    | OpenAllFolds

    /// Open all of the folds under the caret
    | OpenAllFoldsUnderCaret

    /// Open a fold under the caret
    | OpenFoldUnderCaret

    /// Toggle a fold under the caret
    | ToggleFoldUnderCaret

    /// Toggle all folds under the caret
    | ToggleAllFolds

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

    /// Scroll the caret in the specified direciton.  The bool is whether to use
    /// the 'scroll' option or 'count'
    | ScrollLines of ScrollDirection * bool

    /// Move the display a single page in the specified direction
    | ScrollPages of ScrollDirection

    /// Scroll the window in the specified direction by 'count' lines
    | ScrollWindow of ScrollDirection

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

    /// Switch to a selection dictated by the given caret movement
    | SwitchToSelection of CaretMovement

    /// Write out the ITextBuffer and quit
    | WriteBufferAndQuit

    /// Yank the given motion into a register
    | Yank of MotionData

    /// Yank the specified number of lines
    | YankLines

/// Visual mode commands which can be executed by the user 
[<RequireQualifiedAccess>]
[<NoComparison>]
[<StructuralEquality>]
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

    /// Invert the selection by swapping the caret and anchor points.  When true it means that block mode should
    /// be special cased to invert the column only 
    | InvertSelection of bool

    /// Move the caret to the result of the given Motion.  This movement is from a 
    /// text-object selection.  Certain motions 
    | MoveCaretToTextObject of Motion * TextObjectKind

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

    /// Switch the mode to insert and possibly a block insert
    | SwitchModeInsert

    /// Switch to the previous mode
    | SwitchModePrevious

    /// Switch to the specified visual mode
    | SwitchModeVisual of VisualKind

    /// Toggle one fold in the selection
    | ToggleFoldInSelection

    /// Toggle all folds in the selection
    | ToggleAllFoldsInSelection

    /// Yank the lines which are specified by the selection
    | YankLineSelection

    /// Yank the selection into the specified register
    | YankSelection

/// Insert mode commands that can be executed by the user
[<RequireQualifiedAccess>]
[<NoComparison>]
[<StructuralEquality>]
type InsertCommand  =

    /// Backspace at the current caret position
    | Back

    /// Block edit of the specified TextChange value.  The int represents the number of 
    /// lines on which this block insert should take place
    | BlockInsert of string * int

    /// This is an insert command which is a combination of other insert commands
    | Combined of InsertCommand * InsertCommand

    /// Complete the Insert Mode session.  This is done as a command so that it will 
    /// be a bookend of insert mode for the repeat infrastructure
    ///
    /// The bool value represents whether or not the caret needs to be moved to the
    /// left
    | CompleteMode of bool

    /// Delete the character under the caret
    | Delete

    /// Delete count characters to the left of the caret
    | DeleteLeft of int 

    /// Delete count characters to the right of the caret
    | DeleteRight of int

    /// Delete all indentation on the current line
    | DeleteAllIndent

    /// Delete the word before the cursor
    | DeleteWordBeforeCursor

    /// Direct insert of the specified char
    | DirectInsert of char

    /// Direct replacement of the specified char
    | DirectReplace of char

    /// Insert the character which is immediately above the caret
    | InsertCharacterAboveCaret

    /// Insert the character which is immediately below the caret
    | InsertCharacterBelowCaret

    /// Insert a new line into the ITextBuffer
    | InsertNewLine

    /// Insert a tab into the ITextBuffer
    | InsertTab

    /// Insert the specified text into the ITextBuffer
    | InsertText of string

    /// Move the caret in the given direction
    | MoveCaret of Direction

    /// Move the caret in the given direction with an arrow key
    | MoveCaretWithArrow of Direction

    /// Move the caret in the given direction by a whole word
    | MoveCaretByWord of Direction

    /// Shift the current line one indent width to the left
    | ShiftLineLeft 

    /// Shift the current line one indent width to the right
    | ShiftLineRight

    with

    /// Convert a TextChange value into the appropriate InsertCommand structure
    static member OfTextChange textChange = 
        match textChange with
        | TextChange.Insert text -> InsertCommand.InsertText text
        | TextChange.DeleteLeft count -> InsertCommand.DeleteLeft count
        | TextChange.DeleteRight count -> InsertCommand.DeleteRight count
        | TextChange.Combination (left, right) ->
            let leftCommand = InsertCommand.OfTextChange left
            let rightCommand = InsertCommand.OfTextChange right
            InsertCommand.Combined (leftCommand, rightCommand)

    /// Convert this InsertCommand to a TextChange object
    member x.TextChange editorOptions = 
        match x with 
        | InsertCommand.Back ->  Some (TextChange.DeleteLeft 1)
        | InsertCommand.BlockInsert _ -> None
        | InsertCommand.Combined (left, right) -> 
            match left.TextChange editorOptions, right.TextChange editorOptions with
            | Some l, Some r -> TextChange.Combination (l, r) |> Some
            | _ -> None
        | InsertCommand.CompleteMode _ -> None
        | InsertCommand.Delete -> Some (TextChange.DeleteRight 1)
        | InsertCommand.DeleteLeft count -> Some (TextChange.DeleteLeft count)
        | InsertCommand.DeleteRight count -> Some (TextChange.DeleteRight count)
        | InsertCommand.DeleteAllIndent -> None
        | InsertCommand.DeleteWordBeforeCursor -> None
        | InsertCommand.DirectInsert c -> Some (TextChange.Insert (c.ToString()))
        | InsertCommand.DirectReplace c -> Some (TextChange.Combination ((TextChange.DeleteRight 1), (TextChange.Insert (c.ToString()))))
        | InsertCommand.InsertCharacterAboveCaret -> None
        | InsertCommand.InsertCharacterBelowCaret -> None
        | InsertCommand.InsertNewLine -> Some (TextChange.Insert (EditUtil.NewLine editorOptions))
        | InsertCommand.InsertTab -> Some (TextChange.Insert "\t")
        | InsertCommand.InsertText text -> Some (TextChange.Insert text)
        | InsertCommand.MoveCaret _ -> None
        | InsertCommand.MoveCaretWithArrow _ -> None
        | InsertCommand.MoveCaretByWord _ -> None
        | InsertCommand.ShiftLineLeft -> None
        | InsertCommand.ShiftLineRight -> None

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
    member x.Map (mapFunc : 'T -> BindResult<'U>) : BindResult<'U> =
        match x with
        | Complete value -> mapFunc value 
        | NeedMoreInput (bindData : BindData<'T>) -> NeedMoreInput (bindData.Map mapFunc)
        | Error -> Error
        | Cancelled -> Cancelled

    /// Used to convert a BindResult<'T>.Completed to BindResult<'U>.Completed through a conversion
    /// function
    member x.Convert (convertFunc : 'T -> 'U) : BindResult<'U> = 
        x.Map (fun value -> convertFunc value |> BindResult.Complete)

and BindData<'T> = {

    /// The optional KeyRemapMode which should be used when binding
    /// the next KeyInput in the sequence
    KeyRemapMode : KeyRemapMode 

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
        { KeyRemapMode = KeyRemapMode.None; BindFunction = bindFunc }

    /// Very similar to the Convert function.  This will instead map a BindData<'T>.Completed
    /// to a BindData<'U> of any form 
    member x.Map<'U> (mapFunc : 'T -> BindResult<'U>) : BindData<'U> = 

        let rec inner bindFunction keyInput = 
            match x.BindFunction keyInput with
            | BindResult.Cancelled -> BindResult.Cancelled
            | BindResult.Complete value -> mapFunc value
            | BindResult.Error -> BindResult.Error
            | BindResult.NeedMoreInput bindData -> BindResult.NeedMoreInput (bindData.Map mapFunc)

        { KeyRemapMode = x.KeyRemapMode; BindFunction = inner x.BindFunction }

    /// Often types bindings need to compose together because we need an inner binding
    /// to succeed so we can create a projected value.  This function will allow us
    /// to translate a BindResult<'T>.Completed -> BindResult<'U>.Completed
    member x.Convert (convertFunc : 'T -> 'U) : BindData<'U> = 
        x.Map (fun value -> convertFunc value |> BindResult.Complete)

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
    abstract RunNormalCommand : command : NormalCommand -> commandData : CommandData -> CommandResult

    /// Run a visual command
    abstract RunVisualCommand : command : VisualCommand -> commandData: CommandData -> visualSpan : VisualSpan -> CommandResult

    /// Run a insert command
    abstract RunInsertCommand : command : InsertCommand -> CommandResult

    /// Run a command
    abstract RunCommand : command : Command -> CommandResult

type internal IInsertUtil = 

    /// Run a insert command
    abstract RunInsertCommand : InsertCommand -> CommandResult

    /// Repeat the given edit series. 
    abstract RepeatEdit : textChange : TextChange -> addNewLines : bool -> count : int -> unit

    /// Repeat the given edit series. 
    abstract RepeatBlock : command : InsertCommand -> blockSpan : BlockSpan -> string option

/// Contains the stored information about a Visual Span.  This instance *will* be 
/// stored for long periods of time and used to repeat a Command instance across
/// multiple IVimBuffer instances so it must be buffer agnostic
[<RequireQualifiedAccess>]
type StoredVisualSpan = 

    /// Storing a character wise span.  Need to know the line count and the offset 
    /// in the last line for the end.  
    | Character of int * int

    /// Storing a line wise span just stores the count of lines
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
            StoredVisualSpan.Block (blockSpan.Spaces, blockSpan.Height)

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

    /// This type of motion can be used to move the caret
    | CaretMovement = 0x1 

    /// The motion function wants to specially handle the esape function.  This is used 
    /// on Complex motions such as / and ? 
    | HandlesEscape = 0x2

    /// Text object selection motions.  These can be used for cursor movement inside of 
    /// Visual Mode but otherwise need to be used only after operators.  
    /// :help text-objects
    | TextObject = 0x4

    /// Text object with line to character.  Requires TextObject
    | TextObjectWithLineToCharacter = 0x8

    /// Text object with always character.  Requires TextObject
    | TextObjectWithAlwaysCharacter = 0x10

    /// Text objcet with always line.  Requires TextObject
    | TextObjectWithAlwaysLine = 0x12

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

type CommandRunDataEventArgs(_commandRunData : CommandRunData) =
    inherit System.EventArgs()

    member x.CommandRunData = _commandRunData

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

/// Responsible for managing a set of Commands and running them
type ICommandRunner =

    /// Set of Commands currently supported
    abstract Commands : CommandBinding seq

    /// In certain circumstances a specific type of key remapping needs to occur for input.  This 
    /// option will have the appropriate value in those circumstances.  For example while processing
    /// the {char} argument to f,F,t or T the Language mapping will be used
    abstract KeyRemapMode : KeyRemapMode 

    /// True when in the middle of a count operation
    abstract InCount : bool

    /// Is the command runner currently binding a command which needs to explicitly handle escape
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
    abstract CommandRan : IDelegateEvent<System.EventHandler<CommandRunDataEventArgs>>

/// Information about a single key mapping
[<NoComparison>]
[<NoEquality>]
type KeyMapping = {

    // The LHS of the key mapping
    Left : KeyInputSet

    // The RHS of the key mapping
    Right : KeyInputSet 

    // Does the expansion participate in remapping
    AllowRemap : bool
}

/// Manages the key map for Vim.  Responsible for handling all key remappings
type IKeyMap =

    /// Is the mapping of the 0 key currently enabled
    abstract IsZeroMappingEnabled : bool with get, set 

    /// Get all mappings for the specified mode
    abstract GetKeyMappingsForMode : KeyRemapMode -> KeyMapping list

    /// Get the mapping for the provided KeyInput for the given mode.  If no mapping exists
    /// then a sequence of a single element containing the passed in key will be returned.  
    /// If a recursive mapping is detected it will not be persued and treated instead as 
    /// if the recursion did not exist
    abstract GetKeyMapping : KeyInputSet -> KeyRemapMode -> KeyMappingResult

    /// Map the given key sequence without allowing for remaping
    abstract MapWithNoRemap : lhs : string -> rhs : string -> KeyRemapMode -> bool

    /// Map the given key sequence allowing for a remap 
    abstract MapWithRemap : lhs : string -> rhs : string -> KeyRemapMode -> bool

    /// Unmap the specified key sequence for the specified mode
    abstract Unmap : lhs : string -> KeyRemapMode -> bool

    /// Unmap the specified key sequence for the specified mode by considering
    /// the passed in value to be an expansion
    abstract UnmapByMapping : righs : string -> KeyRemapMode -> bool

    /// Clear the Key mappings for the specified mode
    abstract Clear : KeyRemapMode -> unit

    /// Clear the Key mappings for all modes
    abstract ClearAll : unit -> unit

/// Jump list information associated with an IVimBuffer.  This is maintained as a forward
/// and backwards traversable list of points with which to navigate to
///
/// Technically Vim's implementation of a jump list can span across different
/// buffers  This is limited to just a single ITextBuffer.  This is mostly due to Visual 
/// Studio's limitations in swapping out an ITextBuffer contents for a different file.  It
/// is possible but currently not a high priority here
type IJumpList = 

    /// Associated ITextView instance
    abstract TextView : ITextView

    /// Current value in the jump list.  Will be None if we are not currently traversing the
    /// jump list
    abstract Current : SnapshotPoint option

    /// Current index into the jump list.  Will be None if we are not currently traversing
    /// the jump list
    abstract CurrentIndex : int option

    /// True if we are currently traversing the list
    abstract IsTraversing : bool

    /// Get all of the jumps in the jump list.  Returns in order of most recent to oldest
    abstract Jumps : VirtualSnapshotPoint list

    /// The SnapshotPoint when the last jump occurred
    abstract LastJumpLocation : VirtualSnapshotPoint option

    /// Add a given SnapshotPoint to the jump list.  This will reset Current to point to 
    /// the begining of the jump list
    abstract Add : SnapshotPoint -> unit

    /// Clear out all of the stored jump information.  Removes all tracking information from
    /// the IJumpList
    abstract Clear : unit -> unit

    /// Move to the previous point in the jump list.  This will fail if we are not traversing
    /// the list or at the end 
    abstract MoveOlder : int -> bool

    /// Move to the next point in the jump list.  This will fail if we are not traversing
    /// the list or at the start
    abstract MoveNewer : int -> bool

    /// Set the last jump location to the given line and column
    abstract SetLastJumpLocation : line : int -> column : int -> unit

    /// Start a traversal of the list
    abstract StartTraversal : unit -> unit

type IIncrementalSearch = 

    /// True when a search is occurring
    abstract InSearch : bool

    /// When in the middle of a search this will return the SearchData for 
    /// the search
    abstract CurrentSearchData : SearchData 

    /// When in the middle of a search this will return the SearchResult for the 
    /// search
    abstract CurrentSearchResult : SearchResult 

    /// When in the middle of a search this will return the actual text which
    /// is being searched for
    abstract CurrentSearchText : string

    /// The ITextStructureNavigator used for finding 'word' values in the ITextBuffer
    abstract WordNavigator : ITextStructureNavigator

    /// Begin an incremental search in the ITextBuffer
    abstract Begin : path : Path -> BindData<SearchResult>

    /// Reset the current search to be the given value 
    abstract ResetSearch : pattern : string -> unit

    [<CLIEvent>]
    abstract CurrentSearchUpdated : IDelegateEvent<System.EventHandler<SearchResultEventArgs>>

    [<CLIEvent>]
    abstract CurrentSearchCompleted : IDelegateEvent<System.EventHandler<SearchResultEventArgs>>

    [<CLIEvent>]
    abstract CurrentSearchCancelled : IDelegateEvent<System.EventHandler<SearchDataEventArgs>>

type RecordRegisterEventArgs(_register : Register, _isAppend : bool) =
    inherit System.EventArgs()
    
    member x.Register = _register

    member x.IsAppend = _isAppend

/// Used to record macros in a Vim 
type IMacroRecorder =

    /// The current recording 
    abstract CurrentRecording : KeyInput list option

    /// Is a macro currently recording
    abstract IsRecording : bool

    /// Start recording a macro into the specified Register.  Will fail if the recorder
    /// is already recording
    abstract StartRecording : Register -> isAppend : bool -> unit

    /// Stop recording a macro.  Will fail if it's not actually recording
    abstract StopRecording : unit -> unit

    /// Raised when a macro recording is started.  Passes the Register where the recording
    /// will take place.  The bool is whether the record is an append or not
    [<CLIEvent>]
    abstract RecordingStarted : IDelegateEvent<System.EventHandler<RecordRegisterEventArgs>>

    /// Raised when a macro recording is completed.
    [<CLIEvent>]
    abstract RecordingStopped : IDelegateEvent<System.EventHandler>

[<RequireQualifiedAccess>]
type ProcessResult = 

    /// The input was processed and provided the given ModeSwitch
    | Handled of ModeSwitch

    /// The input was processed but more input is needed in order to complete
    /// an operation
    | HandledNeedMoreInput

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
            | ModeSwitch.SwitchModeOneTimeCommand _ -> true
        | HandledNeedMoreInput ->
            false
        | NotHandled -> 
            false
        | Error -> 
            false

    /// Did this actually handle the KeyInput
    member x.IsAnyHandled = 
        match x with
        | Handled _ -> true
        | HandledNeedMoreInput -> true
        | Error -> true
        | NotHandled -> false

    /// Is this a successfully handled value?
    member x.IsHandledSuccess =
        match x with
        | Handled _ -> true
        | HandledNeedMoreInput -> true
        | Error -> false
        | NotHandled -> false

    static member OfModeKind kind = 
        let switch = ModeSwitch.SwitchMode kind
        Handled switch

    /// Create a ProcessResult from the given CommandResult value
    static member OfCommandResult commandResult = 
        match commandResult with
        | CommandResult.Completed modeSwitch -> Handled modeSwitch
        | CommandResult.Error -> Error

type StringEventArgs(_message : string) =
    inherit System.EventArgs()

    member x.Message = _message

    override x.ToString() = _message

type KeyInputEventArgs (_keyInput : KeyInput) = 
    inherit System.EventArgs()

    member x.KeyInput = _keyInput

    override x.ToString() = _keyInput.ToString()

type KeyInputStartEventArgs (_keyInput : KeyInput) =
    inherit KeyInputEventArgs(_keyInput)

    let mutable _handled = false

    member x.Handled 
        with get() = _handled 
        and set value = _handled <- value

    override x.ToString() = _keyInput.ToString()

type KeyInputSetEventArgs (_keyInputSet : KeyInputSet) = 
    inherit System.EventArgs()

    member x.KeyInputSet = _keyInputSet

    override x.ToString() = _keyInputSet.ToString()

type KeyInputProcessedEventArgs(_keyInput : KeyInput, _processResult : ProcessResult) =
    inherit System.EventArgs()

    member x.KeyInput = _keyInput

    member x.ProcessResult = _processResult

    override x.ToString() = _keyInput.ToString()

/// Implements a list for storing history items.  This is used for the 5 types
/// of history lists in Vim (:help history).  
type HistoryList () = 

    let mutable _list : string list = List.empty
    let mutable _limit = Constants.DefaultHistoryLength
    let mutable _totalCount = 0

    /// Limit of the items stored in the list
    member x.Limit 
        with get () = 
            _limit
        and set value = 
            _limit <- value
            x.MaybeTruncateList()

    /// The count of actual items currently stored in the collection
    member x.Count = _list.Length

    /// This is a truncating list.  As items exceed the set Limit the eeriest items will
    /// be removed from the collection.  The total count represents the number of items
    /// that have ever been added, not the current count
    member x.TotalCount = _totalCount

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
            _totalCount <- _totalCount + 1

    /// Reset the list back to it's original state
    member x.Reset () = 
        _list <- List.empty
        _totalCount <- 0
        _limit <- Constants.DefaultHistoryLength

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
    abstract RemapMode : KeyRemapMode

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

/// An active use of an IHistoryClient instance 
type internal IHistorySession<'TData, 'TResult> =

    /// The IHistoryClient this session is using 
    abstract HistoryClient : IHistoryClient<'TData, 'TResult>

    /// The current command that is being used 
    abstract Command : string 

    /// The current client data 
    abstract ClientData : 'TData

    /// Cancel the IHistorySession
    abstract Cancel : unit -> unit

    /// Reset the command to the current value
    abstract ResetCommand : string -> unit

    /// Create an BindDataStorage for this session which will process relevant KeyInput values
    /// as manipulating the current history
    abstract CreateBindDataStorage : unit -> BindDataStorage<'TResult>

/// Represents shared state which is available to all IVimBuffer instances.
type IVimData = 

    /// The set of supported auto command groups
    abstract AutoCommandGroups : AutoCommandGroup list with get, set

    /// The set of auto commands
    abstract AutoCommands : AutoCommand list with get, set

    /// The current directory Vim is positioned in
    abstract CurrentDirectory : string with get, set

    /// The history of the : command list
    abstract CommandHistory : HistoryList with get, set

    /// This is the pattern for which all occurences should be highlighted in the visible
    /// IVimBuffer instances.  When this value is empty then no pattern should be highlighted
    abstract DisplayPattern : string

    /// The ordered list of incremental search values
    abstract SearchHistory : HistoryList with get, set

    /// Motion function used with the last f, F, t or T motion.  The 
    /// first item in the tuple is the forward version and the second item
    /// is the backwards version
    abstract LastCharSearch : (CharSearchKind * Path * char) option with get, set

    /// The last command which was ran 
    abstract LastCommand : StoredCommand option with get, set

    /// The last shell command that was run
    abstract LastShellCommand : string option with get, set

    /// The last macro register which was run
    abstract LastMacroRun : char option with get, set

    /// Last pattern searched for in any buffer.
    abstract LastPatternData : PatternData with get, set

    /// Data for the last substitute command performed
    abstract LastSubstituteData : SubstituteData option with get, set

    /// The previous value of the current directory Vim is positioned in
    abstract PreviousCurrentDirectory : string

    /// Suspend the display of patterns in the visible IVimBuffer instances.  This is usually
    /// associated with the use of the :nohl command
    abstract SuspendDisplayPattern : unit -> unit

    /// Resume the display of patterns in the visible IVimBuffer instance.  If the display
    /// isn't currently suspended then tihs command will have no effect on the system
    abstract ResumeDisplayPattern : unit -> unit

    /// Raised when the DisplayPattern property changes
    [<CLIEvent>]
    abstract DisplayPatternChanged : IDelegateEvent<System.EventHandler>

type FontPropertiesEventArgs () =

    inherit System.EventArgs()

type IFontProperties =

    /// The font family
    abstract FontFamily : System.Windows.Media.FontFamily

    /// The font size in points
    abstract FontSize : double

    [<CLIEvent>]
    abstract FontPropertiesChanged : IDelegateEvent<System.EventHandler<FontPropertiesEventArgs>>

[<RequireQualifiedAccess>]
[<NoComparison>]
type QuickFix =
    | Next
    | Previous

type TextViewChangedEventArgs
    (
        _oldTextView : ITextView option,
        _newTextView : ITextView option
    ) =

    inherit System.EventArgs()

    member x.OldTextView = _oldTextView

    member x.NewTextView = _newTextView

type IVimHost =

    /// Should vim automatically start synchronization of IVimBuffer instances when they are 
    /// created
    abstract AutoSynchronizeSettings : bool 

    /// Get the count of tabs that are active in the host.  If tabs are not supported then
    /// -1 should be returned
    abstract TabCount : int

    /// Get the font properties associated with the text editor
    abstract FontProperties : IFontProperties

    abstract Beep : unit -> unit

    /// Called at the start of a bulk operation such as a macro replay or a repeat of
    /// a last command
    abstract BeginBulkOperation : unit -> unit

    /// Close the provided view
    abstract Close : ITextView -> unit

    /// Create a hidden ITextView instance.  This is primarily used to load the contents
    /// of the vimrc
    abstract CreateHiddenTextView : unit -> ITextView

    /// Called at the end of a bulk operation such as a macro replay or a repeat of
    /// a last command
    abstract EndBulkOperation : unit -> unit

    /// Ensure that the given point is visible
    abstract EnsureVisible : textView : ITextView -> point : SnapshotPoint -> unit

    /// Format the provided lines
    abstract FormatLines : textView : ITextView -> range : SnapshotLineRange -> unit

    /// Get the ITextView which currently has keyboard focus
    abstract GetFocusedTextView : unit -> ITextView option

    /// Get the tab index of the tab containing the given ITextView.  A number less
    /// than 0 indicates the value couldn't be determined
    abstract GetTabIndex : textView : ITextView -> int

    /// Go to the definition of the value under the cursor
    abstract GoToDefinition : unit -> bool

    /// Go to the local declaration of the value under the cursor
    abstract GoToLocalDeclaration : textView : ITextView -> identifier : string -> bool

    /// Go to the local declaration of the value under the cursor
    abstract GoToGlobalDeclaration : tetxView : ITextView -> identifier : string -> bool

    /// Go to the nth tab in the tab list.  This value is always a 0 based index 
    /// into the set of tabs.  It does not correspond to vim's handling of tab
    /// values which is not a standard 0 based index
    abstract GoToTab : index : int -> unit

    /// Go to the specified entry in the quick fix list
    abstract GoToQuickFix : quickFix : QuickFix -> count : int -> hasBang : bool -> bool

    /// Get the name of the given ITextBuffer
    abstract GetName : textBuffer : ITextBuffer -> string

    /// Is the ITextBuffer in a dirty state?
    abstract IsDirty : textBuffer : ITextBuffer -> bool

    /// Is the ITextBuffer readonly
    abstract IsReadOnly : textBuffer : ITextBuffer -> bool

    /// Is the ITextView visible to the user
    abstract IsVisible : textView : ITextView -> bool

    /// Loads the new file into the existing window
    abstract LoadFileIntoExistingWindow : filePath : string -> textView : ITextView -> HostResult

    /// Loads the new file into a new existing window
    abstract LoadFileIntoNewWindow : filePath : string -> HostResult

    /// Run the host specific make operation
    abstract Make : jumpToFirstError : bool -> arguments : string -> HostResult

    /// Move the focus to the ITextView in the open document in the specified direction
    abstract MoveFocus : textView : ITextView -> direction : Direction -> HostResult

    abstract NavigateTo : point : VirtualSnapshotPoint -> bool

    /// Quit the application
    abstract Quit : unit -> unit

    /// Reload the contents of the ITextBuffer discarding any changes
    abstract Reload : ITextBuffer -> bool

    /// Run the specified command with the given arguments and return the textual
    /// output
    abstract RunCommand : file : string -> arguments : string -> vimHost : IVimData -> string

    /// Run the Visual studio command
    abstract RunVisualStudioCommand : commandName : string -> argument : string -> unit

    /// Save the provided ITextBuffer instance
    abstract Save : textBuffer : ITextBuffer -> bool 

    /// Save the current document as a new file with the specified name
    abstract SaveTextAs : text : string -> filePath : string -> bool 

    /// Called by Vim when it encounters a new ITextView and needs to know if it should 
    /// create an IVimBuffer for it
    abstract ShouldCreateVimBuffer : textView : ITextView -> bool

    /// Split the views horizontally
    abstract SplitViewHorizontally : ITextView -> HostResult

    /// Split the views horizontally
    abstract SplitViewVertically : ITextView -> HostResult

    /// Called when VsVim attempts to load the user _vimrc file.  If the load succeeded 
    /// then the resulting settings are passed into the method.  If the load failed it is 
    /// the defaults.  Either way, they are the default settings used for new buffers
    abstract VimRcLoaded : vimRcState : VimRcState -> localSettings : IVimLocalSettings -> windowSettings : IVimWindowSettings -> unit

    /// Allow the host to custom process the insert command.  Hosts often have
    /// special non-vim semantics for certain types of edits (Enter for 
    /// example).  This override allows them to do this processing
    abstract TryCustomProcess : textView : ITextView -> command : InsertCommand -> bool


    /// Raised when the visibility of an ITextView changes
    [<CLIEvent>]
    abstract IsVisibleChanged : IDelegateEvent<System.EventHandler<TextViewEventArgs>>

    /// Raised when the active ITextView changes
    [<CLIEvent>]
    abstract ActiveTextViewChanged : IDelegateEvent<System.EventHandler<TextViewChangedEventArgs>>

/// Core parts of an IVimBuffer.  Used for components which make up an IVimBuffer but
/// need the same data provided by IVimBuffer.
type IVimBufferData =

    /// The current directory for this particular window
    abstract CurrentDirectory : string option with get, set

    /// This is the caret point at the start of the most recent visual mode session. It's
    /// the actual location of the caret vs. the anchor point.
    abstract VisualCaretStartPoint : ITrackingPoint option with get, set

    /// This is the anchor point for the visual mode selection.  It is different than the anchor
    /// point in ITextSelection.  The ITextSelection anchor point is always the start or end
    /// of the visual selection.  While the anchor point for visual mode selection may be in 
    /// the middle (in say line wise mode)
    abstract VisualAnchorPoint : ITrackingPoint option with get, set

    /// The IJumpList associated with the IVimBuffer
    abstract JumpList : IJumpList

    /// The ITextView associated with the IVimBuffer
    abstract TextView : ITextView

    /// The ITextBuffer associated with the IVimBuffer
    abstract TextBuffer : ITextBuffer

    /// The IStatusUtil associated with the IVimBuffer
    abstract StatusUtil : IStatusUtil

    /// The IUndoRedOperations associated with the IVimBuffer
    abstract UndoRedoOperations : IUndoRedoOperations

    /// The IVimTextBuffer associated with the IVimBuffer
    abstract VimTextBuffer : IVimTextBuffer

    /// The IVimWindowSettings associated with the ITextView 
    abstract WindowSettings : IVimWindowSettings

    /// The IWordUtil associated with the IVimBuffer
    abstract WordUtil : IWordUtil

    /// The IVimLocalSettings associated with the ITextBuffer
    abstract LocalSettings : IVimLocalSettings

    abstract Vim : IVim

/// Vim instance.  Global for a group of buffers
and IVim =

    /// Buffer actively processing input.  This has no relation to the IVimBuffer
    /// which has focus 
    abstract ActiveBuffer : IVimBuffer option

    /// Whether or not the vimrc file should be autoloaded before the first IVimBuffer
    /// is created
    abstract AutoLoadVimRc : bool with get, set

    /// Get the set of tracked IVimBuffer instances
    abstract VimBuffers : IVimBuffer list

    /// Get the IVimBuffer which currently has KeyBoard focus
    abstract FocusedBuffer : IVimBuffer option

    /// Is Vim currently disabled 
    abstract IsDisabled : bool with get, set

    /// In the middle of a bulk operation such as a macro replay or repeat last command
    abstract InBulkOperation : bool

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

    /// IGlobalSettings for this IVim instance
    abstract GlobalSettings : IVimGlobalSettings

    /// The variable map for this IVim instance
    abstract VariableMap : VariableMap

    abstract VimData : IVimData 

    abstract VimHost : IVimHost

    /// The state of the VimRc file
    abstract VimRcState : VimRcState

    /// Create an IVimBuffer for the given ITextView
    abstract CreateVimBuffer : textView: ITextView -> IVimBuffer

    /// Create an IVimTextBuffer for the given ITextBuffer
    abstract CreateVimTextBuffer : textBuffer: ITextBuffer -> IVimTextBuffer

    /// Close all IVimBuffer instances in the system
    abstract CloseAllVimBuffers : unit -> unit

    /// Get the IVimInterpreter for the specified IVimBuffer
    abstract GetVimInterpreter : vimBuffer : IVimBuffer -> IVimInterpreter

    /// Get or create an IVimBuffer for the given ITextView
    abstract GetOrCreateVimBuffer : textView: ITextView -> IVimBuffer

    /// Get or create an IVimTextBuffer for the given ITextBuffer
    abstract GetOrCreateVimTextBuffer : textBuffer: ITextBuffer -> IVimTextBuffer

    /// Load the VimRc file.  If the file was previously loaded a new load will be 
    /// attempted.  Returns true if a VimRc was actually loaded.
    abstract LoadVimRc : unit -> VimRcState

    /// Remove the IVimBuffer associated with the given view.  This will not actually close
    /// the IVimBuffer but instead just removes it's association with the given view
    abstract RemoveVimBuffer : ITextView -> bool

    /// Get the IVimBuffer associated with the given ITextView
    abstract TryGetVimBuffer : textView : ITextView * [<Out>] vimBuffer : IVimBuffer byref -> bool

    /// Get the IVimTextBuffer associated with the given ITextBuffer
    abstract TryGetVimTextBuffer : textBuffer : ITextBuffer * [<Out>] vimTextBuffer : IVimTextBuffer byref -> bool

    /// Get or create an IVimBuffer for the given ITextView.  The creation of the IVimBuffer will
    /// only occur if the host returns true from IVimHost::ShouldCreateVimBuffer.  
    ///
    /// MEF component load ordering isn't defined and it's very possible that components like the 
    /// ITagger implementations will be called long before the host has a chance to create the 
    /// IVimBuffer instance.  This method removes the ordering concerns and maintains control of 
    /// creation in the IVimHost
    abstract TryGetOrCreateVimBufferForHost : textView : ITextView * [<Out>] vimBuffer : IVimBuffer byref -> bool

and SwitchModeKindEventArgs
    (
        _modeKind : ModeKind,
        _modeArgument : ModeArgument
    ) =
    inherit System.EventArgs()

    member x.ModeKind = _modeKind

    member x.ModeArgument = _modeArgument

and SwitchModeEventArgs 
    (
        _previousMode : IMode,
        _currentMode : IMode
    ) = 

    inherit System.EventArgs()

    /// Current IMode 
    member x.CurrentMode = _currentMode

    /// Previous IMode.  Expressed as an Option because the first mode switch
    /// has no previous one
    member x.PreviousMode = _previousMode

and IMarkMap =

    /// The set of active global marks
    abstract GlobalMarks : (Letter * VirtualSnapshotPoint) seq

    /// Get the mark for the given char for the IVimTextBuffer
    abstract GetMark : mark : Mark -> vimBufferData : IVimBufferData -> VirtualSnapshotPoint option

    /// Get the current value of the specified global mark
    abstract GetGlobalMark : letter : Letter -> VirtualSnapshotPoint option

    /// Set the global mark to the given line and column in the provided IVimTextBuffer
    abstract SetGlobalMark : letter: Letter -> vimtextBuffer : IVimTextBuffer -> line : int -> column : int -> unit

    /// Set the mark for the given char for the IVimTextBuffer
    abstract SetMark : mark : Mark -> vimBufferData : IVimBufferData -> line : int -> column : int -> bool

    /// Remove the specified mark and return whether or not a mark was actually
    /// removed
    abstract RemoveGlobalMark : letter : Letter -> bool

    /// Delete all of the global marks 
    abstract Clear : unit -> unit

/// This is the interface which represents the parts of a vim buffer which are shared amongst all
/// of it's views
and IVimTextBuffer = 

    /// The associated ITextBuffer instance
    abstract TextBuffer : ITextBuffer

    /// The associated IVimGlobalSettings instance
    abstract GlobalSettings : IVimGlobalSettings

    /// The last VisualSpan selection for the IVimTextBuffer.  This is a combination of a VisualSpan
    /// and the SnapshotPoint within the span where the caret should be positioned
    abstract LastVisualSelection : VisualSelection option with get, set

    /// The point the caret occupied when Insert mode was exited 
    abstract LastInsertExitPoint : SnapshotPoint option with get, set

    /// The point the caret occupied when the last edit occurred
    abstract LastEditPoint : SnapshotPoint option with get, set

    /// The set of active local marks in the ITextBuffer
    abstract LocalMarks : (LocalMark * VirtualSnapshotPoint) seq

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

    /// Clear out all of the cached information in the IVimTextBuffer.  It will reset to it's startup
    /// state 
    abstract Clear : unit -> unit

    /// Get the local mark value 
    abstract GetLocalMark : localMark : LocalMark -> VirtualSnapshotPoint option

    /// Set the local mark value to the specified line and column.  Returns false if the given 
    /// mark cannot be set
    abstract SetLocalMark : localMark : LocalMark -> line : int -> column : int -> bool

    /// Remove the specified local mark.  Returns whether a mark was actually removed
    abstract RemoveLocalMark : localMark : LocalMark -> bool

    /// Switch the current mode to the provided value
    abstract SwitchMode : ModeKind -> ModeArgument -> unit

    /// Raised when the mode is switched.  Returns the old and new mode 
    [<CLIEvent>]
    abstract SwitchedMode : IDelegateEvent<System.EventHandler<SwitchModeKindEventArgs>>

/// Main interface for the Vim editor engine so to speak. 
and IVimBuffer =

    /// Sequence of available Modes
    abstract AllModes : seq<IMode>

    /// Buffered KeyInput list.  When a key remapping has multiple source elements the input 
    /// is buffered until it is completed or the ambiguity is removed.  
    abstract BufferedKeyInputs : KeyInput list

    /// The current directory for this particular window
    abstract CurrentDirectory : string option with get, set

    /// The ICommandUtil for this IVimBuffer
    abstract CommandUtil : ICommandUtil

    /// Global settings for the buffer
    abstract GlobalSettings : IVimGlobalSettings

    /// IIncrementalSearch instance associated with this IVimBuffer
    abstract IncrementalSearch : IIncrementalSearch

    /// Whether or not the IVimBuffer is currently processing a KeyInput value
    abstract IsProcessingInput : bool

    /// Is this IVimBuffer instance closed
    abstract IsClosed : bool

    /// Jump list
    abstract JumpList : IJumpList

    /// Local settings for the buffer
    abstract LocalSettings : IVimLocalSettings

    /// Associated IMarkMap
    abstract MarkMap : IMarkMap

    /// Current mode of the buffer
    abstract Mode : IMode

    /// ModeKind of the current IMode in the buffer
    abstract ModeKind : ModeKind

    /// Name of the buffer.  Used for items like Marks
    abstract Name : string

    /// If we are in the middle of processing a "one time command" (<c-o>) then this will
    /// hold the ModeKind which will be switched back to after it's completed
    abstract InOneTimeCommand : ModeKind option

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
    abstract VimBufferData : IVimBufferData

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

    /// IVisualMode for visual character mode
    abstract VisualCharacterMode : IVisualMode

    /// IVisualMode for visual line mode
    abstract VisualLineMode : IVisualMode

    /// IVisualMode for visual block mode
    abstract VisualBlockMode : IVisualMode

    /// IInsertMode instance for insert mode
    abstract InsertMode : IInsertMode

    /// IInsertMode instance for replace mode
    abstract ReplaceMode : IInsertMode

    /// ISelectMode instance for character mode
    abstract SelectCharacterMode: ISelectMode

    /// ISelectMode instance for line mode
    abstract SelectLineMode: ISelectMode

    /// ISelectMode instance for block mode
    abstract SelectBlockMode: ISelectMode

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

    /// Process all of the buffered KeyInput values.
    abstract ProcessBufferedKeyInputs : unit -> unit

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
    abstract SwitchedMode : IDelegateEvent<System.EventHandler<SwitchModeEventArgs>>

    /// Raised when a KeyInput is received by the buffer.  This will be raised for the 
    /// KeyInput which was received and does not consider any mappings
    [<CLIEvent>]
    abstract KeyInputStart : IDelegateEvent<System.EventHandler<KeyInputStartEventArgs>>

    /// This is raised just before the IVimBuffer attempts to process a KeyInput 
    /// value.  This will not necessarily be the KeyInput which was raised in KeyInputStart
    /// because a mapping could have changed it to one or many other different KeyInput
    /// values.  
    ///
    /// If this event is marked as Handled then the KeyInput will never actually be 
    /// processed by the IVimBuffer.  It will instead immediately move to the 
    /// KeyInputProcessed event
    [<CLIEvent>]
    abstract KeyInputProcessing : IDelegateEvent<System.EventHandler<KeyInputStartEventArgs>>

    /// Raised when a key is processed.  This is raised when the KeyInput is actually
    /// processed by Vim, not when it is received.  
    ///
    /// Typically this occurs immediately after a Start command and is followed by an
    /// End command.  One case this doesn't happen is in a key remapping where the source 
    /// mapping contains more than one key.  In this case the input is buffered until the 
    /// second key is read and then the inputs are processed
    [<CLIEvent>]
    abstract KeyInputProcessed : IDelegateEvent<System.EventHandler<KeyInputProcessedEventArgs>>

    /// Raised when a key is received but not immediately processed.  Occurs when a
    /// key remapping has more than one source key strokes
    [<CLIEvent>]
    abstract KeyInputBuffered : IDelegateEvent<System.EventHandler<KeyInputSetEventArgs>>

    /// Raised when a KeyInput is completed processing within the IVimBuffer.  This happens 
    /// if the KeyInput is buffered or processed.  This will be raised for the KeyInput which
    /// was initially considered (came from KeyInputStart).  It won't consider any mappings
    [<CLIEvent>]
    abstract KeyInputEnd : IDelegateEvent<System.EventHandler<KeyInputEventArgs>>

    /// Raised when a warning is encountered
    [<CLIEvent>]
    abstract WarningMessage : IDelegateEvent<System.EventHandler<StringEventArgs>>

    /// Raised when an error is encountered
    [<CLIEvent>]
    abstract ErrorMessage : IDelegateEvent<System.EventHandler<StringEventArgs>>

    /// Raised when a status message is encountered
    [<CLIEvent>]
    abstract StatusMessage : IDelegateEvent<System.EventHandler<StringEventArgs>>

    /// Raised when the IVimBuffer is being closed
    [<CLIEvent>]
    abstract Closing : IDelegateEvent<System.EventHandler>

    /// Raised when the IVimBuffer is closed
    [<CLIEvent>]
    abstract Closed : IDelegateEvent<System.EventHandler>

    inherit IPropertyOwner

/// Interface for a given Mode of Vim.  For example normal, insert, etc ...
and IMode =

    /// Associated IVimTextBuffer
    abstract VimTextBuffer : IVimTextBuffer 

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

    /// Is normal mode in the middle of a count operation
    abstract InCount : bool

    /// Is normal mode in the middle of a character replace operation
    abstract InReplace : bool

    inherit IMode

/// This is the interface implemented by Insert and Replace mode
and IInsertMode =

    /// The active IWordCompletionSession if one is active
    abstract ActiveWordCompletionSession : IWordCompletionSession option

    /// Is insert mode currently in a paste operation
    abstract IsInPaste : bool

    /// Is this KeyInput value considered to be a direct insert command in the current
    /// state of the IVimBuffer.  This does not apply to commands which edit the buffer
    /// like 'CTRL-D' but instead commands like 'a', 'b' which directly edit the 
    /// ITextBuffer
    abstract IsDirectInsert : KeyInput -> bool

    /// Raised when a command is successfully run
    [<CLIEvent>]
    abstract CommandRan : IDelegateEvent<System.EventHandler<CommandRunDataEventArgs>>

    inherit IMode

and ICommandMode = 

    /// Buffered input for the current command
    abstract Command : string with get, set

    /// Run the specified command
    abstract RunCommand : string -> RunResult

    /// Raised when the command string is changed
    [<CLIEvent>]
    abstract CommandChanged : IDelegateEvent<System.EventHandler>

    inherit IMode

and IVisualMode = 

    /// The ICommandRunner implementation associated with NormalMode
    abstract CommandRunner : ICommandRunner 

    /// Mode keys need to be remapped with currently
    abstract KeyRemapMode : KeyRemapMode 

    /// Is visual mode in the middle of a count operation
    abstract InCount : bool

    /// The current Visual Selection
    abstract VisualSelection : VisualSelection

    /// Asks Visual Mode to reset what it perceives to be the original selection.  Instead it 
    /// views the current selection as the original selection for entering the mode
    abstract SyncSelection : unit -> unit

    inherit IMode 
    
and IDisabledMode =
    
    /// Help message to display 
    abstract HelpMessage : string 

    inherit IMode

and ISelectMode = 

    /// Sync the selection with the current state
    abstract SyncSelection : unit -> unit

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

module internal VimCoreExtensions =
    
    type IVim with
        member x.GetVimBuffer textView =
            let found, vimBuffer = x.TryGetVimBuffer textView
            if found then
                Some vimBuffer
            else
                None

        member x.GetOrCreateVimBufferForHost textView =
            let found, vimBuffer = x.TryGetOrCreateVimBufferForHost textView
            if found then
                Some vimBuffer
            else
                None

