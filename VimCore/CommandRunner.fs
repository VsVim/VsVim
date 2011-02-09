#light

namespace Vim
open Microsoft.VisualStudio.Text.Editor

type internal RunCommandResult = 
    | RanCommand of CommandRunData * CommandResult
    | CancelledCommand
    | NeedMore of (KeyInput -> RunCommandResult)
    | NoMatchingCommand

type internal CommandRunnerData = {

    /// REPEAT TODO: Store the RegisterName here as an Option so it can be 
    /// threaded through to CommandData properly
    Register : Register;
    Count : int option;

    /// Name of the current command
    KeyInputSet : KeyInputSet;

    /// Reverse ordered List of all KeyInput for a given command
    Inputs : KeyInput list;

    State : CommandRunnerState;
}

/// Implementation of the ICommandRunner interface.  
type internal CommandRunner
    ( 
        _textView : ITextView,
        _registerMap : IRegisterMap,
        _capture : IMotionCapture,
        // REPEAT TODO: Eventually this won't be needed
        _motionUtil : ITextViewMotionUtil,
        _commandUtil : ICommandUtil,
        _statusUtil : IStatusUtil,
        _visualKind : VisualKind ) as this =

    /// Represents the empty state for processing commands.  Holds all of the default
    /// values
    let _emptyData = { 
        Register = _registerMap.GetRegister RegisterName.Unnamed
        Count = None
        KeyInputSet = Empty
        Inputs = List.empty
        State = CommandRunnerState.NoInput
    }

    let _commandRanEvent = Event<_>()
    
    let mutable _commandMap : Map<KeyInputSet, CommandBinding> = Map.empty

    /// Contains all of the state data for a Command operation
    let mutable _data = _emptyData

    /// The current function which handles running input
    let mutable _runFunc : KeyInput -> RunCommandResult = fun _ -> RunCommandResult.CancelledCommand

    /// True during the running of a particular KeyInput 
    let mutable _inRun = false

    do
        _runFunc <- this.RunCheckForCountAndRegister

    /// Create a CommandRunData based on the current state for the given command information
    member x.CreateCommandRunData command motionDataOpt visualDataOpt command2Opt = 
        {  
            Command = command
            Command2 = command2Opt
            Register = _data.Register
            Count = _data.Count
            MotionData = motionDataOpt
            VisualRunData = visualDataOpt }

    member x.GetVisualSpan kind = 
        match _textView.Selection.Mode with
        | TextSelectionMode.Stream -> VisualSpan.Single (kind,_textView.Selection.StreamSelectionSpan.SnapshotSpan)
        | TextSelectionMode.Box -> VisualSpan.Multiple (kind,_textView.Selection.SelectedSpans)
        | _ -> failwith "Invalid Selection Mode"

    /// Used to wait for the character after the " which signals the Register 
    member x.WaitForRegister() = 

        let inner (ki:KeyInput) = 
            match RegisterNameUtil.CharToRegister ki.Char with
            | None -> NoMatchingCommand
            | Some(name) ->
                let reg = _registerMap.GetRegister name
                _data <- { _data with Register = reg }
                NeedMore x.RunCheckForCountAndRegister

        _data <- {_data with State = NotEnoughInput }
        NeedMore inner

    /// Used to wait for a count value to complete.  Passed in the initial digit 
    /// in the count
    member x.WaitForCount (initialDigit:KeyInput) =
        _data <- { _data with State = NotEnoughInput }
        let rec inner (num:string) (ki:KeyInput) = 
            if ki.IsDigit then
                let num = num + ki.Char.ToString()
                NeedMore (inner num)
            else
                let count = System.Int32.Parse(num)
                _data <- { _data with Count = Some count }
                x.RunCheckForCountAndRegister ki
        inner StringUtil.empty initialDigit 

    /// REPEAT TODO: Delete when no longer needed
    member x.WaitForMotionOld command func initialInput =
        let inner (motionData : MotionData, _) = 
            // Now use the MotionResult to complete the command
            match _motionUtil.GetMotion motionData.Motion motionData.MotionArgument with
            | None ->
                // Invalid motion so raise an error
                _statusUtil.OnError Resources.MotionCapture_InvalidMotion
                CancelledCommand
            | Some motionResult -> 
                // Valid motion pass off to the actual command
                let data = x.CreateCommandRunData command (Some motionData) None None
                let result = func data.Count data.Register motionResult
                RanCommand (data, result)
        x.WaitForMotion command inner initialInput

    /// Certain commands require additional data on top of their initial command 
    /// name.  They will return NeedMoreKeyInput until they receive it all
    member x.WaitForAdditionalCommandInput func =
        let inner ki = func ki |> RanCommand
        inner

    /// Waits for a completed command to be entered
    member x.WaitForCommand (ki:KeyInput) = 
        let previousName = _data.KeyInputSet
        let commandName = previousName.Add ki
        _data <- { _data with KeyInputSet = commandName; State = NotEnoughInput }
        x.RunCommand commandName previousName ki

    /// Wait for a long command to complete
    /// REPEAT TODO: Delete
    member x.WaitForLongCommand command keyRemapMode func =
        _data <- { _data with State = NotFinishWithCommand(command, keyRemapMode) }

        let data = x.CreateCommandRunData command None None None
        let rec inner result = 
            match result with
            | LongCommandResult.Finished(commandResult) -> RanCommand (data,commandResult)
            | LongCommandResult.Cancelled -> CancelledCommand
            | LongCommandResult.NeedMoreInput(keyRemapMode, func) -> 
                _data <- { _data with State = NotFinishWithCommand(command, keyRemapMode) }
                NeedMore (fun ki -> func ki |> inner)

        func data.Count data.Register |> inner

    /// Wait for a long visual command to complete
    /// REPEAT TODO: Delete
    member x.WaitForLongVisualCommand command keyRemapMode kind func =
        _data <- { _data with State = NotFinishWithCommand(command, keyRemapMode) }

        let data = x.CreateCommandRunData command None None None
        let rec inner result = 
            match result with
            | LongCommandResult.Finished(commandResult) -> RanCommand (data, commandResult)
            | LongCommandResult.Cancelled -> CancelledCommand
            | LongCommandResult.NeedMoreInput(keyRemapMode, func) -> 
                _data <- { _data with State = NotFinishWithCommand(command, keyRemapMode) }
                NeedMore (fun ki -> func ki |> inner)

        let visualSpan = x.GetVisualSpan kind
        let data = x.CreateCommandRunData command None (Some visualSpan) None
        func _data.Count _data.Register visualSpan |> inner

    /// Wait for a complex binding operation to complete.  When the binding is
    /// complete run the resulting command 
    member x.WaitForComplexBinding binding bindResult completeFunc =

        // Function which loops until we complete the binding
        let rec inner bindResult = 
            match bindResult with
            | BindResult.Complete value ->
                completeFunc value
            | BindResult.NeedMoreInput (keyRemapMode, func) -> 
                _data <- { _data with State = NotFinishWithCommand(binding, keyRemapMode) }
                NeedMore (fun keyInput -> inner (func keyInput))
            | BindResult.Cancelled ->
                CancelledCommand
            | BindResult.Error msg ->
                _statusUtil.OnError msg
                CancelledCommand

        inner bindResult

    /// Wait for a complex binding to complete and then run the associated 
    member x.WaitForComplexBindingThenRun binding keyInputOpt getResultFunc runFunc =

        let completeFunc data =  runFunc data binding

        let inner keyInput =
            let result = getResultFunc keyInput
            x.WaitForComplexBinding binding result completeFunc

        match keyInputOpt with
        | None ->
            _data <- { _data with State = NotFinishWithCommand(binding, None) }
            NeedMore inner
        | Some keyInput ->
            inner keyInput

    /// Wait for a motion binding to be complete
    member x.WaitForMotion commandBinding completeFunc keyInputOpt =

        // When we have the first KeyInput start to bind the motion
        let inner keyInput = 
            let result = _capture.GetOperatorMotion keyInput _data.Count
            x.WaitForComplexBinding commandBinding result completeFunc

        match keyInputOpt with
        | None -> NeedMore inner
        | Some keyInput -> inner keyInput

    /// Wait for a motion binding to be complete and then run the associated 
    /// NormalCommand
    member x.WaitForMotionThenRun commandBinding convertFunc keyInputOpt =

        let run (motionData, _) =
            let normalCommand = convertFunc motionData
            x.RunNormalCommand normalCommand commandBinding

        x.WaitForMotion commandBinding run keyInputOpt

    /// Run the given NormalCommand value
    member x.RunNormalCommand command commandBinding =
        let data = { Count = _data.Count; RegisterName = Some _data.Register.Name }
        let result = _commandUtil.RunNormalCommand command data 

        let data = x.CreateCommandRunData commandBinding None None (Some (Command2.NormalCommand (command, data)))
        RanCommand (data, result)

    /// Run the given VisualCommand value
    member x.RunVisualCommand command commandBinding =
        let visualSpan = x.GetVisualSpan _visualKind
        let data = { Count = _data.Count; RegisterName = Some _data.Register.Name }
        let result = _commandUtil.RunVisualCommand command data visualSpan

        let data = x.CreateCommandRunData commandBinding None (Some visualSpan) (Some (Command2.VisualCommand (command, data, visualSpan)))
        RanCommand (data, result)

    /// Try and run a command with the given name
    member x.RunCommand commandName previousCommandName currentInput = 

        // Find any commands which have the given prefix
        let findPrefixMatches (commandName:KeyInputSet) =
            let commandInputs = commandName.KeyInputs
            let count = List.length commandInputs
            let commandInputsSeq = commandInputs |> Seq.ofList
            _commandMap
            |> Seq.map (fun pair -> pair.Value)
            |> Seq.filter (fun command -> command.KeyInputSet.KeyInputs.Length >= count)
            |> Seq.filter (fun command -> 
                let short = command.KeyInputSet.KeyInputs |> Seq.ofList |> Seq.take count
                SeqUtil.contentsEqual commandInputsSeq short)

        match Map.tryFind commandName _commandMap with
        | Some(command) ->

            match command with
            | CommandBinding.SimpleCommand(_,_,func) -> 
                let data = x.CreateCommandRunData command None None None
                let result = func _data.Count _data.Register
                RanCommand (data,result)
            | CommandBinding.NormalCommand2(_, _, normalCommand) -> 
                x.RunNormalCommand normalCommand command
            | CommandBinding.VisualCommand(_,_,kind,func) -> 
                let visualSpan = x.GetVisualSpan kind
                let data = x.CreateCommandRunData command None (Some visualSpan) None
                let result = func _data.Count _data.Register visualSpan
                RanCommand (data,result)
            | CommandBinding.VisualCommand2(_, _, visualCommand) ->
                x.RunVisualCommand visualCommand command
            | CommandBinding.MotionCommand(_, _, func) -> 
                // Can't just call this.  It's possible there is a non-motion command with a 
                // longer command commandInputs.  If there are any other commands which have a 
                // matching prefix we can't bind to the command yet
                let withPrefix = 
                    findPrefixMatches commandName
                    |> Seq.filter (fun c -> c.KeyInputSet <> command.KeyInputSet)
                if Seq.isEmpty withPrefix then 
                    // Nothing else matched so we are good to go for this motion.
                    x.WaitForMotionOld command func None
                else 
                    // At least one other command matched so we need at least one more piece of input to
                    // differentiate the commands.  At this point though because the command is of the
                    // motion variety we are in operator pending
                    _data <- {_data with State = NotEnoughMatchingPrefix (command, withPrefix |> List.ofSeq, Some KeyRemapMode.OperatorPending)}
                    NeedMore x.WaitForCommand
            | CommandBinding.MotionCommand2 (_, _, func) -> 
                // Can't just call this.  It's possible there is a non-motion command with a 
                // longer command commandInputs.  If there are any other commands which have a 
                // matching prefix we can't bind to the command yet
                let withPrefix = 
                    findPrefixMatches commandName
                    |> Seq.filter (fun c -> c.KeyInputSet <> command.KeyInputSet)
                if Seq.isEmpty withPrefix then 
                    // Nothing else matched so we are good to go for this motion.
                    x.WaitForMotionThenRun command func None
                else 
                    // At least one other command matched so we need at least one more piece of input to
                    // differentiate the commands.  At this point though because the command is of the
                    // motion variety we are in operator pending
                    _data <- {_data with State = NotEnoughMatchingPrefix (command, withPrefix |> List.ofSeq, Some KeyRemapMode.OperatorPending)}
                    NeedMore x.WaitForCommand

            | CommandBinding.LongCommand(_,_,func) -> 
                x.WaitForLongCommand command None func
            | CommandBinding.LongVisualCommand(_, _, kind, func) ->
                x.WaitForLongVisualCommand command None kind func
            | CommandBinding.ComplexNormalCommand (_, _, func) -> 
                x.WaitForComplexBindingThenRun command None func x.RunNormalCommand
            | CommandBinding.ComplexVisualCommand (_, _, func) -> 
                x.WaitForComplexBindingThenRun command None func x.RunVisualCommand
        | None -> 
            let hasPrefixMatch = findPrefixMatches commandName |> SeqUtil.isNotEmpty
            if commandName.KeyInputs.Length > 1 && not hasPrefixMatch then

                // It's possible to have 2 comamnds with similar prefixes where one of them is a 
                // MotionCommand.  Consider
                //
                //  g~{motion}
                //  g~g~
                //
                // This code path triggers when we get the first character after the motion 
                // command name.  If the new name isn't the prefix of any other command then we can
                // choose the motion 
                match Map.tryFind previousCommandName _commandMap with
                | Some(command) ->
                    match command with
                    | CommandBinding.SimpleCommand _ -> RunCommandResult.NeedMore x.WaitForCommand 
                    | CommandBinding.VisualCommand _ -> RunCommandResult.NeedMore x.WaitForCommand
                    | CommandBinding.LongCommand _ -> RunCommandResult.NeedMore x.WaitForCommand 
                    | CommandBinding.LongVisualCommand _ -> RunCommandResult.NeedMore x.WaitForCommand 
                    | CommandBinding.MotionCommand (_, _, func) -> x.WaitForMotionOld command func (Some currentInput) 
                    | CommandBinding.MotionCommand2 (_, _, func) -> x.WaitForMotionThenRun command func (Some currentInput)
                    | CommandBinding.NormalCommand2 _ -> RunCommandResult.NeedMore x.WaitForCommand
                    | CommandBinding.VisualCommand2 _ -> RunCommandResult.NeedMore x.WaitForCommand
                    | CommandBinding.ComplexNormalCommand _ -> RunCommandResult.NeedMore x.WaitForCommand
                    | CommandBinding.ComplexVisualCommand _ -> RunCommandResult.NeedMore x.WaitForCommand
                | None -> 
                    // No prefix matches and no previous motion so won't ever match a comamand
                    RunCommandResult.NoMatchingCommand
            elif hasPrefixMatch then
                // At least one command with a prefix matching the current input.  Wait for the 
                // next keystroke
                RunCommandResult.NeedMore x.WaitForCommand
            else
                // No prospect of matching a command at this point
                RunCommandResult.NoMatchingCommand
                
    /// Starting point for processing input 
    member x.RunCheckForCountAndRegister (ki:KeyInput) = 
        if ki.Char = '"' then x.WaitForRegister()
        elif ki.IsDigit && ki.Char <> '0' then x.WaitForCount ki
        else x.WaitForCommand ki

    /// Should the Esacpe key cancel the current command
    member x.ShouldEscapeCancelCurrentCommand () = 
        match _data.State with
        | CommandRunnerState.NoInput -> true
        | CommandRunnerState.NotEnoughInput -> true
        | CommandRunnerState.NotEnoughMatchingPrefix(_) -> true
        | CommandRunnerState.NotFinishWithCommand (command, _) -> not command.HandlesEscape

    /// Function which handles all incoming input
    member x.Run (ki:KeyInput) =
        if ki = KeyInputUtil.EscapeKey && x.ShouldEscapeCancelCurrentCommand() then 
            x.ResetState()
            RunKeyInputResult.CommandCancelled
        elif _inRun then 
            RunKeyInputResult.NestedRunDetected
        else
            _data <- {_data with Inputs = ki :: _data.Inputs }
            _inRun <- true
            try

                match _runFunc ki with
                | RunCommandResult.NeedMore(func) -> 
                    _runFunc <- func
                    RunKeyInputResult.NeedMoreKeyInput
                | RunCommandResult.CancelledCommand -> 
                    x.ResetState()
                    RunKeyInputResult.CommandCancelled
                | RunCommandResult.NoMatchingCommand ->
                    x.ResetState()
                    RunKeyInputResult.NoMatchingCommand
                | RunCommandResult.RanCommand(data,result) -> 
                    x.ResetState()
                    _commandRanEvent.Trigger (data,result)
                    match result with
                    | CommandResult.Completed(modeSwitch) -> RunKeyInputResult.CommandRan (data,modeSwitch)
                    | CommandResult.Error(msg) -> RunKeyInputResult.CommandErrored (data,msg)

            finally
                _inRun <-false
            
    member x.Add (command : CommandBinding) = 
        if Map.containsKey command.KeyInputSet _commandMap then 
            invalidArg "command" Resources.CommandRunner_CommandNameAlreadyAdded
        _commandMap <- Map.add command.KeyInputSet command _commandMap
    member x.Remove (name:KeyInputSet) = _commandMap <- Map.remove name _commandMap
    member x.ResetState () =
        _data <- _emptyData
        _runFunc <- x.RunCheckForCountAndRegister 

    interface ICommandRunner with
        member x.Commands = _commandMap |> Seq.map (fun pair -> pair.Value)
        member x.State = _data.State
        member x.IsWaitingForMoreInput = 
            match _data.State with
            | CommandRunnerState.NoInput -> false
            | CommandRunnerState.NotEnoughInput -> true
            | CommandRunnerState.NotFinishWithCommand(_) -> true
            | CommandRunnerState.NotEnoughMatchingPrefix(_) -> true
        member x.KeyRemapMode = 
            match _data.State with
            | CommandRunnerState.NoInput -> None
            | CommandRunnerState.NotEnoughInput -> None
            | CommandRunnerState.NotFinishWithCommand(_, mode) -> mode
            | CommandRunnerState.NotEnoughMatchingPrefix(_, _, mode) -> mode

        member x.Add command = x.Add command
        member x.Remove name = x.Remove name
        member x.ResetState () = x.ResetState()
        member x.Run ki = x.Run ki
        [<CLIEvent>]
        member x.CommandRan = _commandRanEvent.Publish








