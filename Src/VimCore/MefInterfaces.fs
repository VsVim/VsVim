#light

namespace Vim
open EditorUtils
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Tagging
open System

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

    /// Get the display broker for the provided ITextView
    abstract GetDisplayWindowBroker : textView : ITextView -> IDisplayWindowBroker

/// What type of tracking are we doing
[<RequireQualifiedAccess>]
[<NoComparison>]
[<NoEquality>]
type LineColumnTrackingMode = 

    /// By default a 1TrackingLineColumn will be deleted if the line it 
    /// tracks is deleted
    | Default 

    /// ITrackingLineColumn should survive the deletion of it's line
    /// and just treat it as a delta adjustment
    | SurviveDeletes 

    /// Same as Survives delete but it resets the column to 0 in this 
    /// case
    | LastEditPoint

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

/// Tracks a VisualSpan across edits to the underlying ITextBuffer.
type ITrackingVisualSpan = 

    /// The associated ITextBuffer instance
    abstract TextBuffer : ITextBuffer

    /// Get the VisualSpan as it relates to the current ITextSnapshot
    abstract VisualSpan : VisualSpan option

    /// Needs to be called when the consumer is finished with the ITrackingVisualSpan
    abstract Close : unit -> unit

/// Tracks a Visual Selection across edits to the underlying ITextBuffer.  This tracks both
/// the selection area and the caret within the selection
type ITrackingVisualSelection = 

    /// The SnapshotPoint for the caret within the current ITextSnapshot
    abstract CaretPoint : SnapshotPoint option

    /// The associated ITextBuffer instance
    abstract TextBuffer : ITextBuffer

    /// Get the Visual Selection as it relates to the current ITextSnapshot
    abstract VisualSelection : VisualSelection option

    /// Needs to be called when the consumer is finished with the ITrackingVisualSpan
    abstract Close : unit -> unit

type IBufferTrackingService = 

    /// Create an ITrackingLineColumn at the given position in the buffer.  
    abstract CreateLineColumn : textBuffer : ITextBuffer -> line : int -> column : int -> LineColumnTrackingMode -> ITrackingLineColumn

    /// Create an ITrackingVisualSpan for the given VisualSpan
    abstract CreateVisualSpan : visualSpan : VisualSpan -> ITrackingVisualSpan

    /// Create an ITrackingVisualSelection for the given Visual Selection
    abstract CreateVisualSelection : visualSelection : VisualSelection -> ITrackingVisualSelection

    /// Does the given ITextBuffer have any out standing tracking instances 
    abstract HasTrackingItems : textBuffer : ITextBuffer -> bool

type IVimBufferCreationListener =

    /// Called whenever an IVimBuffer is created
    abstract VimBufferCreated : vimBuffer : IVimBuffer -> unit

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
    abstract FoldsUpdated: IDelegateEvent<System.EventHandler>

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

    /// Toggle fold under the given SnapshotPoint
    abstract ToggleFold : SnapshotPoint -> int -> unit

    /// Toggle all folds under the given SnapshotPoint
    abstract ToggleAllFolds : SnapshotSpan  -> unit

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

    /// Get the position of the mouse positine within the ITextView
    abstract GetPosition : textView : ITextView -> Nullable<System.Windows.Point>

/// Abstract representation of the keyboard 
type IKeyboardDevice = 

    /// Is the given key pressed
    abstract IsArrowKeyDown : bool

/// Tracks changes to the associated ITextView
type ITextChangeTracker =

    /// Associated ITextView
    abstract TextView : ITextView

    /// Whether or not change tracking is currently enabled.  Disabling the tracking will
    /// cause the current change to be completed
    abstract TrackCurrentChange : bool with get, set

    /// Current change
    abstract CurrentChange : TextChange option

    /// Complete the current change if there is one
    abstract CompleteChange : unit -> unit

    /// Clear out the current change without completing it
    abstract ClearChange : unit -> unit

    /// Raised when a change is completed
    [<CLIEvent>]
    abstract ChangeCompleted : IDelegateEvent<System.EventHandler<TextChangeEventArgs>>

/// Manages the ITextChangeTracker instances
type ITextChangeTrackerFactory =

    /// Get the ITextChangeTracker associated with the given vim buffer information
    abstract GetTextChangeTracker : vimBufferData : IVimBufferData -> ITextChangeTracker

/// Tracks changes to the associated ITextView
type ILineChangeTracker =

    /// Swap the most recently changed line with its saved copy
    abstract Swap : unit -> bool

/// Manages the ILineChangeTracker instances
type ILineChangeTrackerFactory =

    /// Get the ILineChangeTracker associated with the given vim buffer information
    abstract GetLineChangeTracker : vimBufferData : IVimBufferData -> ILineChangeTracker

/// Provides access to the system clipboard 
type IClipboardDevice =

    abstract Text : string with get, set

[<RequireQualifiedAccess>]
[<NoComparison>]
[<NoEquality>]
type Result = 
    | Succeeded
    | Failed of string

[<Flags>]
type ViewFlags = 
    | None = 0 

    /// Ensure the context point is visible in the ITextView
    | Visible = 0x01

    /// If the context point is inside a collapsed region then it needs to be exapnded
    | TextExpanded = 0x02

    /// Using the context point as a reference ensure the scroll respects the 'scrolloff'
    /// setting
    | ScrollOffset = 0x04

    /// Possibly move the caret to account for the 'virtualedit' setting
    | VirtualEdit = 0x08

    /// Standard flags: 
    /// Visible ||| TextExpanded ||| ScrollOffset
    | Standard = 0x07

    /// All flags
    /// Visible ||| TextExpanded ||| ScrollOffset ||| VirtualEdit
    | All = 0x0f

/// When maintaining the caret column for motion moves this represents the desired 
/// column to jump to if there is enough space on the line
///
[<RequireQualifiedAccess>]
[<NoComparison>]
[<NoEquality>]
type MaintainCaretColumn = 

    /// There is no saved caret column. 
    | None

    /// This number is kept as a count of spaces.  Tabs need to be adjusted for when applying
    /// this setting to a motion
    | Spaces of int

    /// The caret was moved with the $ motion and the further moves should move to the end of 
    /// the line 
    | EndOfLine

/// This class abstracts out the operations that are common to normal, visual and 
/// command mode.  It usually contains common edit and movement operations and very
/// rarely will deal with caret operations.  That is the responsibility of the 
/// caller
type ICommonOperations =

    /// Run the beep operation
    abstract Beep : unit -> unit

    /// Associated IEditorOperations
    abstract EditorOperations : IEditorOperations

    /// Associated IEditorOptions
    abstract EditorOptions : IEditorOptions

    /// The currently maintained caret column for up / down caret movements in the
    /// buffer
    abstract MaintainCaretColumn : MaintainCaretColumn with get, set

    /// Associated ITextView
    abstract TextView : ITextView 

    /// Associated VimBufferData instance
    abstract VimBufferData : IVimBufferData

    /// Create a possibly LineWise register value with the specified string value at the given 
    /// point.  This is factored out here because a LineWise value in vim should always
    /// end with a new line but we can't always guarantee the text we are working with 
    /// contains a new line.  This normalizes out the process needed to make this correct
    /// while respecting the settings of the ITextBuffer
    abstract CreateRegisterValue : point : SnapshotPoint -> stringData : StringData -> operationKind : OperationKind -> RegisterValue

    /// Delete at least count lines from the visual snapshot
    abstract DeleteLines : startLine : ITextSnapshotLine -> maxCount : int -> register : Register -> unit

    /// Ensure the view properties are met at the caret
    abstract EnsureAtCaret : viewFlags : ViewFlags -> unit

    /// Ensure the view properties are met at the point
    abstract EnsureAtPoint : point : SnapshotPoint -> viewFlags : ViewFlags -> unit

    /// Format the specified line range
    abstract FormatLines : SnapshotLineRange -> unit

    /// Get the new line text which should be used for new lines at the given SnapshotPoint
    abstract GetNewLineText : SnapshotPoint -> string

    /// Get the indentation for a newly created ITextSnasphotLine.  The context line is
    /// is provided to calculate the indentation off of 
    ///
    /// Warning: Calling this API can cause the buffer to be edited.  Asking certain 
    /// editor implementations about the indentation, in particular Razor, can cause
    /// an edit to occur.  
    ///
    /// Issue #946
    abstract GetNewLineIndent : contextLine : ITextSnapshotLine -> newLine : ITextSnapshotLine -> int option

    /// Get the standard ReplaceData for the given SnapshotPoint
    abstract GetReplaceData : point : SnapshotPoint -> ReplaceData

    /// Get the number of spaces (when tabs are expanded) that is necessary to get to the 
    /// specified point on it's line
    abstract GetSpacesToPoint : point : SnapshotPoint -> int

    /// Get the point that visually corresponds to the specified column on its line
    abstract GetPointForSpaces : contextLine : ITextSnapshotLine -> column : int -> SnapshotPoint

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
    abstract GoToNextTab : path : Path -> count : int -> unit

    /// Go the nth tab.  This uses vim's method of numbering tabs which is a 1 based list.  Both
    /// 0 and 1 can be used to access the first tab
    abstract GoToTab : int -> unit

    /// Joins the lines in the range
    abstract Join : SnapshotLineRange -> JoinKind -> unit

    /// Move the caret in the specified direction
    abstract MoveCaret : caretMovement : CaretMovement -> bool

    /// Move the caret in the specified direction with an arrow key
    abstract MoveCaretWithArrow : caretMovement : CaretMovement -> bool

    /// Move the caret to a given point on the screen and ensure the view has the specified
    /// properties at that point 
    abstract MoveCaretToPoint : point : SnapshotPoint -> viewFlags : ViewFlags -> unit

    /// Move the caret to the MotionResult value
    abstract MoveCaretToMotionResult : motionResult : MotionResult -> unit

    /// Navigate to the given point which may occur in any ITextBuffer.  This will not update the 
    /// jump list
    abstract NavigateToPoint : VirtualSnapshotPoint -> bool

    /// Normalize the spaces and tabs in the string
    abstract NormalizeBlanks : string -> string

    /// Normalize the set of blanks into spaces
    abstract NormalizeBlanksToSpaces : string -> string

    /// Put the specified StringData at the given point.
    abstract Put : SnapshotPoint -> StringData -> OperationKind -> unit

    /// Raise the error / warning messages for the given SearchResult
    abstract RaiseSearchResultMessage : SearchResult -> unit

    /// Redo the buffer changes "count" times
    abstract Redo : count:int -> unit

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
    abstract Substitute : pattern : string -> replace : string -> SnapshotLineRange -> flags : SubstituteFlags -> unit

    /// Undo the buffer changes "count" times
    abstract Undo : count:int -> unit

/// Factory for getting ICommonOperations instances
type ICommonOperationsFactory =

    /// Get the ICommonOperations instance for this IVimBuffer
    abstract GetCommonOperations : IVimBufferData -> ICommonOperations

/// This interface is used to prevent the transition from insert to visual mode
/// when a selection occurs.  In the majority case a selection of text should result
/// in a transition to visual mode.  In some cases though, C# event handlers being
/// the most notable, the best experience is for the buffer to remain in insert 
/// mode
type IVisualModeSelectionOverride =

    /// Is insert mode preferred for the current state of the buffer
    abstract IsInsertModePreferred : textView : ITextView -> bool

/// This interface is used to synchronize settings between vim settings and the 
/// editor settings
///
/// TODO: This process should be automatic.  The IVimHost should have to specifically
/// opt out of synchronization
type IEditorToSettingsSynchronizer = 
    
    /// Begin the synchronization between the editor and vim settings.  This will 
    /// start by overwriting the editor settings with the current local ones 
    ///
    /// This method can be called multiple times for the same IVimBuffer and it 
    /// will only synchronize once 
    abstract StartSynchronizing : vimBuffer : IVimBuffer -> unit

    /// Copy the settings which are synchronized from the editor to the 
    /// corresponding vim settings
    abstract CopyEditorToVimSettings : vimBuffer : IVimBuffer -> unit

    /// Copy the settings which are synchronized from vim to the 
    /// corresponding editor settings
    abstract CopyVimToEditorSettings : vimBuffer : IVimBuffer -> unit

