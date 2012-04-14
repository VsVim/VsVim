namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Vim
open Vim.Modes

type internal SelectMode
    (
        _vimBufferData : IVimBufferData,
        _operations : ICommonOperations
    ) = 

    let _vimTextBuffer = _vimBufferData.VimTextBuffer

    member x.Process keyInput = 
        if keyInput = KeyInputUtil.EscapeKey then
            ProcessResult.Handled ModeSwitch.SwitchPreviousMode
        else
            ProcessResult.Handled ModeSwitch.NoSwitch

    member x.OnEnter() = ()
    member x.OnLeave() = ()
    member x.OnClose() = ()

    interface IMode with
        member x.VimTextBuffer = _vimTextBuffer
        member x.CommandNames = Seq.empty
        member x.ModeKind = ModeKind.Select
        member x.CanProcess _ = true
        member x.Process keyInput =  x.Process keyInput
        member x.OnEnter _ = x.OnEnter()
        member x.OnLeave () = x.OnLeave()
        member x.OnClose() = x.OnClose()

