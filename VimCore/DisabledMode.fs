#light

namespace Vim

type internal DisabledMode( _data : IVimBuffer ) =
    
    member private x.HelpString = 
        let ki = _data.Settings.GlobalSettings.DisableCommand
        if KeyModifiers.None = ki.KeyModifiers then 
            sprintf "Vim Disabled. Type %s to re-enable" (ki.Key.ToString())
        else
            sprintf "Vim Disabled. Type %s+%s to re-enable" (ki.Key.ToString()) (ki.KeyModifiers.ToString())

    interface IDisabledMode with 
        member x.VimBuffer = _data
        member x.HelpMessage = x.HelpString
        member x.ModeKind = ModeKind.Disabled        
        member x.CommandNames = Seq.singleton _data.Settings.GlobalSettings.DisableCommand |> Seq.map OneKeyInput
        member x.CanProcess ki = 
            ki = _data.Settings.GlobalSettings.DisableCommand
        member x.Process ki = 
            if ki = _data.Settings.GlobalSettings.DisableCommand then
                ProcessResult.SwitchMode ModeKind.Normal
            else
                ProcessResult.ProcessNotHandled
        member x.OnEnter _  = ()
        member x.OnLeave() = ()
        member x.OnClose() = ()
    
    

