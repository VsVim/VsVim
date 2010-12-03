#light

namespace Vim

type internal ExternalEditMode( _data : IVimBuffer ) =
    
    interface IMode with 
        member x.VimBuffer = _data
        member x.ModeKind = ModeKind.ExternalEdit
        member x.CommandNames = Seq.empty
        member x.CanProcess ki = ki = KeyInputUtil.VimKeyToKeyInput VimKey.Escape
        member x.Process ki = 
            if ki = KeyInputUtil.VimKeyToKeyInput VimKey.Escape then
                ProcessResult.SwitchMode ModeKind.Insert
            else
                ProcessResult.ProcessNotHandled
        member x.OnEnter _  = ()
        member x.OnLeave() = ()
        member x.OnClose() = ()


