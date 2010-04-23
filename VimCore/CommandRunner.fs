#light

namespace Vim
open Microsoft.VisualStudio.Text.Editor

type internal RunResult = 
    | RanCommand of CommandRunData * CommandResult
    | CancelledCommand
    | NeedMore of (KeyInput -> RunResult)

type internal CommandData = {
    Register : Register;
    Count : int option;
    IsWaitingForMoreInput : bool;

    /// Name of the current command
    CommandName : CommandName;

    /// Reverse ordered List of all KeyInput for a given command
    Inputs : KeyInput list;
}

/// Implementation of the ICommandRunner interface.  
type internal CommandRunner
    ( 
        _textView : ITextView,
        _registerMap : IRegisterMap,
        _statusUtil : IStatusUtil ) as this =

    /// Represents the empty state for processing commands.  Holds all of the default
    /// values
    let _emptyData = { 
        Register = _registerMap.DefaultRegister;
        Count = None;
        CommandName = EmptyName;
        IsWaitingForMoreInput = false; 
        Inputs = List.empty;
    }
    
    let mutable _commands : Command list = List.Empty

    /// Contains all of the state data for a Command operation
    let mutable _data = _emptyData

    /// The current function which handles running input
    let mutable _runFunc : KeyInput -> RunResult = fun _ -> RunResult.CancelledCommand

    /// The full command string of the current input
    let mutable _commandString = StringUtil.empty

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
    member private x.WaitForRegister (ki:KeyInput) = 
        let reg = _registerMap.GetRegister ki.Char
        _data <- { _data with Register = reg }
        NeedMore x.RunCheckForCountAndRegister

    /// Used to wait for a count value to complete.  Passed in the initial digit 
    /// in the count
    member private x.WaitForCount (initialDigit:KeyInput) =
        let rec inner (num:string) (ki:KeyInput) = 
            if ki.IsDigit then
                let num = num + ki.Char.ToString()
                NeedMore (inner num)
            else
                let count = System.Int32.Parse(num)
                _data <- { _data with Count = Some count }
                x.RunCheckForCountAndRegister ki
        inner (initialDigit.Char.ToString())

    /// Used to wait for a MotionCommand to complete.  Will call the passed in function 
    /// if the motion is successfully completed
    member private x.WaitForMotion command onMotionComplete (initialInput : KeyInput option) =
        let rec inner (result:MotionResult) = 
            match result with 
                | MotionResult.Complete (motionData) -> 
                    let data = x.CreateCommandRunData command (Some motionData)
                    let result = onMotionComplete data.Count data.Register motionData
                    RanCommand (data,result)
                | MotionResult.NeedMoreInput (moreFunc) ->
                    let func ki = moreFunc ki |> inner
                    NeedMore func
                | InvalidMotion (msg,moreFunc) ->
                    _statusUtil.OnError msg
                    let func ki = moreFunc ki |> inner
                    NeedMore func
                | MotionResult.Error (msg) ->
                    _statusUtil.OnError msg
                    CancelledCommand
                | Cancel -> CancelledCommand

        let runInitialMotion ki =
            let point = TextViewUtil.GetCaretPoint _textView
            let count = CommandUtil.CountOrDefault _data.Count
            MotionCapture.ProcessInput point ki count |> inner

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
        _data <- { _data with CommandName = commandName }
        x.RunCommand commandName previousName ki

    /// Wait for a long command to complete
    member private x.WaitForLongCommand command func =
        let data = x.CreateCommandRunData command None
        let rec inner result = 
            match result with
            | LongCommandResult.Finished(commandResult) -> RanCommand (data,commandResult)
            | LongCommandResult.Cancelled -> CancelledCommand
            | LongCommandResult.NeedMoreInput(func) -> NeedMore (fun ki -> func ki |> inner)
    
        func data.Count data.Register |> inner

    /// Try and run a command with the given name
    member private x.RunCommand commandName previousCommandName currentInput = 

        // Find any commands matching the given name
        let findMatches commandName =  
            _commands 
            |> Seq.filter (fun command -> command.CommandName = commandName)
            |> List.ofSeq

        // Find any commands which have the given prefix
        let findPrefixMatches (commandName:CommandName) =
            let commandInputs = commandName.KeyInputs
            let count = List.length commandInputs
            let commandInputsSeq = commandInputs |> Seq.ofList
            _commands
            |> Seq.filter (fun command -> command.CommandName.KeyInputs.Length >= count)
            |> Seq.filter (fun command -> 
                let short = command.CommandName.KeyInputs |> Seq.ofList |> Seq.take count
                SeqUtil.contentsEqual commandInputsSeq short)

        // Run the specified command
        let runCommand command func =  
            let data = x.CreateCommandRunData command None
            let result = func _data.Count _data.Register 
            RanCommand (data,result)

        let matches = findMatches commandName
        if matches.Length = 1 then 
            let command = matches |> List.head
            match command with
            | Command.SimpleCommand(_,_,func) -> runCommand command func
            | Command.MotionCommand(_,_,func) -> 
                // Can't just call this.  It's possible there is a non-motion command with a 
                // longer command commandInputs
                let withPrefix = findPrefixMatches commandName
                if Seq.isEmpty withPrefix then x.WaitForMotion command func None
                else NeedMore x.WaitForCommand
            | Command.LongCommand(_,_,func) -> x.WaitForLongCommand command func

        elif matches.Length = 0 && commandName.KeyInputs.Length > 1 then
           
          // It's possible to have 2 commands with similar prefixes where one of them is a MotionCommand.  In this
          // case we can now resolve the ambiguity
          let previousMatches = findMatches previousCommandName
          if previousMatches.Length = 1 then 
            let command = previousMatches |> List.head
            match command with
            | Command.SimpleCommand(_) -> NeedMore x.WaitForCommand
            | Command.LongCommand(_) -> NeedMore x.WaitForCommand
            | Command.MotionCommand(_,_,func) -> x.WaitForMotion command func (Some currentInput)
          else NeedMore x.WaitForCommand

        else NeedMore x.WaitForCommand

    /// Starting point for processing input 
    member private x.RunCheckForCountAndRegister (ki:KeyInput) = 
        if ki.Char = '"' then NeedMore x.WaitForRegister
        elif ki.IsDigit then NeedMore (x.WaitForCount ki)
        else x.WaitForCommand ki

    /// Function which handles all incoming input
    member private x.Run (ki:KeyInput) =
        if ki.Key = VimKey.EscapeKey then 
            x.Reset()
            RunKeyInputResult.CommandCancelled
        elif _inRun then 
            RunKeyInputResult.NestedRunDetected
        else
            _data <- {_data with Inputs = ki :: _data.Inputs }
            _inRun <- true
            try

                match _runFunc ki with
                | RunResult.NeedMore(func) -> 
                    _data <- { _data with IsWaitingForMoreInput= true }
                    _runFunc <- func
                    RunKeyInputResult.NeedMoreKeyInput
                | RunResult.CancelledCommand -> 
                    x.Reset()
                    RunKeyInputResult.CommandCancelled
                | RunResult.RanCommand(data,result) -> 
                    x.Reset()
                    match result with
                    | CommandResult.Completed(modeSwitch) -> RunKeyInputResult.CommandRan (data,modeSwitch)
                    | CommandResult.Error(msg) -> RunKeyInputResult.CommandErrored (data,msg)

            finally
                _inRun <-false
            
    member private x.Add command = _commands <- command :: _commands
    member private x.Reset () =
        _data <- _emptyData
        _runFunc <- x.RunCheckForCountAndRegister 

    interface ICommandRunner with
        member x.Commands = _commands |> Seq.ofList
        member x.IsWaitingForMoreInput = _data.IsWaitingForMoreInput
        member x.Add command = x.Add command
        member x.Reset () = x.Reset()
        member x.Run ki = x.Run ki








