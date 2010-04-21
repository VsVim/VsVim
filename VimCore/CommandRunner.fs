#light

namespace Vim
open Microsoft.VisualStudio.Text.Editor

type internal CommandData = {
    Register : Register;
    Count : int option;
    CommandString : string;
    IsWaitingForMoreInput : bool;
}

/// Implementation of the ICommandRunner interface.  
type internal CommandRunner
    ( 
        _textView : ITextView,
        _registerMap : IRegisterMap,
        _statusUtil : IStatusUtil ) =

    /// Represents the empty state for processing commands.  Holds all of the default
    /// values
    let _emptyData = { 
        Register = _registerMap.DefaultRegister;
        Count = None;
        CommandString = StringUtil.empty;
        IsWaitingForMoreInput = false; 
    }
    
    let mutable _commands : Command list = List.Empty

    /// Contains all of the stateful data for a Command operation
    let mutable _data = _emptyData

    /// The current function which handles running input
    let mutable _runFunc : KeyInput -> CommandResult = fun _ -> CommandCompleted

    /// The full command string of the current input
    let mutable _commandString = StringUtil.empty

    let CountOrDefault = 
        match _data.Count with
        | None -> 1
        | Some(count) -> count

    /// Used to wait for the character after the " which signals the Register 
    member private x.WaitForRegister (ki:KeyInput) = 
        let reg = _registerMap.GetRegister ki.Char
        _data <- { _data with Register = reg }
        CommandResult.CommandNeedMoreInput x.RunCore

    /// Used to wait for a count value to complete.  Passed in the initial digit 
    /// in the count
    member private x.WaitForCount (initialDigit:KeyInput) =
        let rec inner (num:string) (ki:KeyInput) = 
            if ki.IsDigit then
                let num = num + ki.Char.ToString()
                CommandNeedMoreInput (inner num)
            else
                let count = System.Int32.Parse(num)
                _data <- { _data with Count = Some count }
                x.RunCore ki 
        inner (initialDigit.Char.ToString())

    /// Used to wait for a MotionCommand to complete.  Will call the passed in function 
    /// if the motion is successfully completed
    member private x.WaitForMotion onMotionComplete (initialInput : KeyInput option) =
        let rec inner (result:MotionResult) = 
            match result with 
                | MotionResult.Complete (data) -> onMotionComplete data
                | NeedMoreInput (moreFunc) ->
                    let func ki = moreFunc ki |> inner
                    CommandNeedMoreInput func
                | InvalidMotion (msg,moreFunc) ->
                    _statusUtil.OnError msg
                    let func ki = moreFunc ki |> inner
                    CommandNeedMoreInput func
                | Error (msg) ->
                    _statusUtil.OnError msg
                    CommandCompleted
                | Cancel -> CommandCancelled

        let runInitialMotion ki =
            let point = TextViewUtil.GetCaretPoint _textView
            MotionCapture.ProcessInput point ki CountOrDefault |> inner

        match initialInput with
        | None -> CommandNeedMoreInput runInitialMotion
        | Some(ki) -> runInitialMotion ki

    /// Waits for a completed command to be entered
    member private x.WaitForCommand (ki:KeyInput) = 

        let commandString = _data.CommandString + (ki.Char.ToString())
        _data <- { _data with CommandString = commandString }
        x.RunCommand commandString
    
    /// Try and run a command with the given name
    member private x.RunCommand name = 

        // Find any commands matching the given name
        let findMatches name =  _commands |> Seq.filter (fun command -> StringUtil.isEqual command.RawCommand name) |> List.ofSeq

        // Run the passed in command
        let runCommand command = 
            match command with
            | SimpleCommand(_,func) -> func _data.Count _data.Register 
            | MotionCommand(_,func) -> 
                // Can't just call this.  It's possible there is a non-motion command with a 
                // longer command name
                let withPrefix = 
                    _commands 
                    |> Seq.filter (fun command -> command.RawCommand.StartsWith(name, System.StringComparison.Ordinal))
                if Seq.isEmpty withPrefix then x.WaitForMotion (fun data -> func _data.Count _data.Register data) None
                else CommandNeedMoreInput x.WaitForCommand

        let matches = findMatches name
        if matches.Length = 1 then matches |> List.head |> runCommand
        elif matches.Length = 0 && name.Length > 1 then
           
          // It's possible to have 2 commands with similar prefixes where one of them is a MotionCommand.  In this
          // case we can now resolve the ambiguity
          let previousName = name.Substring(0, name.Length - 1)
          let previousMatches = findMatches previousName
          if previousMatches.Length = 1 then 
            let command = previousMatches |> List.head
            match command with
            | SimpleCommand(_,_) -> CommandNeedMoreInput x.WaitForCommand
            | MotionCommand(_,func) -> 
                let last = name.Chars(name.Length-1) |> InputUtil.CharToKeyInput
                x.WaitForMotion (fun data -> func _data.Count _data.Register data) (Some last)
          else CommandNeedMoreInput x.WaitForCommand

        else CommandNeedMoreInput x.WaitForCommand

    /// Starting point for processing input 
    member private x.RunCore (ki:KeyInput) = 
        if ki.Char = '"' then CommandNeedMoreInput x.WaitForRegister
        elif ki.IsDigit then CommandNeedMoreInput (x.WaitForCount ki)
        else x.WaitForCommand ki






