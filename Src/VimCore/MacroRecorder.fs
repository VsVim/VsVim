namespace Vim

/// Macro recording implementation
type internal MacroRecorder (_registerMap : IRegisterMap) =

    /// Option holding the data related to recording.  If it's Some then we are in the
    /// middle of recording.
    let mutable _recordData : (Register * KeyInput list) option = None

    /// This records whether or not the KeyInputEnd event is valid or not for 
    /// recording.  The macro recorder needs to be careful to not record key
    /// strokes that it only sees partial events for.  It will always see the
    /// partial key event for the register (KeyInputEnd) and the macro stop
    /// command 'q' (KeyInputStart).  Neither of these should be recorded.  Only
    /// keys which the recorder fully sees while recording should be considered
    let mutable _isKeyInputEndValid = false

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
        _recordingStoppedEvent.Trigger x

    /// Need to track the KeyInputProcessed event for every IVimBuffer in the system
    member x.OnVimBufferCreated (buffer : IVimBuffer) =
        let bag = DisposableBag()
        buffer.KeyInputStart.Subscribe x.OnKeyInputStart |> bag.Add
        buffer.KeyInputEnd.Subscribe x.OnKeyInputEnd |> bag.Add
        buffer.Closed.AddHandler (fun _ _ -> bag.DisposeAll())

    member x.OnKeyInputStart _ = 
        _isKeyInputEndValid <- Option.isSome _recordData

    member x.OnKeyInputEnd (args : KeyInputEventArgs) =
        if _isKeyInputEndValid then
            match _recordData with
            | None -> () 
            | Some (register, list) -> 
                let list = args.KeyInput :: list
                _recordData <- Some (register, list)

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

