#light

namespace VimCore

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input

type VimBuffer 
    (
        _data:IVimBufferData,
        _modeMap : Map<ModeKind,IMode>) =
    let _modeSwitchedEvent = new Event<_>()
    let _keyInputProcessedEvent = new Event<_>()
    let mutable _mode = _modeMap.Item ModeKind.Normal
    do  _mode.OnEnter()
    
    /// Get the current mode
    member x.Mode = _mode

    member x.SwitchMode kind =
        let old = _mode
        _mode <- _modeMap.Item kind 
        old.OnLeave()
        _mode.OnEnter()
        _modeSwitchedEvent.Trigger(_mode)
        _mode
        
    // Actuall process the input key.  Raise the change event on an actual change
    member x.ProcessInput (i:KeyInput) = 
        let inner = 
            let res = _mode.Process i
            match res with
                | SwitchMode (kind) -> 
                    x.SwitchMode kind |> ignore
                    true
                | SwitchModeNotHandled (kind) ->
                    x.SwitchMode kind |> ignore
                    false
                | Processed -> true
                | ProcessNotHandled -> false
        let ret = inner 
        _keyInputProcessedEvent.Trigger(i)
        ret
            
    member x.WillProcessInput ki = _mode.CanProcess ki
                 
    interface IVimBuffer with
        member x.VimBufferData = _data
        member x.VimHost = _data.VimHost
        member x.TextView = _data.TextView
        member x.ModeKind = _mode.ModeKind
        member x.Modes = _modeMap |> Map.toSeq |> Seq.map (fun (k,m) -> m)
        member x.Settings = _data.Settings
        member x.RegisterMap = _data.RegisterMap
        member x.GetRegister c = _data.RegisterMap.GetRegister c
        member x.ProcessChar c = 
            let ki = InputUtil.CharToKeyInput c
            x.ProcessInput ki
        member x.SwitchMode kind = x.SwitchMode kind
        [<CLIEvent>]
        member x.SwitchedMode = _modeSwitchedEvent.Publish
        [<CLIEvent>]
        member x.KeyInputProcessed = _keyInputProcessedEvent.Publish
        member x.ProcessKey k = x.ProcessInput (InputUtil.KeyToKeyInput k)
        member x.ProcessInput ki = x.ProcessInput ki
        member x.WillProcessInput ki = x.WillProcessInput ki
        member x.WillProcessKey k = x.WillProcessInput (InputUtil.KeyToKeyInput k)
