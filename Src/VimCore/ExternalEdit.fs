#light

namespace Vim

type internal ExternalEditMode(_vimBufferData: IVimBufferData) =
    
    member x.ClearSelection () =
        let textView = _vimBufferData.TextView
        let start = textView.Selection.Start
        TextViewUtil.ClearSelection textView
        TextViewUtil.MoveCaretToVirtualPoint textView start

    interface IMode with 
        member x.VimTextBuffer = _vimBufferData.VimTextBuffer
        member x.ModeKind = ModeKind.ExternalEdit
        member x.CommandNames = Seq.empty
        member x.CanProcess keyInput =
            keyInput = KeyInputUtil.EscapeKey 
            || keyInput = KeyInputUtil.CharWithControlToKeyInput 'c'
        member x.Process keyInputData = 
            match keyInputData.KeyInput with
            | keyInput when keyInput = KeyInputUtil.EscapeKey ->
                x.ClearSelection()
                ModeKind.Normal
                |> ModeSwitch.SwitchMode
                |> ProcessResult.Handled
            | keyInput when keyInput = KeyInputUtil.CharWithControlToKeyInput 'c' ->
                x.ClearSelection()
                (ModeKind.Normal, ModeArgument.CancelOperation)
                |> ModeSwitch.SwitchModeWithArgument
                |> ProcessResult.Handled
            | _ ->
                ProcessResult.NotHandled
        member x.OnEnter _  = ()
        member x.OnLeave() = ()
        member x.OnClose() = ()


