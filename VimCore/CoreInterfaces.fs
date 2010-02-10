#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open System.Windows.Input

type IMarkMap =
    abstract TrackedBuffers : ITextBuffer seq
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
    abstract DeleteAllMarksForBuffer : ITextBuffer -> unit


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

    /// Add a given SnapshotPoint to the jump list.  Only works if the SnapshotPoint is against
    /// the current version of the ITextBuffer
    abstract Add : SnapshotPoint -> bool


/// Defines a block style caret for a given ITextView.  This allows normal mode to create 
/// a block style cursor when needed
type IBlockCaret =
    abstract TextView : ITextView
    abstract IsShown : bool
    abstract Show : unit -> unit
    abstract Hide : unit -> unit
    abstract Destroy : unit -> unit

type ProcessResult = 
    | Processed
    | ProcessNotHandled
    | SwitchMode of ModeKind
    | SwitchPreviousMode

/// Vim instance.  Global for a group of buffers
type IVim =
    abstract Host : IVimHost
    abstract MarkMap : IMarkMap
    abstract RegisterMap : IRegisterMap
    abstract Settings : VimSettings
    abstract CreateBuffer : IWpfTextView -> IVimBuffer

    /// Get the IVimBuffer associated with the given view
    abstract GetBuffer : IWpfTextView -> IVimBuffer option

    /// Get the IVimBuffer associated with the given view
    abstract GetBufferForBuffer : ITextBuffer -> IVimBuffer option

    /// Remove the IVimBuffer associated with the given view.  This will not actually close
    /// the IVimBuffer but instead just removes it's association with the given view
    abstract RemoveBuffer : IWpfTextView -> bool
    
/// Main interface for the Vim editor engine so to speak. 
and IVimBuffer =

    /// Name of the buffer.  Used for items like Marks
    abstract Name : string

    /// View of the file
    abstract TextView : IWpfTextView

    /// Underyling ITextBuffer Vim is operating under
    abstract TextBuffer : ITextBuffer
    abstract TextSnapshot : ITextSnapshot
    abstract EditorOperations : IEditorOperations

    /// Owning IVim instance
    abstract Vim : IVim
    abstract MarkMap : IMarkMap

    /// Jump list
    abstract JumpList : IJumpList

    /// Available IBlockCaret implementation for the buffer
    abstract BlockCaret : IBlockCaret

    /// IVimHost for the buffer
    abstract VimHost : IVimHost

    /// ModeKind of the current IMode in the buffer
    abstract ModeKind : ModeKind

    /// Current mode of the buffer
    abstract Mode : IMode

    /// Sequence of available Modes
    abstract AllModes : seq<IMode>

    abstract Settings : VimSettings
    abstract RegisterMap : IRegisterMap

    abstract GetRegister : char -> Register

    /// Get the specified Mode
    abstract GetMode : ModeKind -> IMode
    
    /// Process the char in question and return whether or not it was handled
    abstract ProcessChar : char -> bool
    
    /// Process the key in question.  Returns true if the key was handled by the buffer
    abstract ProcessKey : Key -> bool
    
    /// Process the KeyInput and return whether or not the input was completely handled
    abstract ProcessInput : KeyInput -> bool
    abstract CanProcessInput : KeyInput -> bool
    abstract CanProcessKey : Key -> bool
    abstract SwitchMode : ModeKind -> IMode

    /// Switch the buffer back to the previous mode which is returned
    abstract SwitchPreviousMode : unit -> IMode

    /// Called when the view is closed and the IVimBuffer should uninstall itself
    /// and it's modes
    abstract Close : unit -> unit
    
    /// Raised when the mode is switched
    [<CLIEvent>]
    abstract SwitchedMode : IEvent<IMode>

    /// Raised when a key is processed
    [<CLIEvent>]
    abstract KeyInputProcessed : IEvent<KeyInput>

and IMode =

    /// Owning IVimBuffer
    abstract VimBuffer : IVimBuffer 

    /// What type of Mode is this
    abstract ModeKind : ModeKind

    /// Sequence of commands handled by the Mode.  
    abstract Commands : seq<KeyInput>

    /// Can the mode process this particular KeyIput at the current time
    abstract CanProcess : KeyInput -> bool

    /// Process the given KeyInput
    abstract Process : KeyInput -> ProcessResult

    /// Called when the mode is entered
    abstract OnEnter : unit -> unit

    /// Called when the mode is left
    abstract OnLeave : unit -> unit


