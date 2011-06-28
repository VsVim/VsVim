#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Tagging

/// Used to determine if a completion window is active for a given view
type IDisplayWindowBroker =

    /// TextView this broker is associated with
    abstract TextView : ITextView 

    /// Is there currently a completion window active on the given ITextView
    abstract IsCompletionActive : bool

    /// Is signature help currently active
    abstract IsSignatureHelpActive : bool

    // Is Quick Info active
    abstract IsQuickInfoActive : bool 

    /// Is there a smart tip window active
    abstract IsSmartTagSessionActive : bool

    /// Dismiss any completion windows on the given ITextView
    abstract DismissDisplayWindows : unit -> unit

type IDisplayWindowBrokerFactoryService  =

    abstract CreateDisplayWindowBroker : ITextView -> IDisplayWindowBroker


/// What type of tracking are we doing
type LineColumnTrackingMode = 

    /// By default a 1TrackingLineColumn will be deleted if the line it 
    /// tracks is deleted
    | Default 

    /// ITrackingLineColumn should survive the deletion of it's line
    /// and just treat it as a delta adjustment
    | SurviveDeletes 

/// Tracks a line number and column across edits to the ITextBuffer.  This auto tracks
/// and will return information against the current ITextSnapshot for the 
/// ITextBuffer
type ITrackingLineColumn =

    /// ITextBuffer this ITrackingLineColumn is tracking against
    abstract TextBuffer : ITextBuffer

    /// Returns the LineColumnTrackingMode for this instance
    abstract TrackingMode : LineColumnTrackingMode

    /// Get the point as it relates to current Snapshot.
    abstract Point : SnapshotPoint option 

    /// Get the point as a VirtualSnapshot point on the current ITextSnapshot
    abstract VirtualPoint : VirtualSnapshotPoint option

    /// Needs to be called when you are done with the ITrackingLineColumn
    abstract Close : unit -> unit

type ITrackingLineColumnService = 

    /// Create an ITrackingLineColumn at the given position in the buffer.  
    abstract Create : ITextBuffer -> line:int -> column: int -> LineColumnTrackingMode -> ITrackingLineColumn

    /// Close all of the outstanding ITrackingLineColumn instances
    abstract CloseAll : unit -> unit

type IVimBufferFactory =
    
    /// Create a IVimBuffer for the given parameters
    abstract CreateBuffer : IVim -> ITextView -> IVimBuffer

type IVimBufferCreationListener =

    /// Called whenever an IVimBuffer is created
    abstract VimBufferCreated : IVimBuffer -> unit

/// Supports the creation and deletion of folds within a ITextBuffer.  Most components
/// should talk to IFoldManager directly
type IFoldData = 

    /// Associated ITextBuffer the data is over
    abstract TextBuffer : ITextBuffer

    /// Gets snapshot spans for all of the currently existing folds.  This will
    /// only return the folds explicitly created by vim.  It won't return any
    /// collapsed regions in the ITextView
    abstract Folds : SnapshotSpan seq

    /// Create a fold for the given line range
    abstract CreateFold : SnapshotLineRange -> unit 

    /// Delete a fold which crosses the given SnapshotPoint.  Returns false if 
    /// there was no fold to be deleted
    abstract DeleteFold : SnapshotPoint -> bool

    /// Delete all of the folds which intersect the given SnapshotSpan
    abstract DeleteAllFolds : SnapshotSpan -> unit

    /// Raised when the collection of folds are updated for any reason
    [<CLIEvent>]
    abstract FoldsUpdated: IEvent<System.EventArgs>

/// Supports the creation and deletion of folds within a ITextBuffer
///
/// TODO: This should become a merger between folds and outlining regions in 
/// an ITextBuffer / ITextView
type IFoldManager = 

    /// Associated ITextView
    abstract TextView : ITextView

    /// Create a fold for the given line range
    abstract CreateFold : SnapshotLineRange -> unit 

    /// Close 'count' fold values under the given SnapshotPoint
    abstract CloseFold : SnapshotPoint -> int -> unit

    /// Close all folds which intersect the given SnapshotSpan
    abstract CloseAllFolds : SnapshotSpan -> unit

    /// Delete a fold which crosses the given SnapshotPoint.  Returns false if 
    /// there was no fold to be deleted
    abstract DeleteFold : SnapshotPoint -> unit

    /// Delete all of the folds which intersect the SnapshotSpan
    abstract DeleteAllFolds : SnapshotSpan -> unit

    /// Open 'count' folds under the given SnapshotPoint
    abstract OpenFold : SnapshotPoint -> int -> unit

    /// Open all folds which intersect the given SnapshotSpan
    abstract OpenAllFolds : SnapshotSpan -> unit

/// Supports the get and creation of IFoldManager for a given ITextBuffer
type IFoldManagerFactory =

    /// Get the IFoldData for this ITextBuffer
    abstract GetFoldData : ITextBuffer -> IFoldData

    /// Get the IFoldManager for this ITextView.
    abstract GetFoldManager : ITextView -> IFoldManager

/// Abstract representation of the mouse
type IMouseDevice = 
    
    /// Is the left button pressed
    abstract IsLeftButtonPressed : bool

/// Abstract representation of the keyboard 
type IKeyboardDevice = 

    /// Is the given key pressed
    abstract IsKeyDown : VimKey -> bool

/// Tracks changes to the IVimBuffer
type ITextChangeTracker =

    /// Associated IVimBuffer
    abstract VimBuffer : IVimBuffer

    /// Current change
    abstract CurrentChange : TextChange option

    /// Raised when a change is completed
    [<CLIEvent>]
    abstract ChangeCompleted : IEvent<TextChange>

/// Manages the ITextChangeTracker instances
type ITextChangeTrackerFactory =

    abstract GetTextChangeTracker : IVimBuffer -> ITextChangeTracker

/// Provides access to the system clipboard 
type IClipboardDevice =

    abstract Text : string with get,set

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

    /// Ensure the caret is on the visible screen
    abstract EnsureCaretOnScreen : unit -> unit

    /// Ensure the caret is on screen and that it is not in a collapsed region
    abstract EnsureCaretOnScreenAndTextExpanded : unit -> unit

    /// Ensure the point is on screen and that it is not in a collapsed region
    abstract EnsurePointOnScreenAndTextExpanded : SnapshotPoint -> unit

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

    /// Move the caret to the specified SnapshotPoint and make sure to adjust for the 
    /// virtual edit settings
    abstract MoveCaretToPointAndCheckVirtualSpace : SnapshotPoint -> unit

    /// Move the caret to the MotionResult value
    abstract MoveCaretToMotionResult : MotionResult -> unit

    /// Navigate to the given point which may occur in any ITextBuffer.  This will not update the 
    /// jump list
    abstract NavigateToPoint : VirtualSnapshotPoint -> bool

    /// Normalize the spaces and tabs in the string
    abstract NormalizeBlanks : string -> string

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

/// Factory for getting ICommonOperations instances
type ICommonOperationsFactory =

    /// Get the ICommonOperations instance for this IVimBuffer
    abstract GetCommonOperations : VimBufferData -> ICommonOperations

