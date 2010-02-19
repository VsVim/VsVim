#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Tagging

/// MEF component which can spin up Vi components
type IVimFactoryService =
    abstract Vim : IVim
    abstract CreateKeyProcessor : IVimBuffer -> KeyProcessor
    abstract CreateMouseProcessor : IVimBuffer -> IMouseProcessor

type IBlockCaretFactoryService =
    abstract CreateBlockCaret : IWpfTextView -> IBlockCaret

/// Used to determine if a completion window is active for a given view
type ICompletionWindowBroker =

    /// TextView this broker is associated with
    abstract TextView : ITextView 

    /// Is there currently a completion window active on the given ITextView
    abstract IsCompletionWindowActive : bool

    /// Dismiss any completion windows on the given ITextView
    abstract DismissCompletionWindow : unit -> unit

type ICompletionWindowBrokerFactoryService =
    abstract CreateCompletionWindowBroker : ITextView -> ICompletionWindowBroker

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
    abstract CreateBuffer : IVim -> IWpfTextView -> IVimBuffer

    /// Raised when an IVimBuffer instance is created
    [<CLIEvent>]
    abstract BufferCreated: IEvent<IVimBuffer>
