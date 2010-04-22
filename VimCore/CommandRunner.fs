#light

namespace Vim
open Microsoft.VisualStudio.Text.Editor

type internal KeyInputResult = 
    | RanCommand of CommandResult
    | NeedMoreInput of (KeyInput -> KeyInputResult)

type internal CommandData = {
    Register : Register;
    Count : int option;
    IsWaitingForMoreInput : bool;

    /// In order list for all of the KeyInput values which participate in the command.  Does
    /// not include the KeyInput for Register and Count values
    CommandInputs : KeyInput list;

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
        CommandInputs = List.empty;
        IsWaitingForMoreInput = false; 
        Inputs = List.empty;
    }
    
    let mutable _commands : Command list = List.Empty

    /// Contains all of the state data for a Command operation
    let mutable _data = _emptyData

    /// The current function which handles running input
    let mutable _runFunc : KeyInput -> KeyInputResult = fun _ -> RanCommand CommandCancelled

    /// The full command string of the current input
    let mutable _commandString = StringUtil.empty

    do
        _runFunc <- this.RunCheckForCountAndRegister

    /// Returns the current count if set or otherwise will return 1 
    member x.CountOrDefault = 
        match _data.Count with
        | None -> 1
        | Some(count) -> count

    /// Used to wait for the character after the " which signals the Register 
    member private x.WaitForRegister (ki:KeyInput) = 
        let reg = _registerMap.GetRegister ki.Char
        _data <- { _data with Register = reg }
        NeedMoreInput x.RunCheckForCountAndRegister

    /// Used to wait for a count value to complete.  Passed in the initial digit 
    /// in the count
    member private x.WaitForCount (initialDigit:KeyInput) =
        let rec inner (num:string) (ki:KeyInput) = 
            if ki.IsDigit then
                let num = num + ki.Char.ToString()
                NeedMoreInput (inner num)
            else
                let count = System.Int32.Parse(num)
                _data <- { _data with Count = Some count }
                x.RunCheckForCountAndRegister ki
        inner (initialDigit.Char.ToString())

    /// Used to wait for a MotionCommand to complete.  Will call the passed in function 
    /// if the motion is successfully completed
    member private x.WaitForMotion onMotionComplete (initialInput : KeyInput option) =
        let rec inner (result:MotionResult) = 
            match result with 
                | MotionResult.Complete (data) -> onMotionComplete data |> RanCommand
                | MotionResult.NeedMoreInput (moreFunc) ->
                    let func ki = moreFunc ki |> inner
                    NeedMoreInput func
                | InvalidMotion (msg,moreFunc) ->
                    _statusUtil.OnError msg
                    let func ki = moreFunc ki |> inner
                    NeedMoreInput func
                | Error (msg) ->
                    _statusUtil.OnError msg
                    RanCommand (CommandError (msg))
                | Cancel -> RanCommand CommandCancelled

        let runInitialMotion ki =
            let point = TextViewUtil.GetCaretPoint _textView
            MotionCapture.ProcessInput point ki x.CountOrDefault |> inner

        match initialInput with
        | None -> NeedMoreInput runInitialMotion
        | Some(ki) -> runInitialMotion ki

    /// Waits for a completed command to be entered
    member private x.WaitForCommand (ki:KeyInput) = 

        let previousCommandInputs = _data.CommandInputs
        let commandInputs = previousCommandInputs @ [ki]
        _data <- { _data with CommandInputs = commandInputs }
        x.RunCommand commandInputs previousCommandInputs ki
    
    /// Try and run a command with the given name
    member private x.RunCommand commandInputs previousCommandInputs currentInput = 

        // Find any commands matching the given name
        let findMatches commandInputs =  
            _commands 
            |> Seq.filter (fun command -> ListUtil.contentsEqual command.RawCommand commandInputs) 
            |> List.ofSeq

        // Find any commands which have the given prefix
        let findPrefixMatches commandInputs = 
            let count = List.length commandInputs
            let commandInputsSeq = commandInputs |> Seq.ofList
            _commands
            |> Seq.filter (fun command -> command.RawCommand.Length >= count)
            |> Seq.filter (fun command -> 
                let short = command.RawCommand |> Seq.ofList |> Seq.take count
                SeqUtil.contentsEqual commandInputsSeq short)

        // Run the passed in command
        let runCommand command = 
            match command with
            | SimpleCommand(_,func) -> func _data.Count _data.Register |> RanCommand
            | MotionCommand(_,func) -> 
                // Can't just call this.  It's possible there is a non-motion command with a 
                // longer command commandInputs
                let withPrefix = findPrefixMatches commandInputs
                if Seq.isEmpty withPrefix then x.WaitForMotion (fun data -> func _data.Count _data.Register data) None
                else NeedMoreInput x.WaitForCommand

        let matches = findMatches commandInputs
        if matches.Length = 1 then matches |> List.head |> runCommand
        elif matches.Length = 0 && commandInputs.Length > 1 then
           
          // It's possible to have 2 commands with similar prefixes where one of them is a MotionCommand.  In this
          // case we can now resolve the ambiguity
          let previousMatches = findMatches previousCommandInputs
          if previousMatches.Length = 1 then 
            let command = previousMatches |> List.head
            match command with
            | SimpleCommand(_,_) -> NeedMoreInput x.WaitForCommand
            | MotionCommand(_,func) -> 
                x.WaitForMotion (fun data -> func _data.Count _data.Register data) (Some currentInput)
          else NeedMoreInput x.WaitForCommand

        else NeedMoreInput x.WaitForCommand

    /// Starting point for processing input 
    member private x.RunCheckForCountAndRegister (ki:KeyInput) = 
        if ki.Char = '"' then NeedMoreInput x.WaitForRegister
        elif ki.IsDigit then NeedMoreInput (x.WaitForCount ki)
        else x.WaitForCommand ki

    /// Function which handles all incoming input
    member private x.Run (ki:KeyInput) =
        if ki.Key = VimKey.EscapeKey then 
            x.Reset()
            Some CommandCancelled
        else
            _data <- {_data with Inputs = ki :: _data.Inputs }
            let result = 
                match _runFunc ki with
                | RanCommand(commandResult) -> 
                    x.Reset()
                    Some commandResult
                | NeedMoreInput(func) ->
                    _data <- { _data with IsWaitingForMoreInput= true }
                    _runFunc <- func
                    None
            result
            
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








