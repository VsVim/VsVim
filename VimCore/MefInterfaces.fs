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

/// Supports the creation and deletion of folds within a ITextBuffer
type IFoldManager = 

    /// Associated ITextBuffer
    abstract TextBuffer : ITextBuffer

    /// Gets snapshot spans for all of the currently existing folds
    abstract Folds : SnapshotSpan seq

    /// Create a fold for the given line range
    abstract CreateFold : SnapshotLineRange -> unit 

    /// Delete a fold which crosses the given SnapshotPoint.  Returns false if 
    /// there was no fold to be deleted
    abstract DeleteFold : SnapshotPoint -> bool

    /// Delete all of the folds in the buffer
    abstract DeleteAllFolds : unit -> unit

    /// Raised when the collection of folds are updated
    [<CLIEvent>]
    abstract FoldsUpdated: IEvent<System.EventArgs>

/// Supports the get and creation of IFoldManager for a given ITextBuffer
type IFoldManagerFactory =
    
    abstract GetFoldManager : ITextBuffer -> IFoldManager

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
