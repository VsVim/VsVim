#light

namespace Vim

type internal DisabledMode( _buffer : IVimBuffer ) =

    let _localSettings = _buffer.LocalSettings
    let _globalSettings = _localSettings.GlobalSettings
    
    member private x.HelpString = 
        let ki = _globalSettings.DisableCommand
        if KeyModifiers.None = ki.KeyModifiers then 
            sprintf "Vim Disabled. Type %s to re-enable" (ki.Key.ToString())
        else
            sprintf "Vim Disabled. Type %s+%s to re-enable" (ki.Key.ToString()) (ki.KeyModifiers.ToString())

    interface IDisabledMode with 
        member x.VimBuffer = _buffer
        member x.HelpMessage = x.HelpString
        member x.ModeKind = ModeKind.Disabled        
        member x.CommandNames = Seq.singleton _globalSettings.DisableCommand |> Seq.map OneKeyInput
        member x.CanProcess ki = ki = _globalSettings.DisableCommand
        member x.Process ki = 
            if ki = _globalSettings.DisableCommand then
                ProcessResult.OfModeKind ModeKind.Normal
            else
                ProcessResult.NotHandled
        member x.OnEnter _  = ()
        member x.OnLeave() = ()
        member x.OnClose() = ()
    

