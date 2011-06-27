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

        match prev with
        | None ->
            () 
        | Some prev ->
            prev.OnLeave()

            // When switching between different visual modes we don't want to lose
            // the previous non-visual mode value.  Commands executing in Visual mode
            // which return a SwitchPrevious mode value expected to actually leave 
            // Visual Mode 
            match _previousMode with
            | None -> 
                _previousMode <- Some prev
            | Some mode -> 
                if not (VisualKind.IsAnyVisual prev.ModeKind) && not (VisualKind.IsAnyVisual mode.ModeKind) then
                    _previousMode <- Some prev

        mode.OnEnter arg
        _modeSwitchedEvent.Trigger(SwitchModeEventArgs(prev, mode))
        mode
    member x.SwitchPreviousMode () =
        let prev = Option.get _previousMode
        x.SwitchMode prev.ModeKind ModeArgument.None
    member x.GetMode kind = Map.find kind _modeMap
    member x.AddMode (mode : IMode) = 
        _modeMap <- Map.add (mode.ModeKind) mode _modeMap
    member x.RemoveMode (mode : IMode) = 
        _modeMap <- Map.remove mode.ModeKind _modeMap

type internal VimBuffer 
    (
        _bufferData : VimBufferData,
        _incrementalSearch : IIncrementalSearch,
        _motionUtil : IMotionUtil,
        _wordNavigator : ITextStructureNavigator
    ) =

    let _vim = _bufferData.Vim
    let _textView = _bufferData.TextView
    let _jumpList = _bufferData.JumpList
    let _localSettings = _bufferData.LocalSettings
    let _undoRedoOperations = _bufferData.UndoRedoOperations
    let _statusUtil = _bufferData.StatusUtil
    let _properties = PropertyCollection()
    let mutable _modeMap = ModeMap()
    let mutable _processingInputCount = 0
    let mutable _isClosed = false

    /// This is the buffered input when a remap request needs more than one 
    /// element
    let mutable _remapInput : KeyInputSet option = None

    let _keyInputProcessedEvent = new Event<_>()
    let _keyInputStartEvent = new Event<_>()
    let _keyInputEndEvent = new Event<_>()
    let _keyInputBufferedEvent = new Event<_>()
    let _errorMessageEvent = new Event<_>()
    let _warningMessageEvent = new Event<_>()
    let _statusMessageEvent = new Event<_>()
    let _statusMessageLongEvent = new Event<_>()
    let _closedEvent = new Event<_>()

    member x.BufferedRemapKeyInputs =
        match _remapInput with
        | None -> List.empty
        | Some (keyInputSet) -> keyInputSet.KeyInputs
    
    /// Get the current mode
    member x.Mode = _modeMap.Mode
    member x.NormalMode = _modeMap.GetMode ModeKind.Normal :?> INormalMode
    member x.VisualLineMode = _modeMap.GetMode ModeKind.VisualLine :?> IVisualMode
    member x.VisualCharacterMode = _modeMap.GetMode ModeKind.VisualCharacter :?> IVisualMode
    member x.VisualBlockMode = _modeMap.GetMode ModeKind.VisualBlock :?> IVisualMode
    member x.CommandMode = _modeMap.GetMode ModeKind.Command :?> ICommandMode
    member x.InsertMode = _modeMap.GetMode ModeKind.Insert  :?> IInsertMode
    member x.ReplaceMode = _modeMap.GetMode ModeKind.Replace :?> IInsertMode
    member x.SubstituteConfirmMode = _modeMap.GetMode ModeKind.SubstituteConfirm :?> ISubstituteConfirmMode
    member x.DisabledMode = _modeMap.GetMode ModeKind.Disabled :?> IDisabledMode
    member x.ExternalEditMode = _modeMap.GetMode ModeKind.ExternalEdit 

    /// Current KeyRemapMode which should be used when calculating keyboard mappings
    member x.KeyRemapMode = 
        match _modeMap.Mode.ModeKind with
        | ModeKind.Insert -> Some (KeyRemapMode.Insert)
        | ModeKind.Replace -> Some (KeyRemapMode.Insert)
        | ModeKind.Normal -> Some x.NormalMode.KeyRemapMode
        | ModeKind.Command -> Some(KeyRemapMode.Command)
        | ModeKind.VisualBlock -> Some(KeyRemapMode.Visual)
        | ModeKind.VisualCharacter -> Some(KeyRemapMode.Visual)
        | ModeKind.VisualLine -> Some(KeyRemapMode.Visual)
        | _ -> None

    member x.VimBufferData = _bufferData

    /// Switch to the desired mode
    member x.SwitchMode kind arg = _modeMap.SwitchMode kind arg

    /// Add an IMode into the IVimBuffer instance
    member x.AddMode mode = _modeMap.AddMode mode

    /// Remove an IMode from the IVimBuffer instance
    member x.RemoveMode mode = _modeMap.RemoveMode mode

    /// Returns both the mapping of the KeyInput value and the set of inputs which were
    /// considered to get the mapping.  This does account for buffered KeyInput values
    member x.GetKeyInputMappingCore keyInput =
        match _remapInput, x.KeyRemapMode with
        | Some buffered, Some remapMode -> 
            let keyInputSet = buffered.Add keyInput
            (_vim.KeyMap.GetKeyMapping keyInputSet remapMode), keyInputSet
        | Some buffered, None -> 
            let keyInputSet = buffered.Add keyInput
            (KeyMappingResult.Mapped keyInputSet), keyInputSet
        | None, Some remapMode -> 
            let keyInputSet = OneKeyInput keyInput
            _vim.KeyMap.GetKeyMapping keyInputSet remapMode, keyInputSet
        | None, None -> 
            let keyInputSet = OneKeyInput keyInput
            (KeyMappingResult.Mapped keyInputSet), keyInputSet

    /// Get the correct mapping of the given KeyInput value in the current state of the 
    /// IVimBuffer.  This will consider any buffered KeyInput values 
    member x.GetKeyInputMapping keyInput =
        x.GetKeyInputMappingCore keyInput |> fst

    /// Can the KeyInput value be processed in the given the current state of the
    /// IVimBuffer
    member x.CanProcessCore keyInput allowDirectInsert =  

        // Is this KeyInput going to be used for direct insert
        let isDirectInsert keyInput = 
            match x.Mode.ModeKind with
            | ModeKind.Insert -> x.InsertMode.IsDirectInsert keyInput
            | ModeKind.Replace -> x.ReplaceMode.IsDirectInsert keyInput
            | _ -> false

        // Can the given KeyInput be processed as a command or potentially a 
        // direct insert
        let canProcess keyInput = 
            if keyInput = _vim.Settings.DisableCommand then
                // The disable command can be processed at all times
                true
            elif x.Mode.CanProcess keyInput then
                allowDirectInsert || not (isDirectInsert keyInput)
            else
                false

        let keyMappingResult, keyInputSet = x.GetKeyInputMappingCore keyInput
        match keyMappingResult with
        | KeyMappingResult.Mapped keyInputSet -> 
            match keyInputSet.FirstKeyInput with
            | Some keyInput -> canProcess keyInput
            | None -> false
        | KeyMappingResult.NoMapping -> 
            // Simplest case.  There is no mapping so just consider the first character
            // of the input.  
            //
            // Note: This is not necessarily the provided KeyInput.  There could be several
            // buffered KeyInput values which are around because the matched the prefix of a
            // mapping which this KeyInput has broken.  So the first KeyInput we would
            // process is the first buffered KeyInput
            match keyInputSet.FirstKeyInput with
            | Some keyInput -> canProcess keyInput
            | None -> false
        | KeyMappingResult.NeedsMoreInput -> 
            // If this will simply further a key mapping then yes it can be processed
            // now
            true
        | KeyMappingResult.Recursive -> 
            // Even though this will eventually result in an error it can certainly
            // be processed now
            true

    /// Can the KeyInput value be processed in the given the current state of the
    /// IVimBuffer
    member x.CanProcess keyInput = x.CanProcessCore keyInput true

    /// Can the passed in KeyInput be processed as a Vim command by the current state of
    /// the IVimBuffer.  The provided KeyInput will participate in remapping based on the
    /// current mode
    ///
    /// This is very similar to CanProcess except it will return false for any KeyInput
    /// which would be processed as a direct insert.  In other words commands like 'a',
    /// 'b' when handled by insert / replace mode
    member x.CanProcessAsCommand keyInput = x.CanProcessCore keyInput false

    /// Actually process the input key.  Raise the change event on an actual change
    member x.Process (keyInput : KeyInput) =

        // Actually process the given KeyInput value
        let doProcess keyInput =
            let processResult = 
                _processingInputCount <- _processingInputCount + 1
                try
                    if keyInput = _vim.Settings.DisableCommand && x.Mode.ModeKind <> ModeKind.Disabled then
                        x.SwitchMode ModeKind.Disabled ModeArgument.None |> ignore
                        ProcessResult.OfModeKind ModeKind.Disabled
                    else
                        let result = x.Mode.Process keyInput
                        match result with
                        | ProcessResult.Handled modeSwitch ->
                            match modeSwitch with
                            | ModeSwitch.NoSwitch -> ()
                            | ModeSwitch.SwitchMode kind -> x.SwitchMode kind ModeArgument.None |> ignore
                            | ModeSwitch.SwitchModeWithArgument (kind, argument) -> x.SwitchMode kind argument |> ignore
                            | ModeSwitch.SwitchPreviousMode -> _modeMap.SwitchPreviousMode() |> ignore
                        | ProcessResult.NotHandled -> 
                            ()
                        | ProcessResult.Error ->
                            ()
                        result
                finally
                    _processingInputCount <- _processingInputCount - 1
            _keyInputProcessedEvent.Trigger (keyInput, processResult)
            processResult

        // Raise the event that we received the key
        _keyInputStartEvent.Trigger keyInput

        try
            let remapResult, keyInputSet = x.GetKeyInputMappingCore keyInput

            // Clear out the _remapInput at this point.  It will be reset if the mapping needs more 
            // data
            _remapInput <- None

            match remapResult with
            | KeyMappingResult.NoMapping -> 
                // No mapping so just process the KeyInput values.  Don't forget previously
                // buffered values which are now known to not be a part of a mapping
                keyInputSet.KeyInputs |> Seq.map doProcess |> SeqUtil.last
            | KeyMappingResult.NeedsMoreInput -> 
                _remapInput <- Some keyInputSet
                _keyInputBufferedEvent.Trigger keyInput
                ProcessResult.Handled ModeSwitch.NoSwitch
            | KeyMappingResult.Recursive ->
                x.RaiseErrorMessage Resources.Vim_RecursiveMapping
                ProcessResult.Error
            | KeyMappingResult.Mapped keyInputSet -> 
                keyInputSet.KeyInputs |> Seq.map doProcess |> SeqUtil.last
        finally 
            _keyInputEndEvent.Trigger keyInput

    /// Simulate the KeyInput being processed.  Should not go through remapping
    member x.SimulateProcessed keyInput = 
        _keyInputStartEvent.Trigger keyInput
        _keyInputProcessedEvent.Trigger (keyInput, ProcessResult.Handled ModeSwitch.NoSwitch)
        _keyInputEndEvent.Trigger keyInput

    member x.Close () = 
        if _isClosed then 
            invalidOp Resources.VimBuffer_AlreadyClosed
        else
            try
                x.Mode.OnLeave()
                _modeMap.Modes |> Seq.iter (fun x -> x.OnClose())
                _vim.RemoveBuffer _textView |> ignore
                _undoRedoOperations.Close()
                _closedEvent.Trigger System.EventArgs.Empty
            finally 
                _isClosed <- true

    member x.RaiseErrorMessage msg = _errorMessageEvent.Trigger msg
    member x.RaiseWarningMessage msg = _warningMessageEvent.Trigger msg
    member x.RaiseStatusMessage msg = _statusMessageEvent.Trigger msg
    member x.RaiseStatusMessageLong msgSeq = _statusMessageLongEvent.Trigger msgSeq
                 
    interface IVimBuffer with
        member x.Vim = _vim
        member x.VimData = _vim.VimData
        member x.VimBufferData = x.VimBufferData
        member x.WordNavigator = _wordNavigator
        member x.TextView = _textView
        member x.MotionUtil = _motionUtil
        member x.TextBuffer = _textView.TextBuffer
        member x.TextSnapshot = _textView.TextSnapshot
        member x.UndoRedoOperations = _undoRedoOperations
        member x.BufferedRemapKeyInputs = x.BufferedRemapKeyInputs 
        member x.IncrementalSearch = _incrementalSearch
        member x.IsProcessingInput = _processingInputCount > 0
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
        member x.ExternalEditMode = x.ExternalEditMode
        member x.DisabledMode = x.DisabledMode
        member x.AllModes = _modeMap.Modes
        member x.LocalSettings = _localSettings
        member x.RegisterMap = _vim.RegisterMap

        member x.CanProcess keyInput = x.CanProcess keyInput
        member x.CanProcessAsCommand keyInput = x.CanProcessAsCommand keyInput
        member x.Close () = x.Close()
        member x.GetKeyInputMapping keyInput = x.GetKeyInputMapping keyInput
        member x.GetMode kind = _modeMap.GetMode kind
        member x.GetRegister name = _vim.RegisterMap.GetRegister name
        member x.Process keyInput = x.Process keyInput
        member x.SwitchMode kind arg = x.SwitchMode kind arg
        member x.SwitchPreviousMode () = _modeMap.SwitchPreviousMode()
        member x.SimulateProcessed keyInput = x.SimulateProcessed keyInput

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
        member x.WarningMessage = _warningMessageEvent.Publish
        [<CLIEvent>]
        member x.StatusMessage = _statusMessageEvent.Publish
        [<CLIEvent>]
        member x.StatusMessageLong = _statusMessageLongEvent.Publish
        [<CLIEvent>]
        member x.Closed = _closedEvent.Publish

    interface IPropertyOwner with
        member x.Properties = _properties
