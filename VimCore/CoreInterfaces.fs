#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open System.Windows.Input

type ProcessResult = 
    | Processed
    | ProcessNotHandled
    | SwitchMode of ModeKind
    | SwitchPreviousMode

/// Vim instance.  Global for a group of buffers
type IVim =
    abstract Host : IVimHost
    abstract MarkMap : MarkMap
    abstract RegisterMap : IRegisterMap
    abstract Settings : VimSettings
    abstract Buffers : seq<IVimBuffer>
    abstract CreateBuffer : IWpfTextView -> bufferName:string -> IVimBuffer
    
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
    abstract MarkMap : MarkMap

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
    
    // Process the char in question and return whether or not it was handled
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


