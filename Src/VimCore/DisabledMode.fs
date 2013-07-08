#light

namespace Vim

type internal DisabledMode(_vimBufferData : IVimBufferData) =

    let _localSettings = _vimBufferData.LocalSettings
    let _globalSettings = _localSettings.GlobalSettings
    
    member x.HelpString = 
        let ki = _globalSettings.DisableAllCommand
        if KeyModifiers.None = ki.KeyModifiers then 
            sprintf "Vim Disabled. Type %s to re-enable" (ki.Key.ToString())
        else
            sprintf "Vim Disabled. Type %s+%s to re-enable" (ki.Key.ToString()) (ki.KeyModifiers.ToString())

    member x.Process keyInput = 
        if keyInput = _globalSettings.DisableAllCommand then
            ProcessResult.OfModeKind ModeKind.Normal
        else
            ProcessResult.NotHandled

    interface IDisabledMode with 
        member x.VimTextBuffer = _vimBufferData.VimTextBuffer
        member x.HelpMessage = x.HelpString
        member x.ModeKind = ModeKind.Disabled        
        member x.CommandNames = Seq.singleton _globalSettings.DisableAllCommand |> Seq.map KeyInputSet.OneKeyInput
        member x.CanProcess ki = ki = _globalSettings.DisableAllCommand
        member x.Process keyInput = x.Process keyInput
        member x.OnEnter _  = ()
        member x.OnLeave() = ()
        member x.OnClose() = ()
    

