#light

namespace Vim
open Microsoft.VisualStudio.Text.Editor

type internal RunResult = 
    | RanCommand of CommandRunData * CommandResult
    | CancelledCommand
    | NeedMore of (KeyInput -> RunResult)
    | NoMatchingCommand

type internal CommandData = {
    Register : Register;
    Count : int option;

    /// Name of the current command
    CommandName : CommandName;

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
        _statusUtil : IStatusUtil ) as this =

    /// Represents the empty state for processing commands.  Holds all of the default
    /// values
    let _emptyData = { 
        Register = _registerMap.DefaultRegister;
        Count = None;
        CommandName = EmptyName;
        Inputs = List.empty;
        State = CommandRunnerState.NoInput
    }

    let _commandRanEvent = Event<_>()
    
    let mutable _commandMap : Map<CommandName,Command> = Map.empty

    /// Contains all of the state data for a Command operation
    let mutable _data = _emptyData

    /// The current function which handles running input
    let mutable _runFunc : KeyInput -> RunResult = fun _ -> RunResult.CancelledCommand

    /// True during the running of a particular KeyInput 
    let mutable _inRun = false

    do
        _runFunc <- this.RunCheckForCountAndRegister

    /// Create a CommandRunData based on the current state for the given command information
    member private x.CreateCommandRunData command motionDataOpt = 
        {  
            Command=command;
            Register = _data.Register;
            Count = _data.Count;
            MotionData = motionDataOpt; }

    /// Used to wait for the character after the " which signals the Register 
    member private x.WaitForRegister() = 

        let inner (ki:KeyInput) = 
            let reg = _registerMap.GetRegister ki.Char
            _data <- { _data with Register = reg }
            NeedMore x.RunCheckForCountAndRegister

        _data <- {_data with State = NotEnoughInput }
        NeedMore inner

    /// Used to wait for a count value to complete.  Passed in the initial digit 
    /// in the count
    member private x.WaitForCount (initialDigit:KeyInput) =
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

    /// Used to wait for a MotionCommand to complete.  Will call the passed in function 
    /// if the motion is successfully completed
    member private x.WaitForMotion command onMotionComplete (initialInput : KeyInput option) =
        _data <- { _data with State = NotFinishWithCommand(command) }
        let rec inner (result:MotionResult) = 
            match result with 
                | MotionResult.Complete (motionData,_) -> 
                    let data = x.CreateCommandRunData command (Some motionData)
                    let result = onMotionComplete data.Count data.Register motionData
                    RanCommand (data,result)
                | MotionResult.NeedMoreInput (moreFunc) ->
                    let func ki = moreFunc ki |> inner
                    NeedMore func
                | MotionResult.Error (msg) ->
                    _statusUtil.OnError msg
                    CancelledCommand
                | MotionResult.Cancelled -> CancelledCommand

        let runInitialMotion ki =
            let count = CommandUtil.CountOrDefault _data.Count
            _capture.GetMotion ki (Some count) |> inner

        match initialInput with
        | None -> NeedMore runInitialMotion
        | Some(ki) -> runInitialMotion ki

    /// Certain commands require additional data on top of their initial command 
    /// name.  They will return NeedMoreKeyInput until they receive it all
    member private x.WaitForAdditionalCommandInput func =
        let inner ki = func ki |> RanCommand
        inner

    /// Waits for a completed command to be entered
    member private x.WaitForCommand (ki:KeyInput) = 
        let previousName = _data.CommandName
        let commandName = previousName.Add ki
        _data <- { _data with CommandName = commandName; State = NotEnoughInput }
        x.RunCommand commandName previousName ki

    /// Wait for a long command to complete
    member private x.WaitForLongCommand command func =
        _data <- { _data with State = NotFinishWithCommand(command) }

        let data = x.CreateCommandRunData command None
        let rec inner result = 
            match result with
            | LongCommandResult.Finished(commandResult) -> RanCommand (data,commandResult)
            | LongCommandResult.Cancelled -> CancelledCommand
            | LongCommandResult.NeedMoreInput(func) -> NeedMore (fun ki -> func ki |> inner)
    
        func data.Count data.Register |> inner

    /// Try and run a command with the given name
    member private x.RunCommand commandName previousCommandName currentInput = 

        // Find any commands which have the given prefix
        let findPrefixMatches (commandName:CommandName) =
            let commandInputs = commandName.KeyInputs
            let count = List.length commandInputs
            let commandInputsSeq = commandInputs |> Seq.ofList
            _commandMap
            |> Seq.map (fun pair -> pair.Value)
            |> Seq.filter (fun command -> command.CommandName.KeyInputs.Length >= count)
            |> Seq.filter (fun command -> 
                let short = command.CommandName.KeyInputs |> Seq.ofList |> Seq.take count
                SeqUtil.contentsEqual commandInputsSeq short)

        // Run the specified command
        let runCommand command func =  
            let data = x.CreateCommandRunData command None
            let result = func _data.Count _data.Register 
            RanCommand (data,result)

        match Map.tryFind commandName _commandMap with
        | Some(command) ->
            match command with
            | Command.SimpleCommand(_,_,func) -> runCommand command func
            | Command.MotionCommand(_,_,func) -> 

                // Can't just call this.  It's possible there is a non-motion command with a 
                // longer command commandInputs.  If there are any other commands which have a 
                // matching prefix we can't bind to the command yet
                let withPrefix = 
                    findPrefixMatches commandName
                    |> Seq.filter (fun c -> c.CommandName <> command.CommandName)
                if Seq.isEmpty withPrefix then x.WaitForMotion command func None
                else 
                    let state = NotEnoughMatchingPrefix (command, withPrefix |> List.ofSeq)
                    _data <- {_data with State = state }
                    NeedMore x.WaitForCommand
            | Command.LongCommand(_,_,func) -> x.WaitForLongCommand command func
        | None -> 
            let result = 
                if commandName.KeyInputs.Length > 1 then
                    // It's possible to have 2 commands with similar prefixes where one of them is a MotionCommand.  In this
                    // case we can now resolve the ambiguity
                    match Map.tryFind previousCommandName _commandMap with
                    | Some(command) ->
                        let waitResult =
                            match command with
                            | Command.SimpleCommand(_) -> NeedMore x.WaitForCommand 
                            | Command.LongCommand(_) -> NeedMore x.WaitForCommand 
                            | Command.MotionCommand(_,_,func) -> x.WaitForMotion command func (Some currentInput) 
                        Some waitResult
                    | None -> None
                else None
            match result with
            | Some(value) -> value
            | None -> 
                
                // At this point we need to see if there will ever be a command given the 
                // current starting point with respect to characters
                if findPrefixMatches commandName |> Seq.isEmpty then RunResult.NoMatchingCommand
                else NeedMore x.WaitForCommand
                
    /// Starting point for processing input 
    member private x.RunCheckForCountAndRegister (ki:KeyInput) = 
        if ki.Char = '"' then x.WaitForRegister()
        elif ki.IsDigit && ki.Char <> '0' then x.WaitForCount ki
        else x.WaitForCommand ki

    /// Should the Esacpe key cancel the current command
    member private x.ShouldEscapeCancelCurrentCommand () = 
        match _data.State with
        | CommandRunnerState.NoInput -> true
        | CommandRunnerState.NotEnoughInput -> true
        | CommandRunnerState.NotEnoughMatchingPrefix(_) -> true
        | CommandRunnerState.NotFinishWithCommand (command) -> not command.HandlesEscape

    /// Function which handles all incoming input
    member private x.Run (ki:KeyInput) =
        if ki.Key = VimKey.EscapeKey && x.ShouldEscapeCancelCurrentCommand() then 
            x.ResetState()
            RunKeyInputResult.CommandCancelled
        elif _inRun then 
            RunKeyInputResult.NestedRunDetected
        else
            _data <- {_data with Inputs = ki :: _data.Inputs }
            _inRun <- true
            try

                match _runFunc ki with
                | RunResult.NeedMore(func) -> 
                    _runFunc <- func
                    RunKeyInputResult.NeedMoreKeyInput
                | RunResult.CancelledCommand -> 
                    x.ResetState()
                    RunKeyInputResult.CommandCancelled
                | RunResult.NoMatchingCommand ->
                    x.ResetState()
                    RunKeyInputResult.NoMatchingCommand
                | RunResult.RanCommand(data,result) -> 
                    x.ResetState()
                    _commandRanEvent.Trigger (data,result)
                    match result with
                    | CommandResult.Completed(modeSwitch) -> RunKeyInputResult.CommandRan (data,modeSwitch)
                    | CommandResult.Error(msg) -> RunKeyInputResult.CommandErrored (data,msg)

            finally
                _inRun <-false
            
    member x.Add (command:Command) = 
        if Map.containsKey command.CommandName _commandMap then 
            invalidArg "command" Resources.CommandRunner_CommandNameAlreadyAdded
        _commandMap <- Map.add command.CommandName command _commandMap
    member x.Remove (name:CommandName) = _commandMap <- Map.remove name _commandMap
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

        member x.Add command = x.Add command
        member x.Remove name = x.Remove name
        member x.ResetState () = x.ResetState()
        member x.Run ki = x.Run ki
        [<CLIEvent>]
        member x.CommandRan = _commandRanEvent.Publish








