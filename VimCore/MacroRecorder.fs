namespace Vim

/// Macro recording implementation
type internal MacroRecorder (_registerMap : IRegisterMap) =

    /// Option holding the data related to recording.  If it's Some then we are in the
    /// middle of recording.
    let mutable _recordData : (Register * KeyInput list) option = None

    /// Need to be careful to only record keystrokes which happen after we start
    /// recording.  Don't for instance want to record the KeyStroke for the stored
    /// register.
    let mutable _recordKeyStroke = false

    let _recordingStartedEvent = StandardEvent<RecordRegisterEventArgs>()
    let _recordingStoppedEvent = StandardEvent()

    member x.CurrentRecording = _recordData |> Option.map snd

    /// Are we currently recording
    member x.IsRecording = Option.isSome _recordData

    /// Start recording KeyInput values which are processed
    member x.StartRecording (register : Register) isAppend = 
        Contract.Requires (Option.isNone _recordData)

        // Calculate the initial list.  If we are appending to the existing register
        // value make sure to reverse the list as we build up the recorded KeyInput
        // values in reverse order for efficiency
        let list = 
            if isAppend then register.RegisterValue.KeyInputs |> List.rev
            else List.empty
        _recordData <- Some (register, list)
        _recordKeyStroke <- false
        
        let args = RecordRegisterEventArgs(register, isAppend)
        _recordingStartedEvent.Trigger x args

    member x.StopRecording () = 
        Contract.Requires (Option.isSome _recordData)
        let register, list = Option.get _recordData

        // Need to reverse the list as we stored it backwards
        let list = List.rev list
        let value = RegisterValue(list)
        _registerMap.SetRegisterValue register RegisterOperation.Yank value
        _recordData <- None
        _recordKeyStroke <- false
        _recordingStoppedEvent.Trigger x

    /// Need to track the KeyInputProcessed event for every IVimBuffer in the system
    member x.OnVimBufferCreated (buffer : IVimBuffer) =
        let bag = DisposableBag()
        buffer.KeyInputProcessed.Subscribe x.OnKeyInputProcessed |> bag.Add
        buffer.KeyInputStart.Subscribe x.OnKeyInputStart |> bag.Add
        buffer.Closed.AddHandler (fun _ _ -> bag.DisposeAll())

    /// Called whenever a KeyInput is processed.  Capture this if we are currently
    /// recording
    member x.OnKeyInputProcessed (args : KeyInputProcessedEventArgs) =
        let keyInput = args.KeyInput
        if not _recordKeyStroke then
            ()
        else
            match _recordData with 
            | None ->
                // Not recording so we don't care
                ()
            | Some (register, list) ->
                let list = keyInput :: list
                _recordData <- Some (register, list)

    /// Called whenever a KeyInput is started
    member x.OnKeyInputStart _ =
        _recordKeyStroke <- true

    interface IVimBufferCreationListener with
        member x.VimBufferCreated buffer = x.OnVimBufferCreated buffer

    interface IMacroRecorder with
        member x.CurrentRecording = x.CurrentRecording
        member x.IsRecording = x.IsRecording
        member x.StartRecording register isAppend = x.StartRecording register isAppend
        member x.StopRecording () = x.StopRecording ()
        [<CLIEvent>]
        member x.RecordingStarted = _recordingStartedEvent.Publish
        [<CLIEvent>]
        member x.RecordingStopped = _recordingStoppedEvent.Publish

