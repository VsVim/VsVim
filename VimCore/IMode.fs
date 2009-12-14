#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input

type ProcessResult = 
    | Processed
    | ProcessNotHandled
    | SwitchMode of ModeKind
    | SwitchModeNotHandled of ModeKind

type IMode =

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
