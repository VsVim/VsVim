#light

namespace Vim

type internal DisabledMode(_vimBufferData: IVimBufferData) =

    let _localSettings = _vimBufferData.LocalSettings
    let _globalSettings = _localSettings.GlobalSettings
    
    member x.HelpString = 
        let keyInput = _globalSettings.DisableAllCommand
        if VimKeyModifiers.None = keyInput.KeyModifiers then 
            sprintf "Vim Disabled. Type %s to re-enable" (keyInput.Key.ToString())
        else
            sprintf "Vim Disabled. Type %s+%s to re-enable" (keyInput.Key.ToString()) (keyInput.KeyModifiers.ToString())

    member x.Process (keyInputData: KeyInputData) = 
        if keyInputData.KeyInput = _globalSettings.DisableAllCommand then
            ProcessResult.OfModeKind ModeKind.Normal
        else
            ProcessResult.NotHandled

    interface IDisabledMode with 
        member x.VimTextBuffer = _vimBufferData.VimTextBuffer
        member x.HelpMessage = x.HelpString
        member x.ModeKind = ModeKind.Disabled        
        member x.CommandNames = Seq.singleton (KeyInputSet(_globalSettings.DisableAllCommand))
        member x.CanProcess keyInput = keyInput = _globalSettings.DisableAllCommand
        member x.Process keyInputData = x.Process keyInputData
        member x.OnEnter _  = ()
        member x.OnLeave() = ()
        member x.OnClose() = ()
    

