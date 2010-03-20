#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

type internal ChangeTracker() =

    let mutable _last : RepeatableChange option = None
    
    member private x.OnVimBufferCreated (buffer:IVimBuffer) =
        buffer.NormalMode.CommandExecuted |> Event.add x.OnCommandExecuted

    member private x.OnCommandExecuted command = 
        match command with
        | NonRepeatableCommand -> _last <- None
        | RepeatableCommand(keyInputs,count,reg) -> _last <- NormalModeChange(keyInputs,count,reg) |> Some

    interface IVimBufferCreationListener with
        member x.VimBufferCreated buffer = x.OnVimBufferCreated buffer

    interface IChangeTracker with
        member x.LastChange = _last