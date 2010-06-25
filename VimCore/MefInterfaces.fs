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

type ITrackingLineColumn =
    abstract TextBuffer : ITextBuffer

    /// Get the point as it relates to current Snapshot.  Returns None
    /// in the case that the line and column cannot be matched
    abstract Point : SnapshotPoint option 

    /// Get the point as it relates the current Snapshot.  If the current
    /// length of the line is not long enough to support the column, it will be 
    /// truncated to the last non-linebreak character of the line
    abstract PointTruncating: SnapshotPoint option

    /// Get the point as a VirtualSnapshot point on the current ITextSnapshot
    abstract VirtualPoint : VirtualSnapshotPoint option

    /// Needs to be called when you are done with the ITrackingLineColumn
    abstract Close : unit -> unit

type ITrackingLineColumnService = 

    /// Create an ITrackingLineColumn at the given position in the buffer.  
    abstract Create : ITextBuffer -> line:int -> column: int -> ITrackingLineColumn

    /// Creates a disconnected ITrackingLineColumn instance.  ITrackingLineColumn 
    /// instances can only be created against the current snapshot of an ITextBuffer.  This
    /// method is useful when a valid one can't be supplied so instead we provide 
    /// a ITrackingLineColumn which satisifies the interface but produces no values
    abstract CreateDisconnected : ITextBuffer -> ITrackingLineColumn

    /// Creates an ITrackingLineColumn for the given SnapshotPoint.  If the point does
    /// not point to the current snapshot of ITextBuffer, a disconnected ITrackingLineColumn
    /// will be created
    abstract CreateForPoint : SnapshotPoint -> ITrackingLineColumn

    /// Close all of the outstanding ITrackingLineColumn instances
    abstract CloseAll : unit -> unit

type IVimBufferFactory =
    
    /// Create a IVimBuffer for the given parameters
    abstract CreateBuffer : IVim -> ITextView -> IVimBuffer

type IVimBufferCreationListener =

    /// Called whenever an IVimBuffer is created
    abstract VimBufferCreated : IVimBuffer -> unit

