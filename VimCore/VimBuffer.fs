#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities

type internal ModeMap() = 
    let mutable _modeMap : Map<ModeKind,IMode> = Map.empty
    let mutable _mode : IMode option = None
    let mutable _previousMode : IMode option = None
    let _modeSwitchedEvent = new Event<_>()

    member x.SwitchedEvent = _modeSwitchedEvent.Publish
    member x.Mode = Option.get _mode
    member x.Modes = _modeMap |> Map.toSeq |> Seq.map (fun (k,m) -> m)
    member x.SwitchMode kind arg =
        let prev = _mode
        let mode = _modeMap.Item kind
        _mode <- Some mode
        if Option.isSome prev then
            (Option.get prev).OnLeave()
            _previousMode <- prev
        mode.OnEnter arg
        _modeSwitchedEvent.Trigger(mode)
        mode
    member x.SwitchPreviousMode () =
        let prev = Option.get _previousMode
        x.SwitchMode prev.ModeKind ModeArgument.None
    member x.GetMode kind = Map.find kind _modeMap
    member x.AddMode (mode:IMode) = 
        _modeMap <- Map.add (mode.ModeKind) mode _modeMap

type internal VimBuffer 
    (
        _vim : IVim,
        _textView : ITextView,
        _jumpList : IJumpList,
        _settings : IVimLocalSettings ) =

    let _properties = PropertyCollection()
    let mutable _modeMap = ModeMap()
    let mutable _isProcessingInput = false
    let mutable _isClosed = false

    /// This is the buffered input when a remap request needs more than one 
    /// element
    let mutable _remapInput : KeyInputSet option = None


    let _keyInputProcessedEvent = new Event<_>()
    let _keyInputStartEvent = new Event<_>()
    let _keyInputEndEvent = new Event<_>()
    let _keyInputBufferedEvent = new Event<_>()
    let _errorMessageEvent = new Event<_>()
    let _statusMessageEvent = new Event<_>()
    let _statusMessageLongEvent = new Event<_>()
    let _closedEvent = new Event<_>()

    member x.BufferedRemapKeyInputs =
        match _remapInput with
        | None -> List.empty
        | Some(keyInputSet) -> keyInputSet.KeyInputs
    
    /// Get the current mode
    member x.Mode = _modeMap.Mode
    member x.NormalMode = _modeMap.GetMode ModeKind.Normal :?> INormalMode
    member x.VisualLineMode = _modeMap.GetMode ModeKind.VisualLine :?> IVisualMode
    member x.VisualCharacterMode = _modeMap.GetMode ModeKind.VisualCharacter :?> IVisualMode
    member x.VisualBlockMode = _modeMap.GetMode ModeKind.VisualBlock :?> IVisualMode
    member x.CommandMode = _modeMap.GetMode ModeKind.Command :?> ICommandMode
    member x.InsertMode = _modeMap.GetMode ModeKind.Insert 
    member x.ReplaceMode = _modeMap.GetMode ModeKind.Replace
    member x.SubstituteConfirmMode = _modeMap.GetMode ModeKind.SubstituteConfirm :?> ISubstituteConfirmMode
    member x.DisabledMode = _modeMap.GetMode ModeKind.Disabled :?> IDisabledMode

    /// Current KeyRemapMode which should be used when calculating keyboard mappings
    member x.KeyRemapMode = 
        match _modeMap.Mode.ModeKind with
        | ModeKind.Insert -> Some (KeyRemapMode.Insert)
        | ModeKind.Replace -> Some (KeyRemapMode.Insert)
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

    /// Switch to the desired mode
    member x.SwitchMode kind arg = _modeMap.SwitchMode kind arg

    // Actuall process the input key.  Raise the change event on an actual change
    member x.Process (i:KeyInput) : bool = 

        // Actually process the given piece of input
        let doProcess i = 
            let ret,res = 
                _isProcessingInput <- true 
                try
                    if i = _vim.Settings.DisableCommand && x.Mode.ModeKind <> ModeKind.Disabled then
                        x.SwitchMode ModeKind.Disabled ModeArgument.None |> ignore
                        true,SwitchMode(ModeKind.Disabled)
                    else
                        let res = x.Mode.Process i
                        let ret = 
                            match res with
                            | SwitchMode (kind) -> 
                                x.SwitchMode kind ModeArgument.None |> ignore
                                true
                            | SwitchModeWithArgument (kind,arg) ->
                                x.SwitchMode kind arg |> ignore
                                true
                            | SwitchPreviousMode -> 
                                _modeMap.SwitchPreviousMode() |> ignore
                                true
                            | Processed -> true
                            | ProcessNotHandled -> false
                        ret,res
                finally
                    _isProcessingInput <- false
            _keyInputProcessedEvent.Trigger(i,res)
            ret

        // Calculate the current remapMode
        let remapMode = x.KeyRemapMode

        // Raise the event that we recieved the key
        _keyInputStartEvent.Trigger i

        try
            let remapResult,keyInputSet = 
                match _remapInput,remapMode with
                | Some(buffered),Some(remapMode) -> 
                    let keyInputSet = buffered.Add(i)
                    (_vim.KeyMap.GetKeyMapping keyInputSet remapMode),keyInputSet
                | Some(buffered),None -> 
                    let keyInputSet = buffered.Add(i)
                    (Mapped keyInputSet),keyInputSet
                | None,Some(remapMode) -> 
                    let keyInputSet = OneKeyInput i
                    (_vim.KeyMap.GetKeyMapping keyInputSet remapMode,keyInputSet)
                | None,None -> 
                    let keyInputSet = OneKeyInput i
                    (Mapped keyInputSet),keyInputSet
    
            // Clear out the _remapInput at this point.  It will be reset if the mapping needs more 
            // data
            _remapInput <- None
    
            match remapResult with
            | NoMapping -> doProcess i 
            | MappingNeedsMoreInput -> 
                _remapInput <- Some keyInputSet
                _keyInputBufferedEvent.Trigger i
                true
            | RecursiveMapping(_) -> 
                x.RaiseErrorMessage Resources.Vim_RecursiveMapping
                true
            | Mapped(keyInputSet) -> keyInputSet.KeyInputs |> Seq.map doProcess |> SeqUtil.last
        finally 
            _keyInputEndEvent.Trigger i
    
    member x.AddMode mode = _modeMap.AddMode mode
            
    member x.CanProcess ki =  
        let ki = 
            match x.KeyRemapMode with 
            | None -> ki
            | Some(remapMode) ->
                match _vim.KeyMap.GetKeyMapping (OneKeyInput ki) remapMode with
                | Mapped(keyInputSet) -> 
                    match keyInputSet.FirstKeyInput with
                    | Some(mapped) -> mapped
                    | None -> ki
                | NoMapping -> ki
                | MappingNeedsMoreInput -> ki
                | RecursiveMapping(_) -> ki
        x.Mode.CanProcess ki || ki = _vim.Settings.DisableCommand

    member x.RaiseErrorMessage msg = _errorMessageEvent.Trigger msg
    member x.RaiseStatusMessage msg = _statusMessageEvent.Trigger msg
    member x.RaiseStatusMessageLong msgSeq = _statusMessageLongEvent.Trigger msgSeq
                 
    interface IVimBuffer with
        member x.Vim = _vim
        member x.VimData = _vim.VimData
        member x.TextView = _textView
        member x.TextBuffer = _textView.TextBuffer
        member x.TextSnapshot = _textView.TextSnapshot
        member x.BufferedRemapKeyInputs = x.BufferedRemapKeyInputs 
        member x.IsProcessingInput = _isProcessingInput
        member x.Name = _vim.VimHost.GetName _textView.TextBuffer
        member x.MarkMap = _vim.MarkMap
        member x.JumpList = _jumpList
        member x.ModeKind = x.Mode.ModeKind
        member x.Mode = x.Mode
        member x.NormalMode = x.NormalMode
        member x.VisualLineMode = x.VisualLineMode
        member x.VisualCharacterMode = x.VisualCharacterMode
        member x.VisualBlockMode = x.VisualBlockMode
        member x.CommandMode = x.CommandMode
        member x.InsertMode = x.InsertMode
        member x.ReplaceMode = x.ReplaceMode
        member x.SubstituteConfirmMode = x.SubstituteConfirmMode
        member x.DisabledMode = x.DisabledMode
        member x.AllModes = _modeMap.Modes
        member x.Settings = _settings
        member x.RegisterMap = _vim.RegisterMap
        member x.GetRegister name = _vim.RegisterMap.GetRegister name
        member x.GetMode kind = _modeMap.GetMode kind
        member x.SwitchMode kind arg = x.SwitchMode kind arg
        member x.SwitchPreviousMode () = _modeMap.SwitchPreviousMode()

        [<CLIEvent>]
        member x.SwitchedMode = _modeMap.SwitchedEvent
        [<CLIEvent>]
        member x.KeyInputProcessed = _keyInputProcessedEvent.Publish
        [<CLIEvent>]
        member x.KeyInputStart = _keyInputStartEvent.Publish
        [<CLIEvent>]
        member x.KeyInputEnd = _keyInputEndEvent.Publish
        [<CLIEvent>]
        member x.KeyInputBuffered = _keyInputBufferedEvent.Publish
        [<CLIEvent>]
        member x.ErrorMessage = _errorMessageEvent.Publish
        [<CLIEvent>]
        member x.StatusMessage = _statusMessageEvent.Publish
        [<CLIEvent>]
        member x.StatusMessageLong = _statusMessageLongEvent.Publish
        [<CLIEvent>]
        member x.Closed = _closedEvent.Publish

        member x.Process ki = x.Process ki
        member x.CanProcess ki = x.CanProcess ki
        member x.Close () = 
            if _isClosed then invalidOp Resources.VimBuffer_AlreadyClosed
            else
                try
                    x.Mode.OnLeave()
                    _modeMap.Modes |> Seq.iter (fun x -> x.OnClose())
                    _vim.RemoveBuffer _textView |> ignore
                    _closedEvent.Trigger System.EventArgs.Empty
                finally 
                    _isClosed <- true

    interface IPropertyOwner with
        member x.Properties = _properties
