#light

namespace Vim

type internal ExternalEditMode(_vimBufferData: IVimBufferData) =
    
    member x.ClearSelection () =
        let textView = _vimBufferData.TextView
        let start = textView.Selection.Start
        textView.Selection.Clear()
        TextViewUtil.MoveCaretToVirtualPoint textView start

    interface IMode with 
        member x.VimTextBuffer = _vimBufferData.VimTextBuffer
        member x.ModeKind = ModeKind.ExternalEdit
        member x.CommandNames = Seq.empty
        member x.CanProcess keyInput =
            keyInput = KeyInputUtil.EscapeKey 
            || keyInput = KeyInputUtil.CharWithControlToKeyInput 'c'
        member x.Process keyInputData = 
            if keyInputData.KeyInput = KeyInputUtil.EscapeKey then
                x.ClearSelection()
                ModeKind.Normal
                |> ModeSwitch.SwitchMode
                |> ProcessResult.Handled
            elif keyInputData.KeyInput = KeyInputUtil.CharWithControlToKeyInput 'c' then
                x.ClearSelection()
                (ModeKind.Normal, ModeArgument.CancelOperation)
                |> ModeSwitch.SwitchModeWithArgument
                |> ProcessResult.Handled
            else
                ProcessResult.NotHandled
        member x.OnEnter _  = ()
        member x.OnLeave() = ()
        member x.OnClose() = ()


