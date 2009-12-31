#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open System.Windows.Input

type internal ModeMap
    (
        _modeMap : Map<ModeKind,IMode> ) =
    let mutable _mode : IMode option = None
    let _modeSwitchedEvent = new Event<_>()

    member x.SwitchedEvent = _modeSwitchedEvent.Publish
    member x.Mode = Option.get _mode
    member x.Modes = _modeMap |> Map.toSeq |> Seq.map (fun (k,m) -> m)
    member x.SwitchMode kind =
        let old = _mode
        let mode = _modeMap.Item kind
        _mode <- Some mode
        if Option.isSome old then
            (Option.get old).OnLeave()
        mode.OnEnter()
        _modeSwitchedEvent.Trigger(mode)
        mode

type internal VimBuffer 
    (
        _vim : IVim,
        _textView : IWpfTextView,
        _name : string,
        _editorOperations : IEditorOperations,
        _blockCaret : IBlockCaret,
        _markMap : MarkMap ) =

    let mutable _modeMap = ModeMap(Map.empty)
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
                    | SwitchModeNotHandled (kind) ->
                        x.SwitchMode kind |> ignore
                        false
                    | Processed -> true
                    | ProcessNotHandled -> false
        _keyInputProcessedEvent.Trigger(i)
        ret
            
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
        member x.MarkMap = _markMap
        member x.ModeKind = x.Mode.ModeKind
        member x.Modes = _modeMap.Modes
        member x.Settings = _vim.Settings
        member x.RegisterMap = _vim.RegisterMap
        member x.GetRegister c = _vim.RegisterMap.GetRegister c
        member x.ProcessChar c = 
            let ki = InputUtil.CharToKeyInput c
            x.ProcessInput ki
        member x.SwitchMode kind = x.SwitchMode kind
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
            _markMap.DeleteAllMarksForBuffer _textView.TextBuffer
