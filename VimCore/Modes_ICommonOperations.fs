#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining


type OperationsData = {
    EditorOperations : IEditorOperations
    EditorOptions : IEditorOptions
    FoldManager : IFoldManager
    JumpList : IJumpList
    KeyMap : IKeyMap
    LocalSettings : IVimLocalSettings
    OutliningManager : IOutliningManager option
    RegisterMap : IRegisterMap 
    SearchService : ISearchService
    SmartIndentationService : ISmartIndentationService
    StatusUtil : IStatusUtil
    TextView : ITextView
    UndoRedoOperations : IUndoRedoOperations
    VimData : IVimData
    VimHost : IVimHost
    Navigator : ITextStructureNavigator
}

type Result = 
| Succeeded
| Failed of string

[<RequireQualifiedAccess>]
type PutKind =
| Before
| After

/// This class abstracts out the operations that are common to normal, visual and 
/// command mode.  It usually contains common edit and movement operations and very
/// rarely will deal with caret operations.  That is the responsibility of the 
/// caller
type ICommonOperations =

    /// Associated ITextView
    abstract TextView : ITextView 

    /// The TabSize for the buffer
    abstract TabSize : int

    /// Whether or not to use Spaces in the buffer
    abstract UseSpaces : bool

    /// Associated IEditorOperations
    abstract EditorOperations : IEditorOperations

    /// Associated IFoldManager
    abstract FoldManager : IFoldManager

    /// Associated IUndoRedoOperations
    abstract UndoRedoOperations : IUndoRedoOperations

    /// Run the beep operation
    abstract Beep : unit -> unit

    /// Close the current buffer
    abstract Close : checkDirty : bool -> unit

    /// Close all open files
    abstract CloseAll : checkDirty : bool -> unit

    /// Close count foldse in the given SnapshotSpan
    abstract CloseFold : SnapshotSpan -> count:int -> unit

    /// Close all folds which intersect with the given SnapshotSpan
    abstract CloseAllFolds : SnapshotSpan -> unit

    /// Delete one folds at the cursor
    abstract DeleteOneFoldAtCursor : unit -> unit

    /// Delete all folds at the cursor
    abstract DeleteAllFoldsAtCursor : unit -> unit

    /// Ensure the caret is on the visible screen
    abstract EnsureCaretOnScreen : unit -> unit

    /// Ensure the caret is on screen and that it is not in a collapsed region
    abstract EnsureCaretOnScreenAndTextExpanded : unit -> unit

    /// Ensure the point is on screen and that it is not in a collapsed region
    abstract EnsurePointOnScreenAndTextExpanded : SnapshotPoint -> unit

    /// Fold count lines under the cursor
    abstract FoldLines : count:int -> unit

    /// Format the specified line range
    abstract FormatLines : SnapshotLineRange -> unit

    /// Attempt to GoToDefinition on the current state of the buffer.  If this operation fails, an error message will 
    /// be generated as appropriate
    abstract GoToDefinition : unit -> Result

    /// Go to the file named in the word under the cursor
    abstract GoToFile : unit -> unit

    /// Go to the local declaration of the word under the cursor
    abstract GoToLocalDeclaration : unit -> unit

    /// Go to the global declaration of the word under the cursor
    abstract GoToGlobalDeclaration : unit -> unit

    /// Go to the "count" next tab window in the specified direction.  This will wrap 
    /// around
    abstract GoToNextTab : Direction -> count : int -> unit

    /// Go the nth tab.  The first tab can be accessed with both 0 and 1
    abstract GoToTab : int -> unit

    /// Insert text at the caret
    abstract InsertText : string -> int -> unit

    /// Joins the lines in the range
    abstract Join : SnapshotLineRange -> JoinKind -> unit

    /// Jumps to a given mark in the buffer.  
    abstract JumpToMark : char -> IMarkMap -> Result

    /// Move the caret to a given point on the screen
    abstract MoveCaretToPoint : SnapshotPoint -> unit

    /// Move the caret to the MotionResult value
    abstract MoveCaretToMotionResult : MotionResult -> unit

    /// Move the caret count spaces left on the same line
    abstract MoveCaretLeft : count : int -> unit

    /// Move the cursor count spaces right on the same line
    abstract MoveCaretRight : count : int -> unit

    /// Move the cursor up count lines
    abstract MoveCaretUp : count : int -> unit

    /// Move the cursor down count lines
    abstract MoveCaretDown : count : int -> unit

    /// Maybe adjust the caret to respect the virtual edit setting
    abstract MoveCaretForVirtualEdit : unit -> unit

    /// Move the caret the number of lines in the given direction and scroll the view
    abstract MoveCaretAndScrollLines : ScrollDirection -> count:int -> unit

    /// Move to the next "count" occurrence of the last search
    abstract MoveToNextOccuranceOfLastSearch : count:int -> isReverse:bool -> unit

    /// Move to the next occurrence of the word under the cursor
    abstract MoveToNextOccuranceOfWordAtCursor : SearchKind -> count:int -> unit

    /// Move to the next occurrence of the word under the cursor
    abstract MoveToNextOccuranceOfPartialWordAtCursor : SearchKind -> count:int -> unit

    /// Move the cursor backward count WordKind's
    abstract MoveWordBackward : WordKind -> count : int -> unit

    /// Move the cursor forward count WordKind's 
    abstract MoveWordForward : WordKind -> count : int -> unit

    /// Navigate to the given point which may occur in any ITextBuffer.  This will not update the 
    /// jump list
    abstract NavigateToPoint : VirtualSnapshotPoint -> bool

    /// Open count folds in the given SnapshotSpan 
    abstract OpenFold : SnapshotSpan -> count:int -> unit

    /// Open all folds which inersect with the given SnapshotSpan
    abstract OpenAllFolds : SnapshotSpan -> unit

    /// Put the specified StringData at the given point 
    abstract PutAt : SnapshotPoint -> StringData -> OperationKind -> unit

    /// Put the specified StringData at the caret 
    abstract PutAtCaret : StringData -> OperationKind -> PutKind -> moveCaretAfterText:bool-> unit

    /// Put the specified StringData at the given point 
    abstract PutAtWithReturn : SnapshotPoint -> StringData -> OperationKind -> SnapshotSpan

    /// Redo the buffer changes "count" times
    abstract Redo : count:int -> unit

    /// Save the current document
    abstract Save : unit -> bool 

    /// Save the current document as the specified file
    abstract SaveAs : string -> bool

    /// Save all files
    abstract SaveAll : unit -> bool

    /// Sets a mark at the specified point.  If this operation fails an error message will be generated
    abstract SetMark : SnapshotPoint -> char -> IMarkMap -> Result

    /// Scrolls the number of lines given and keeps the caret in the view
    abstract ScrollLines : ScrollDirection -> count:int -> unit

    /// Scroll the buffer by the specified number of pages in the given direction
    abstract ScrollPages : ScrollDirection -> count:int -> unit

    /// Shift the block of lines to the left by shiftwidth * 'multiplier'
    abstract ShiftLineBlockLeft : SnapshotSpan seq -> multiplier : int -> unit

    /// Shift the block of lines to the right by shiftwidth * 'multiplier'
    abstract ShiftLineBlockRight : SnapshotSpan seq -> multiplier : int -> unit

    /// Shift the given line range left by shiftwidth * 'multiplier'
    abstract ShiftLineRangeLeft : SnapshotLineRange -> multiplier : int -> unit

    /// Shift the given line range right by shiftwidth * 'multiplier'
    abstract ShiftLineRangeRight : SnapshotLineRange -> multiplier : int -> unit

    /// Substitute Command implementation
    abstract Substitute : pattern : string -> replace : string -> SnapshotLineRange -> SubstituteFlags -> unit

    /// Undo the buffer changes "count" times
    abstract Undo : count:int -> unit

    /// Update the register for the given register operation
    abstract UpdateRegister : Register -> RegisterOperation -> EditSpan -> OperationKind -> unit

    /// Update the register for the given register value
    abstract UpdateRegisterForValue : Register -> RegisterOperation -> RegisterValue -> unit

    /// Update the register for the given register operation
    abstract UpdateRegisterForSpan : Register -> RegisterOperation -> SnapshotSpan -> OperationKind -> unit

    /// Update the register for the given register operation
    abstract UpdateRegisterForCollection : Register -> RegisterOperation -> NormalizedSnapshotSpanCollection -> OperationKind -> unit

