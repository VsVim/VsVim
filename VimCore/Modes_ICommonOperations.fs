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
    StatusUtil : IStatusUtil
    TextView : ITextView
    UndoRedoOperations : IUndoRedoOperations
    VimData : IVimData
    VimHost : IVimHost
    Navigator : ITextStructureNavigator
}

[<RequireQualifiedAccess>]
type Result = 
    | Succeeded
    | Failed of string

/// This class abstracts out the operations that are common to normal, visual and 
/// command mode.  It usually contains common edit and movement operations and very
/// rarely will deal with caret operations.  That is the responsibility of the 
/// caller
type ICommonOperations =

    /// Associated ITextView
    abstract TextView : ITextView 

    /// Associated IEditorOperations
    abstract EditorOperations : IEditorOperations

    /// Associated IEditorOptions
    abstract EditorOptions : IEditorOptions

    /// Associated ISearchService instance
    abstract SearchService : ISearchService

    /// Associated IUndoRedoOperations
    abstract UndoRedoOperations : IUndoRedoOperations

    /// Apply the TextChange to the ITextBuffer 'count' times
    abstract ApplyTextChange : TextChange -> addNewLintes : bool -> int -> unit

    /// Run the beep operation
    abstract Beep : unit -> unit

    /// Close count folds in the given SnapshotSpan
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

    /// Go to the file named in the word under the cursor in a new window
    abstract GoToFileInNewWindow : unit -> unit

    /// Go to the local declaration of the word under the cursor
    abstract GoToLocalDeclaration : unit -> unit

    /// Go to the global declaration of the word under the cursor
    abstract GoToGlobalDeclaration : unit -> unit

    /// Go to the "count" next tab window in the specified direction.  This will wrap 
    /// around
    abstract GoToNextTab : Path -> count : int -> unit

    /// Go the nth tab.  The first tab can be accessed with both 0 and 1
    abstract GoToTab : int -> unit

    /// Joins the lines in the range
    abstract Join : SnapshotLineRange -> JoinKind -> unit

    /// Jumps to a given mark in the buffer.  
    abstract JumpToMark : char -> IMarkMap -> Result

    /// Move the caret to a given point on the screen
    abstract MoveCaretToPoint : SnapshotPoint -> unit

    /// Move the caret to a given point on the screen and ensure it's visible and the surrounding
    /// text is expanded
    abstract MoveCaretToPointAndEnsureVisible : SnapshotPoint -> unit

    /// Move the caret to the MotionResult value
    abstract MoveCaretToMotionResult : MotionResult -> unit

    /// Maybe adjust the caret to respect the virtual edit setting
    abstract MoveCaretForVirtualEdit : unit -> unit

    /// Navigate to the given point which may occur in any ITextBuffer.  This will not update the 
    /// jump list
    abstract NavigateToPoint : VirtualSnapshotPoint -> bool

    /// Open count folds in the given SnapshotSpan 
    abstract OpenFold : SnapshotSpan -> count:int -> unit

    /// Open all folds which intersect with the given SnapshotSpan
    abstract OpenAllFolds : SnapshotSpan -> unit

    /// Put the specified StringData at the given point.
    abstract Put : SnapshotPoint -> StringData -> OperationKind -> unit

    /// Raise the error / warning messages for the given SearchResult
    abstract RaiseSearchResultMessage : SearchResult -> unit

    /// Redo the buffer changes "count" times
    abstract Redo : count:int -> unit

    /// Sets a mark at the specified point.  If this operation fails an error message will be generated
    abstract SetMark : SnapshotPoint -> char -> IMarkMap -> Result

    /// Scrolls the number of lines given and keeps the caret in the view
    abstract ScrollLines : ScrollDirection -> count:int -> unit

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

