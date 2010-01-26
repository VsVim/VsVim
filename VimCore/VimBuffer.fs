#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open System.Windows.Input

type internal ModeMap() = 
    let mutable _modeMap : Map<ModeKind,IMode> = Map.empty
    let mutable _mode : IMode option = None
    let mutable _previousMode : IMode option = None
    let _modeSwitchedEvent = new Event<_>()

    member x.SwitchedEvent = _modeSwitchedEvent.Publish
    member x.Mode = Option.get _mode
    member x.Modes = _modeMap |> Map.toSeq |> Seq.map (fun (k,m) -> m)
    member x.SwitchMode kind =
        let prev = _mode
        let mode = _modeMap.Item kind
        _mode <- Some mode
        if Option.isSome prev then
            (Option.get prev).OnLeave()
            _previousMode <- prev
        mode.OnEnter()
        _modeSwitchedEvent.Trigger(mode)
        mode
    member x.SwitchPreviousMode () =
        let prev = Option.get _previousMode
        x.SwitchMode prev.ModeKind
    member x.GetMode kind = Map.find kind _modeMap
    member x.AddMode (mode:IMode) = 
        _modeMap <- Map.add (mode.ModeKind) mode _modeMap

type internal VimBuffer 
    (
        _vim : IVim,
        _textView : IWpfTextView,
        _name : string,
        _editorOperations : IEditorOperations,
        _blockCaret : IBlockCaret) =

    let mutable _modeMap = ModeMap()
    let _keyInputProcessedEvent = new Event<_>()
    
    /// Get the current mode
    member x.Mode = _modeMap.Mode

    /// Switch to the desired mode
    member x.SwitchMode kind = _modeMap.SwitchMode kind

    // Actuall process the input key.  Raise the change event on an actual change
    member x.ProcessInput (i:KeyInput) = 
        let ret = 
            if i = _vim.Settings.DisableCommand && x.Mode.ModeKind <> ModeKind.Disabled then
                x.SwitchMode ModeKind.Disabled |> ignore
                true
            else
                let res = x.Mode.Process i
                match res with
                    | SwitchMode (kind) -> 
                        x.SwitchMode kind |> ignore
                        true
                    | SwitchPreviousMode -> 
                        _modeMap.SwitchPreviousMode() |> ignore
                        true
                    | Processed -> true
                    | ProcessNotHandled -> false
        _keyInputProcessedEvent.Trigger(i)
        ret

    member x.AddMode mode = _modeMap.AddMode mode
            
    member x.CanProcessInput ki = x.Mode.CanProcess ki || ki = _vim.Settings.DisableCommand
                 
    interface IVimBuffer with
        member x.Vim = _vim
        member x.VimHost = _vim.Host
        member x.TextView = _textView
        member x.TextBuffer = _textView.TextBuffer
        member x.TextSnapshot = _textView.TextSnapshot
        member x.BlockCaret = _blockCaret
        member x.EditorOperations = _editorOperations
        member x.Name = _name
        member x.MarkMap = _vim.MarkMap
        member x.ModeKind = x.Mode.ModeKind
        member x.Mode = x.Mode
        member x.AllModes = _modeMap.Modes
        member x.Settings = _vim.Settings
        member x.RegisterMap = _vim.RegisterMap
        member x.GetRegister c = _vim.RegisterMap.GetRegister c
        member x.GetMode kind = _modeMap.GetMode kind
        member x.ProcessChar c = 
            let ki = InputUtil.CharToKeyInput c
            x.ProcessInput ki
        member x.SwitchMode kind = x.SwitchMode kind
        member x.SwitchPreviousMode () = _modeMap.SwitchPreviousMode()
        [<CLIEvent>]
        member x.SwitchedMode = _modeMap.SwitchedEvent
        [<CLIEvent>]
        member x.KeyInputProcessed = _keyInputProcessedEvent.Publish
        member x.ProcessKey k = x.ProcessInput (InputUtil.KeyToKeyInput k)
        member x.ProcessInput ki = x.ProcessInput ki
        member x.CanProcessInput ki = x.CanProcessInput ki
        member x.CanProcessKey k = x.CanProcessInput (InputUtil.KeyToKeyInput k)
        member x.Close () = 
            x.Mode.OnLeave()
            _blockCaret.Destroy()
            _vim.MarkMap.DeleteAllMarksForBuffer (x :> IVimBuffer)
            _vim.RemoveBuffer _textView |> ignore
