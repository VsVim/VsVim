#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

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
        _textView : ITextView,
        _editorOperations : IEditorOperations,
        _jumpList : IJumpList,
        _settings : IVimLocalSettings ) =

    let mutable _modeMap = ModeMap()

    /// This is the buffered input when a remap request needs more than one 
    /// element
    let mutable _remapInput : KeyInput list option = None

    let _keyInputProcessedEvent = new Event<_>()
    let _keyInputReceivedEvent = new Event<_>()

    member x.BufferedRemapKeyInputs =
        match _remapInput with
        | None -> List.empty
        | Some(keyInputs) -> keyInputs
    
    /// Get the current mode
    member x.Mode = _modeMap.Mode
    member x.NormalMode = _modeMap.GetMode ModeKind.Normal :?> INormalMode
    member x.CommandMode = _modeMap.GetMode ModeKind.Command :?> ICommandMode

    /// Switch to the desired mode
    member x.SwitchMode kind = _modeMap.SwitchMode kind

    // Actuall process the input key.  Raise the change event on an actual change
    member x.ProcessInput (i:KeyInput) : bool = 

        // Actually process the given piece of input
        let doProcess i = 
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

        // Calculate the current remapMode
        let remapMode = 
            match _modeMap.Mode.ModeKind with
            | ModeKind.Insert -> Some (KeyRemapMode.Insert)
            | ModeKind.Normal -> 
                let mode = x.NormalMode
                if mode.IsOperatorPending then Some(KeyRemapMode.OperatorPending)
                elif mode.IsWaitingForInput then None
                else Some(KeyRemapMode.Normal)
            | ModeKind.Command -> Some(KeyRemapMode.Command)
            | ModeKind.VisualBlock -> Some(KeyRemapMode.Visual)
            | ModeKind.VisualCharacter -> Some(KeyRemapMode.Visual)
            | ModeKind.VisualLine -> Some(KeyRemapMode.Visual)
            | _ -> None

        // Raise the event that we recieved the key
        _keyInputReceivedEvent.Trigger(i)

        let remapResult,keyInputs = 
            match _remapInput,remapMode with
            | Some(buffered),Some(remapMode) -> 
                let keyInputs = buffered |> SeqUtil.appendSingle i 
                (_vim.KeyMap.GetKeyMappingResultFromMultiple keyInputs remapMode),keyInputs
            | Some(buffered),None -> 
                let keyInputs = buffered |> SeqUtil.appendSingle i 
                KeySequence(keyInputs),keyInputs
            | None,Some(remapMode) -> (_vim.KeyMap.GetKeyMappingResult i remapMode,Seq.singleton i)
            | None,None -> (SingleKey(i),Seq.singleton(i))

        // Clear out the _remapInput at this point.  It will be reset if the mapping needs more 
        // data
        _remapInput <- None

        match remapResult with
        | SingleKey(ki) -> doProcess ki
        | NoMapping -> doProcess i 
        | MappingNeedsMoreInput -> 
            _remapInput <- keyInputs |> List.ofSeq |> Some
            true
        | RecursiveMapping(_) -> 
            _vim.Host.UpdateStatus Resources.Vim_RecursiveMapping
            true
        | KeySequence(kiSeq) -> kiSeq |> Seq.map doProcess |> SeqUtil.last
    
    member x.AddMode mode = _modeMap.AddMode mode
            
    member x.CanProcessInput ki = x.Mode.CanProcess ki || ki = _vim.Settings.DisableCommand
                 
    interface IVimBuffer with
        member x.Vim = _vim
        member x.VimHost = _vim.Host
        member x.TextView = _textView
        member x.TextBuffer = _textView.TextBuffer
        member x.TextSnapshot = _textView.TextSnapshot
        member x.BufferedRemapKeyInputs = x.BufferedRemapKeyInputs 
        member x.EditorOperations = _editorOperations
        member x.Name = _vim.Host.GetName _textView.TextBuffer
        member x.MarkMap = _vim.MarkMap
        member x.JumpList = _jumpList
        member x.ModeKind = x.Mode.ModeKind
        member x.Mode = x.Mode
        member x.NormalMode = x.NormalMode
        member x.CommandMode = x.CommandMode
        member x.AllModes = _modeMap.Modes
        member x.Settings = _settings
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
        [<CLIEvent>]
        member x.KeyInputReceived = _keyInputReceivedEvent.Publish
        member x.ProcessInput ki = x.ProcessInput ki
        member x.CanProcessInput ki = x.CanProcessInput ki
        member x.Close () = 
            x.Mode.OnLeave()
            _vim.MarkMap.DeleteAllMarksForBuffer _textView.TextBuffer
            _vim.RemoveBuffer _textView |> ignore
