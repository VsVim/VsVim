#light

namespace Vim

open System
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Utilities

/// Core parts of an IVimBuffer.  Used for components which make up an IVimBuffer but
/// need the same data provided by IVimBuffer.
type VimBufferData 
    (
        _vimTextBuffer: IVimTextBuffer,
        _textView: ITextView,
        _windowSettings: IVimWindowSettings,
        _jumpList: IJumpList,
        _statusUtil: IStatusUtil,
        _wordUtil: IWordUtil
    ) = 

    let mutable _currentDirectory: string option = None
    let mutable _visualCaretStartPoint: ITrackingPoint option = None
    let mutable _visualAnchorPoint: ITrackingPoint option = None 

    member x.CurrentFilePath : string option = _vimTextBuffer.Vim.VimHost.GetName _textView.TextBuffer |> Some
    member x.CurrentRelativeFilePath : string option =
        match x.CurrentFilePath with
        | None -> None
        | Some filePath ->
            let cd = match _currentDirectory with Some s -> s | None -> _vimTextBuffer.Vim.VimData.CurrentDirectory
            SystemUtil.StripPathPrefix cd filePath |> Some

    member x.WorkingDirectory =
        match _currentDirectory with
        | Some directory ->
            directory
        | None ->
            _vimTextBuffer.Vim.VimData.CurrentDirectory

    interface IVimBufferData with
        member x.CurrentDirectory 
            with get() = _currentDirectory
            and set value = _currentDirectory <- value
        member x.CurrentFilePath = x.CurrentFilePath
        member x.CurrentRelativeFilePath = x.CurrentRelativeFilePath
        member x.WorkingDirectory = x.WorkingDirectory
        member x.VisualCaretStartPoint 
            with get() = _visualCaretStartPoint
            and set value = _visualCaretStartPoint <- value
        member x.VisualAnchorPoint 
            with get() = _visualAnchorPoint
            and set value = _visualAnchorPoint <- value
        member x.JumpList = _jumpList
        member x.TextView = _textView
        member x.TextBuffer = _textView.TextBuffer
        member x.StatusUtil = _statusUtil
        member x.UndoRedoOperations = _vimTextBuffer.UndoRedoOperations
        member x.VimTextBuffer = _vimTextBuffer
        member x.WindowSettings = _windowSettings
        member x.WordUtil = _wordUtil
        member x.LocalSettings = _vimTextBuffer.LocalSettings
        member x.Vim = _vimTextBuffer.Vim

/// Implementation of the uninitialized mode.  This is designed to handle the ITextView
/// while it's in an uninitialized state.  It shouldn't touch the ITextView in any way.  
/// This is why it doesn't even contain a reference to it
type UninitializedMode(_vimTextBuffer: IVimTextBuffer) =
    interface IMode with
        member x.VimTextBuffer = _vimTextBuffer
        member x.ModeKind = ModeKind.Uninitialized
        member x.CommandNames = Seq.empty
        member x.CanProcess _ = false
        member x.Process _ = ProcessResult.NotHandled
        member x.OnEnter _ = ()
        member x.OnLeave() = ()
        member x.OnClose() = ()

type internal ModeMap
    (
        _vimTextBuffer: IVimTextBuffer,
        _incrementalSearch: IIncrementalSearch
    ) = 

    let mutable _modeMap: Map<ModeKind, IMode> = Map.empty
    let mutable _mode = UninitializedMode(_vimTextBuffer) :> IMode
    let mutable _previousMode = None
    let mutable _isSwitchingMode = false
    let _modeSwitchedEvent = StandardEvent<SwitchModeEventArgs>()

    member x.SwitchedEvent = _modeSwitchedEvent
    member x.Mode = _mode
    member x.PreviousMode = _previousMode
    member x.Modes = _modeMap |> Map.toSeq |> Seq.map (fun (k,m) -> m)
    member x.IsSwitchingMode = _isSwitchingMode
    member x.SwitchMode kind arg =
        if _isSwitchingMode then raise (InvalidOperationException("Recursive mode switch detected"))

        let switchModeCore () =
            let oldMode = _mode
            let newMode = _modeMap.Item kind

            // Need to update all of our internal state before calling out to external 
            // code.  This ensures all consumers see the final state vs. an intermediate
            // state.
            _mode <- newMode
            _previousMode <-
                if oldMode.ModeKind = ModeKind.Disabled || oldMode.ModeKind = ModeKind.Uninitialized then
                    if VisualKind.IsAnyVisualOrSelect newMode.ModeKind then
                        // Visual Mode always needs a mode to fall back on when it is exited.  The switch 
                        // previous must perform some action. 
                        _modeMap.Item ModeKind.Normal |> Some
                    else
                        // Otherwise transitioning out of disabled / uninitialized should have no 
                        // mode to fall back on.  
                        None
                elif VisualKind.IsAnyVisualOrSelect oldMode.ModeKind then
                    if VisualKind.IsAnyVisualOrSelect newMode.ModeKind then
                        // When switching between different visual modes we don't want to lose
                        // the previous non-visual mode value.  Commands executing in Visual mode
                        // which return a SwitchPrevious mode value expected to actually leave 
                        // Visual Mode 
                        _previousMode
                    elif newMode.ModeKind = ModeKind.Normal then
                        None
                    else
                        Some oldMode
                else
                    Some oldMode

            oldMode.OnLeave()

            // Incremental search should not persist between mode changes.  
            if _incrementalSearch.HasActiveSession then
                _incrementalSearch.CancelSession()

            _vimTextBuffer.SwitchMode kind arg

            newMode.OnEnter arg
            (oldMode, newMode)

        let (oldMode, newMode) = 
            try
                _isSwitchingMode <- true
                switchModeCore ()
            finally
                _isSwitchingMode <-false

        _modeSwitchedEvent.Trigger x (SwitchModeEventArgs(oldMode, newMode))
        newMode

    member x.GetMode kind = Map.find kind _modeMap

    member x.AddMode (mode: IMode) = 
        _modeMap <- Map.add (mode.ModeKind) mode _modeMap

    member x.RemoveMode (mode: IMode) = 
        _modeMap <- Map.remove mode.ModeKind _modeMap

    member x.Reset (mode: IMode) =
        _mode <- mode
        _previousMode <- None

type internal VimBuffer 
    (
        _vimBufferData: IVimBufferData,
        _incrementalSearch: IIncrementalSearch,
        _motionUtil: IMotionUtil,
        _wordNavigator: ITextStructureNavigator,
        _windowSettings: IVimWindowSettings,
        _commandUtil: ICommandUtil
    ) as this =

    /// Maximum number of maps which can occur for a key map.  This is not a standard vim or gVim
    /// setting.  It's a hueristic setting meant to prevent infinite recursion in the specific cases
    /// that maxmapdepth can't or won't catch (see :help maxmapdepth).  
    let _maxMapCount = 1000

    let _vim = _vimBufferData.Vim
    let _textView = _vimBufferData.TextView
    let _jumpList = _vimBufferData.JumpList
    let _localSettings = _vimBufferData.LocalSettings
    let _undoRedoOperations = _vimBufferData.UndoRedoOperations
    let _vimTextBuffer = _vimBufferData.VimTextBuffer
    let _statusUtil = _vimBufferData.StatusUtil
    let _properties = PropertyCollection()
    let _bag = DisposableBag()
    let _modeMap = ModeMap(_vimBufferData.VimTextBuffer, _incrementalSearch)
    let _keyMap = _vim.KeyMap
    let mutable _processingInputCount = 0
    let mutable _isClosed = false

    /// This is the buffered input when a remap request needs more than one 
    /// element
    let mutable _bufferedKeyInput: KeyInputSet option = None

    let _keyInputStartEvent = StandardEvent<KeyInputStartEventArgs>()
    let _keyInputProcessingEvent = StandardEvent<KeyInputStartEventArgs>()
    let _keyInputProcessedEvent = StandardEvent<KeyInputProcessedEventArgs>()
    let _keyInputBufferedEvent = StandardEvent<KeyInputSetEventArgs>()
    let _keyInputEndEvent = StandardEvent<KeyInputEventArgs>()
    let _errorMessageEvent = StandardEvent<StringEventArgs>()
    let _warningMessageEvent = StandardEvent<StringEventArgs>()
    let _statusMessageEvent = StandardEvent<StringEventArgs>()
    let _closingEvent = StandardEvent()
    let _closedEvent = StandardEvent()
    let _postClosedEvent = StandardEvent()
    let _bufferName = _vim.VimHost.GetName _vimBufferData.TextBuffer

    do 
        // Adjust local settings.
        this.AdjustLocalSettings()

        // Listen for mode switches on the IVimTextBuffer instance.  We need to keep our 
        // Mode in sync with this value
        _vimTextBuffer.SwitchedMode
        |> Observable.subscribe (fun args -> this.OnVimTextBufferSwitchedMode args.ModeKind args.ModeArgument)
        |> _bag.Add

        _vim.MarkMap.SetMark Mark.LastJump _vimBufferData 0 0 |> ignore
        _vim.MarkMap.ReloadBuffer _vimBufferData _bufferName |> ignore

        // Subscribe to local settings changed events.
        _vimTextBuffer.LocalSettings.SettingChanged
        |> Observable.subscribe (fun args -> this.OnLocalSettingsChanged args.Setting)
        |> _bag.Add

        // Subscribe to text buffer before save events.
        _vim.VimHost.BeforeSave
        |> Observable.subscribe (fun args -> this.OnBeforeSave args.TextBuffer)
        |> _bag.Add

    member x.IsReadOnly
        with get() = _vim.VimHost.IsReadOnly _vimBufferData.TextBuffer

    member x.BufferedKeyInputs =
        match _bufferedKeyInput with
        | None -> List.empty
        | Some keyInputSet -> keyInputSet.KeyInputs

    member x.InOneTimeCommand 
        with get() = _vimTextBuffer.InOneTimeCommand
        and set value = _vimTextBuffer.InOneTimeCommand <- value
    
    member x.ModeMap = _modeMap
    member x.Mode = _modeMap.Mode
    member x.NormalMode = _modeMap.GetMode ModeKind.Normal :?> INormalMode
    member x.VisualLineMode = _modeMap.GetMode ModeKind.VisualLine :?> IVisualMode
    member x.VisualCharacterMode = _modeMap.GetMode ModeKind.VisualCharacter :?> IVisualMode
    member x.VisualBlockMode = _modeMap.GetMode ModeKind.VisualBlock :?> IVisualMode
    member x.CommandMode = _modeMap.GetMode ModeKind.Command :?> ICommandMode
    member x.InsertMode = _modeMap.GetMode ModeKind.Insert  :?> IInsertMode
    member x.ReplaceMode = _modeMap.GetMode ModeKind.Replace :?> IInsertMode
    member x.SelectCharacterMode = _modeMap.GetMode ModeKind.SelectCharacter :?> ISelectMode
    member x.SelectLineMode = _modeMap.GetMode ModeKind.SelectLine :?> ISelectMode
    member x.SelectBlockMode = _modeMap.GetMode ModeKind.SelectBlock :?> ISelectMode
    member x.SubstituteConfirmMode = _modeMap.GetMode ModeKind.SubstituteConfirm :?> ISubstituteConfirmMode
    member x.DisabledMode = _modeMap.GetMode ModeKind.Disabled :?> IDisabledMode
    member x.ExternalEditMode = _modeMap.GetMode ModeKind.ExternalEdit 

    /// Current KeyRemapMode which should be used when calculating keyboard mappings
    member x.KeyRemapMode = 
        match _modeMap.Mode.ModeKind with
        | ModeKind.Insert -> KeyRemapMode.Insert
        | ModeKind.Replace -> KeyRemapMode.Insert
        | ModeKind.Normal -> x.NormalMode.KeyRemapMode
        | ModeKind.Command -> KeyRemapMode.Command
        | ModeKind.VisualBlock -> x.VisualBlockMode.KeyRemapMode
        | ModeKind.VisualCharacter -> x.VisualCharacterMode.KeyRemapMode
        | ModeKind.VisualLine -> x.VisualLineMode.KeyRemapMode
        | _ -> KeyRemapMode.None

    /// Is this buffer currently in the middle of a count operation
    member x.InCount = 
        match _modeMap.Mode.ModeKind with
        | ModeKind.Normal -> x.NormalMode.InCount
        | ModeKind.VisualBlock -> x.VisualBlockMode.InCount
        | ModeKind.VisualCharacter -> x.VisualCharacterMode.InCount
        | ModeKind.VisualLine -> x.VisualLineMode.InCount
        | _ -> false

    member x.VimBufferData = _vimBufferData

    /// Add an IMode into the IVimBuffer instance
    member x.AddMode mode = _modeMap.AddMode mode

    /// Vim treats keypad keys exactly like their non-keypad equivalent (keypad 
    /// + is the same as a normal +).  The keypad does participate in key mapping
    /// but once key mapping is finished the keypad keys are mapped back to their
    /// non-keypad equivalent for processing
    member x.GetNonKeypadEquivalent keyInput = 
        match KeyInputUtil.GetNonKeypadEquivalent keyInput with
        | None -> keyInput
        | Some keyInput -> keyInput

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
            if keyInput = _vim.GlobalSettings.DisableAllCommand then
                // The disable command can be processed at all times
                true
            elif keyInput.Key = VimKey.Nop then
                // The nop key can be processed at all times
                true
            elif keyInput.Key = VimKey.Escape && _vimTextBuffer.InOneTimeCommand.IsSome then
                // Inside a one command state Escape is valid and returns us to the original
                // mode.  This check is necessary because certain modes like Normal don't handle
                // Escape themselves but Escape should force us back to Insert even here
                true
            elif x.Mode.CanProcess keyInput then
                allowDirectInsert || not (isDirectInsert keyInput)
            else
                false

        let mapped (keyInputSet: KeyInputSet) =
            // Simplest case.  Mapped to a set of values so just consider the first one
            //
            // Note: This is not necessarily the provided KeyInput.  There could be several
            // buffered KeyInput values which are around because the matched the prefix of a
            // mapping which this KeyInput has broken.  So the first KeyInput we would
            // process is the first buffered KeyInput
            match keyInputSet.FirstKeyInput with
            | Some keyInput -> keyInput |> x.GetNonKeypadEquivalent |> canProcess 
            | None -> false

        match x.GetKeyInputMapping keyInput with
        | KeyMappingResult.Mapped keyInputSet -> 
            mapped keyInputSet

        | KeyMappingResult.PartiallyMapped (keyInputSet, _) ->
            mapped keyInputSet

        | KeyMappingResult.NeedsMoreInput _ -> 
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

    member x.Close () = 

        if _isClosed then 
            invalidOp Resources.VimBuffer_AlreadyClosed

        let lineNumber, offset = SnapshotPointUtil.GetLineNumberAndOffset (TextViewUtil.GetCaretPoint _textView)
        _vim.MarkMap.UnloadBuffer _vimBufferData _bufferName lineNumber offset |> ignore

        // Run the closing event in a separate try / catch.  Don't want anyone to be able
        // to disrupt the necessary actions like removing a buffer from the global list
        // by throwing during the Closing event
        try
            _closingEvent.Trigger x
        with
            _ -> ()

        try
            x.Mode.OnLeave()
            _modeMap.Modes |> Seq.iter (fun x -> x.OnClose())
            _vim.RemoveVimBuffer _textView |> ignore
            _undoRedoOperations.Close()
            _jumpList.Clear()
            _closedEvent.Trigger x
        finally 
            _isClosed <- true
            if not x.IsProcessingInput then
                _postClosedEvent.Trigger x
            
            // Stop listening to events
            _bag.DisposeAll()

    /// Get the key mapping for the given KeyInputSet and KeyRemapMode.  This will take into
    /// account whether the buffer is currently in the middle of a count operation.  In this
    /// state the 0 key is not ever mapped
    member x.GetKeyMappingCore keyInputSet keyRemapMode = 
        try
            _keyMap.IsZeroMappingEnabled <- not x.InCount
            _keyMap.GetKeyMapping keyInputSet keyRemapMode
        finally
            _keyMap.IsZeroMappingEnabled <- true

    /// Get the correct mapping of the given KeyInput value in the current state of the 
    /// IVimBuffer.  This will consider any buffered KeyInput values 
    member x.GetKeyInputMapping (keyInput: KeyInput) =

        let keyInputSet = 
            match _bufferedKeyInput with
            | None -> KeyInputSet(keyInput)
            | Some bufferedKeyInputSet -> bufferedKeyInputSet.Add keyInput

        x.GetKeyMappingCore keyInputSet x.KeyRemapMode

    member x.OnVimTextBufferSwitchedMode modeKind modeArgument =
        if x.Mode.ModeKind <> modeKind then
            _modeMap.SwitchMode modeKind modeArgument |> ignore

    /// Adjust any local settings for the buffer
    member x.AdjustLocalSettings () =
        x.AdjustEndOfLineSetting()

    /// Raised when a local setting is changed
    member x.OnLocalSettingsChanged (setting: Setting) = 
        if setting.Name = LocalSettingNames.EndOfLineName then
            x.ApplyEndOfLineSetting()

    /// Raised before a text buffer is saved
    member x.OnBeforeSave (textBuffer: ITextBuffer) = 
        if textBuffer = _vimBufferData.TextBuffer then
           x.ApplyFixEndOfLineSetting() 

    /// Adjust the 'endofline' setting for the buffer
    member x.AdjustEndOfLineSetting () =
        let textView = _vimBufferData.TextView
        let textBuffer = textView.TextBuffer
        let snapshot = textBuffer.CurrentSnapshot
        let localSettings = _vimBufferData.LocalSettings
        let endOfLineSetting = SnapshotUtil.AllLinesHaveLineBreaks snapshot
        localSettings.EndOfLine <- endOfLineSetting

    /// Apply the 'endofline' setting to the buffer
    member x.ApplyEndOfLineSetting () =
        if not x.IsReadOnly then
            let localSettings = _vimBufferData.LocalSettings
            let textView = _vimBufferData.TextView
            let endOfLineSetting = localSettings.EndOfLine
            if endOfLineSetting then
                TextViewUtil.InsertFinalNewLine textView
            else
                TextViewUtil.RemoveFinalNewLine textView

    /// Apply the 'fixeondofline' setting to the buffer
    member x.ApplyFixEndOfLineSetting () =
        if not x.IsReadOnly then
            let localSettings = _vimBufferData.LocalSettings
            let textView = _vimBufferData.TextView
            let fixEndOfLineSetting = localSettings.FixEndOfLine
            if fixEndOfLineSetting then
                TextViewUtil.InsertFinalNewLine textView

    member x.IsProcessingInput
        with get(): bool = _processingInputCount > 0

    /// Process the single KeyInput value.  No mappings are considered here.  The KeyInput is 
    /// simply processed directly
    member x.ProcessOneKeyInput (keyInput: KeyInput) =

        let processResult = 

            // Raise the KeyInputProcessing event before we actually process the value
            let args = KeyInputStartEventArgs(keyInput)
            _keyInputProcessingEvent.Trigger x args

            _processingInputCount <- _processingInputCount + 1
            try
                if args.Handled then
                    ProcessResult.Handled ModeSwitch.NoSwitch
                elif keyInput = _vim.GlobalSettings.DisableAllCommand then
                    // Toggle the state of Vim.IsDisabled
                    _vim.IsDisabled <- not _vim.IsDisabled
                    ProcessResult.OfModeKind x.Mode.ModeKind
                elif keyInput.Key = VimKey.Nop then
                    // The <nop> key should have no affect
                    ProcessResult.Handled ModeSwitch.NoSwitch
                else
                    let result = x.Mode.Process keyInput

                    // Certain types of commands can always cause the current mode to be exited for
                    // the previous one time command mode.  Handle them here
                    let maybeLeaveOneCommand() = 
                        if _vimTextBuffer.InSelectModeOneTimeCommand then
                            // completing any command (even cursor movements) in a one-command mode initiated from a 
                            // select mode will revert to the analogous select mode
                            _vimTextBuffer.InSelectModeOneTimeCommand <- false
                            match VisualKind.OfModeKind x.Mode.ModeKind with
                            | Some visualModeKind -> 
                                x.SwitchMode visualModeKind.SelectModeKind ModeArgument.None |> ignore
                            | None -> 
                                ()
                        else
                            match _vimTextBuffer.InOneTimeCommand with
                            | Some modeKind ->
                                // A completed command always ends a one-command started from insert/replace mode, 
                                // unless the current mode is visual/select.  In the latter case, we expect that a
                                // command completed in visual/select mode will return an explicit SwitchMode result 
                                // so we don't need to override the result here.
                                if not (VisualKind.IsAnyVisualOrSelect x.Mode.ModeKind) then
                                    _vimTextBuffer.InOneTimeCommand <- None
                                    x.SwitchMode modeKind ModeArgument.None |> ignore
                            | None ->
                                ()

                    let maybeOverrideModeSwich modeKind =
                        // switching to an insert mode should cancel one command mode
                        if VimExtensions.IsAnyInsert modeKind then
                            _vimTextBuffer.InSelectModeOneTimeCommand <- false
                            _vimTextBuffer.InOneTimeCommand <- None
                            modeKind
                        // switching to normal mode should end one command mode, reverting to the stored mode
                        elif modeKind = ModeKind.Normal then
                            match _vimTextBuffer.InOneTimeCommand with
                            | Some oneTimeModeKind ->
                                _vimTextBuffer.InSelectModeOneTimeCommand <- false
                                _vimTextBuffer.InOneTimeCommand <- None
                                oneTimeModeKind
                            | None ->
                                modeKind
                        else
                            modeKind

                    match result with
                    | ProcessResult.Handled modeSwitch ->
                        match modeSwitch with
                        | ModeSwitch.NoSwitch -> 
                            maybeLeaveOneCommand()
                        | ModeSwitch.SwitchMode kind -> 
                            let switchKind = maybeOverrideModeSwich kind
                            x.SwitchMode switchKind ModeArgument.None |> ignore
                        | ModeSwitch.SwitchModeWithArgument (kind, argument) -> 
                            let switchKind = maybeOverrideModeSwich kind
                            x.SwitchMode switchKind argument |> ignore
                        | ModeSwitch.SwitchPreviousMode -> 
                            x.SwitchPreviousMode() |> ignore
                        | ModeSwitch.SwitchModeOneTimeCommand modeKind ->
                            // Begins one command mode and immediately switches to the target mode.
                            // One command mode initiated from select mode is tracked separately.
                            if VisualKind.IsAnySelect x.Mode.ModeKind then
                                _vimTextBuffer.InSelectModeOneTimeCommand <- true
                            else
                                _vimTextBuffer.InOneTimeCommand <- Some x.Mode.ModeKind
                            x.SwitchMode modeKind ModeArgument.None |> ignore
                    | ProcessResult.HandledNeedMoreInput ->
                        ()
                    | ProcessResult.NotHandled -> 
                        maybeLeaveOneCommand()
                    | ProcessResult.Error ->
                        maybeLeaveOneCommand()
                    result
            finally
                _processingInputCount <- _processingInputCount - 1

        let args = KeyInputProcessedEventArgs(keyInput, processResult)
        _keyInputProcessedEvent.Trigger x args
        processResult

    /// Process the provided KeyInputSet until completion or until a point where an 
    /// ambiguous mapping is reached
    member x.ProcessCore (keyInputSet: KeyInputSet) = 
        Contract.Assert(Option.isNone _bufferedKeyInput)

        let mapCount = ref 0
        let remainingSet = ref keyInputSet
        let processResult = ref (ProcessResult.Handled ModeSwitch.NoSwitch)

        // Whenever processing a key results in an error, the rest of the key mapping 
        // should not be processed (:help key-mapping, search for error)
        let isError () = 
            match processResult.Value with
            | ProcessResult.Error -> true
            | _ -> false

        // Process the KeyInput values in the given set to completion without considering
        // any further key mappings
        let processSet (keyInputSet: KeyInputSet) = 
            let mutable error = false
            for keyInput in keyInputSet.KeyInputs do
                if not (isError ()) then 
                    let keyInput = x.GetNonKeypadEquivalent keyInput
                    processResult := x.ProcessOneKeyInput keyInput

        while remainingSet.Value.Length > 0 && not (isError ()) do
            match x.KeyRemapMode with
            | KeyRemapMode.None -> 
                // There is no mode for the current key stroke but may be for the subsequent
                // ones in the set.  Process the first one only here 
                remainingSet.Value.FirstKeyInput.Value |> KeyInputSetUtil.Single |> processSet
                remainingSet := remainingSet.Value.Rest |> KeyInputSetUtil.OfList
            | _ -> 
                let keyMappingResult = x.GetKeyMappingCore remainingSet.Value x.KeyRemapMode
                remainingSet := 
                    match keyMappingResult with
                    | KeyMappingResult.Mapped mappedKeyInputSet -> 
                        mapCount := mapCount.Value + 1
                        processSet mappedKeyInputSet
                        KeyInputSet.Empty
                    | KeyMappingResult.PartiallyMapped (mappedKeyInputSet, remainingSet) ->
                        mapCount := mapCount.Value + 1
                        processSet mappedKeyInputSet
                        remainingSet
                    | KeyMappingResult.NeedsMoreInput keyInputSet -> 
                        _bufferedKeyInput <- Some keyInputSet
                        let args = KeyInputSetEventArgs(keyInputSet)
                        _keyInputBufferedEvent.Trigger x args
                        processResult := ProcessResult.Handled ModeSwitch.NoSwitch
                        KeyInputSet.Empty
                    | KeyMappingResult.Recursive ->
                        x.RaiseErrorMessage Resources.Vim_RecursiveMapping
                        processResult := ProcessResult.Error
                        KeyInputSet.Empty

                // The MaxMapCount value is a heuristic which VsVim implements to avoid an infinite
                // loop processing recursive input.  In a perfect world we would implement 
                // Ctrl-C support and allow users to break out of this loop but right now we don't
                // and this is a heuristic to prevent hanging the IDE until then
                if remainingSet.Value.Length > 0 && mapCount.Value = _maxMapCount then
                    x.RaiseErrorMessage Resources.Vim_RecursiveMapping
                    processResult := ProcessResult.Error
                    remainingSet := KeyInputSet.Empty

        processResult.Value

    /// Actually process the input key.  Raise the change event on an actual change
    member x.Process (keyInput: KeyInput) =

        VimTrace.TraceInfo("VimBuffer.Process: {0}", keyInput)

        // Raise the event that we received the key
        let args = KeyInputStartEventArgs(keyInput)
        _keyInputStartEvent.Trigger x args
        
        try
            if args.Handled then
                // If one of the event handlers handled the KeyInput themselves then 
                // the key is considered handled and nothing changed.  Need to raise 
                // the process events here since it was technically processed at this
                // point
                let keyInputProcessingEventArgs = KeyInputStartEventArgs(keyInput)
                keyInputProcessingEventArgs.Handled <- true
                _keyInputProcessingEvent.Trigger x keyInputProcessingEventArgs

                let processResult = ProcessResult.Handled ModeSwitch.NoSwitch
                let keyInputProcessedEventArgs = KeyInputProcessedEventArgs(keyInput, processResult)
                _keyInputProcessedEvent.Trigger x keyInputProcessedEventArgs

                processResult
            else

                // Combine this KeyInput with the buffered KeyInput values and clear it out.  If 
                // this KeyInput needs more input then it will be re-buffered
                let keyInputSet = 
                    match _bufferedKeyInput with
                    | None -> KeyInputSet(keyInput)
                    | Some bufferedKeyInputSet -> bufferedKeyInputSet.Add keyInput
                _bufferedKeyInput <- None

                x.ProcessCore keyInputSet

        finally 
            _keyInputEndEvent.Trigger x args
            if _isClosed && not x.IsProcessingInput then
                _postClosedEvent.Trigger x

    /// Process all of the buffered KeyInput values 
    member x.ProcessBufferedKeyInputs() = 
        match _bufferedKeyInput with
        | None -> ()
        | Some keyInputSet ->
            _bufferedKeyInput <- None

            // If there is an exact match in the KeyMap then we will use that to do a 
            // mapping.  This isn't documented anywhere but can be demonstrated as follows
            //
            //  :imap i short
            //  :imap ii long
            //
            // Then type 'i' in insert mode and wait for the time out.  It will print 'short'
            let keyInputSet = 
                let keyMapping = 
                    _keyMap.GetKeyMappingsForMode x.KeyRemapMode
                    |> Seq.tryFind (fun keyMapping -> keyMapping.Left = keyInputSet)
                match keyMapping with
                | None -> keyInputSet
                | Some keyMapping -> keyMapping.Right

            keyInputSet.KeyInputs
            |> Seq.iter (fun keyInput -> x.ProcessOneKeyInput keyInput |> ignore)
 
            if _isClosed && not x.IsProcessingInput then
                _postClosedEvent.Trigger x   
    
    member x.RaiseErrorMessage msg = 
        let args = StringEventArgs(msg)
        _errorMessageEvent.Trigger x args

    member x.RaiseWarningMessage msg = 
        let args = StringEventArgs(msg)
        _warningMessageEvent.Trigger x args

    member x.RaiseStatusMessage msg = 
        let args = StringEventArgs(msg)
        _statusMessageEvent.Trigger x args

    /// Remove an IMode from the IVimBuffer instance
    member x.RemoveMode mode = _modeMap.RemoveMode mode

    /// Switch to the desired mode
    member x.SwitchMode modeKind modeArgument =        
        _modeMap.SwitchMode modeKind modeArgument

    member x.SwitchPreviousMode () =
        // The previous mode is overridden when we are in one command mode
        match _vimTextBuffer.InOneTimeCommand with
        | Some modeKind ->
            _vimTextBuffer.InSelectModeOneTimeCommand <- false
            _vimTextBuffer.InOneTimeCommand <- None
            x.SwitchMode modeKind ModeArgument.None
        | None ->
            match _modeMap.PreviousMode with
            | None -> _modeMap.Mode
            | Some mode -> x.SwitchMode mode.ModeKind ModeArgument.None

    /// Simulate the KeyInput being processed.  Should not go through remapping because the caller
    /// is responsible for doing the mapping.  They are indicating the literal key was processed
    member x.SimulateProcessed keyInput = 

        // When simulating KeyInput as being processed we clear out any buffered KeyInput 
        // values.  By calling this API the caller wants us to simulate a specific key was 
        // pressed and they action was handled by them.  We assume they accounted for any 
        // buffered input with this action.
        _bufferedKeyInput <- None

        let keyInputEventArgs = KeyInputStartEventArgs(keyInput)
        let keyInputProcessedEventArgs = KeyInputProcessedEventArgs(keyInput, ProcessResult.Handled ModeSwitch.NoSwitch)
        _keyInputStartEvent.Trigger x keyInputEventArgs
        _keyInputProcessedEvent.Trigger x keyInputProcessedEventArgs
        _keyInputEndEvent.Trigger x keyInputEventArgs
                 
    interface IVimBuffer with
        member x.CurrentDirectory 
            with get() = _vimBufferData.CurrentDirectory
            and set value = _vimBufferData.CurrentDirectory <- value
        member x.Vim = _vim
        member x.VimData = _vim.VimData
        member x.VimBufferData = x.VimBufferData
        member x.VimTextBuffer = x.VimBufferData.VimTextBuffer
        member x.WordNavigator = _wordNavigator
        member x.TextView = _textView
        member x.CommandUtil = _commandUtil
        member x.MotionUtil = _motionUtil
        member x.TextBuffer = _textView.TextBuffer
        member x.TextSnapshot = _textView.TextSnapshot
        member x.UndoRedoOperations = _undoRedoOperations
        member x.BufferedKeyInputs = x.BufferedKeyInputs 
        member x.IncrementalSearch = _incrementalSearch
        member x.InOneTimeCommand = x.InOneTimeCommand
        member x.IsClosed = _isClosed
        member x.IsProcessingInput = x.IsProcessingInput 
        member x.IsSwitchingMode = _modeMap.IsSwitchingMode
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
        member x.SelectCharacterMode = x.SelectCharacterMode
        member x.SelectLineMode = x.SelectLineMode
        member x.SelectBlockMode = x.SelectBlockMode
        member x.SubstituteConfirmMode = x.SubstituteConfirmMode
        member x.ExternalEditMode = x.ExternalEditMode
        member x.DisabledMode = x.DisabledMode
        member x.AllModes = _modeMap.Modes
        member x.GlobalSettings = _localSettings.GlobalSettings;
        member x.LocalSettings = _localSettings
        member x.WindowSettings = _windowSettings
        member x.RegisterMap = _vim.RegisterMap

        member x.CanProcess keyInput = x.CanProcess keyInput
        member x.CanProcessAsCommand keyInput = x.CanProcessAsCommand keyInput
        member x.Close () = x.Close()
        member x.GetKeyInputMapping keyInput = x.GetKeyInputMapping keyInput
        member x.GetMode kind = _modeMap.GetMode kind
        member x.GetRegister name = _vim.RegisterMap.GetRegister name
        member x.Process keyInput = x.Process keyInput
        member x.ProcessBufferedKeyInputs() = x.ProcessBufferedKeyInputs()
        member x.SwitchMode kind arg = x.SwitchMode kind arg
        member x.SwitchPreviousMode() = x.SwitchPreviousMode()
        member x.SimulateProcessed keyInput = x.SimulateProcessed keyInput

        member x.IsReadOnly
            with get () =  x.IsReadOnly

        [<CLIEvent>]
        member x.SwitchedMode = _modeMap.SwitchedEvent.Publish
        [<CLIEvent>]
        member x.KeyInputStart = _keyInputStartEvent.Publish
        [<CLIEvent>]
        member x.KeyInputProcessing = _keyInputProcessingEvent.Publish
        [<CLIEvent>]
        member x.KeyInputProcessed = _keyInputProcessedEvent.Publish
        [<CLIEvent>]
        member x.KeyInputBuffered = _keyInputBufferedEvent.Publish
        [<CLIEvent>]
        member x.KeyInputEnd = _keyInputEndEvent.Publish
        [<CLIEvent>]
        member x.ErrorMessage = _errorMessageEvent.Publish
        [<CLIEvent>]
        member x.WarningMessage = _warningMessageEvent.Publish
        [<CLIEvent>]
        member x.StatusMessage = _statusMessageEvent.Publish
        [<CLIEvent>]
        member x.Closing = _closingEvent.Publish
        [<CLIEvent>]
        member x.Closed = _closedEvent.Publish
        [<CLIEvent>]
        member x.PostClosed = _postClosedEvent.Publish

    interface IVimBufferInternal with
        member x.TextView = _textView
        member x.RaiseStatusMessage msg = x.RaiseStatusMessage msg
        member x.RaiseWarningMessage msg = x.RaiseWarningMessage msg
        member x.RaiseErrorMessage msg = x.RaiseErrorMessage msg

    interface IPropertyOwner with
        member x.Properties = _properties
