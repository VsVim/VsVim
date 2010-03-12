#light

namespace Vim

type internal DisabledMode( _data : IVimBuffer ) =
    
    member private x.HelpString = 
        let ki = _data.Settings.GlobalSettings.DisableCommand
        if KeyModifiers.None = ki.KeyModifiers then 
            sprintf "Vim Disabled. Type %s to re-enable" (ki.Key.ToString())
        else
            sprintf "Vim Disabled. Type %s+%s to re-enable" (ki.Key.ToString()) (ki.KeyModifiers.ToString())

    interface IMode with 
        member x.VimBuffer = _data
        member x.ModeKind = ModeKind.Disabled        
        member x.Commands = Seq.singleton _data.Settings.GlobalSettings.DisableCommand
        member x.CanProcess ki = 
            _data.VimHost.UpdateStatus(x.HelpString)
            ki = _data.Settings.GlobalSettings.DisableCommand
        member x.Process ki = 
            if ki = _data.Settings.GlobalSettings.DisableCommand then
                ProcessResult.SwitchMode ModeKind.Normal
            else
                ProcessResult.ProcessNotHandled
        member x.OnEnter() = 
            _data.VimHost.UpdateStatus(x.HelpString)
            ()
        member x.OnLeave() = 
            _data.VimHost.UpdateStatus(System.String.Empty)
            ()
    
    

