#light

namespace Vim

type internal ExternalEditMode(_vimBufferData : IVimBufferData) =
    
    interface IMode with 
        member x.VimTextBuffer = _vimBufferData.VimTextBuffer
        member x.ModeKind = ModeKind.ExternalEdit
        member x.CommandNames = Seq.empty
        member x.CanProcess ki = ki = KeyInputUtil.EscapeKey
        member x.Process ki = 
            if ki = KeyInputUtil.EscapeKey then
                ProcessResult.OfModeKind ModeKind.Normal
            else
                ProcessResult.NotHandled
        member x.OnEnter _  = ()
        member x.OnLeave() = ()
        member x.OnClose() = ()


