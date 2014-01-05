#light

namespace Vim
open Microsoft.VisualStudio.Text.Editor

[<NoComparison>]
[<NoEquality>]
type internal CommandRunnerData = {

    /// Name of the current command
    KeyInputSet : KeyInputSet

    /// Reverse ordered List of all KeyInput for a given command
    Inputs : KeyInput list

    /// Once a CommandBinding is chosen this will be the flags of that 
    /// CommandBinding instance
    CommandFlags : CommandFlags option
}

/// Implementation of the ICommandRunner interface.  
type internal CommandRunner
    ( 
        _textView : ITextView,
        _registerMap : IRegisterMap,
        _motionCapture : IMotionCapture,
        _localSettings : IVimLocalSettings,
        _commandUtil : ICommandUtil,
        _statusUtil : IStatusUtil,
        _visualKind : VisualKind,
        _defaultKeyRemapMode : KeyRemapMode
    ) =

    /// Represents the empty state for processing commands.  Holds all of the default
    /// values
    let _emptyData = { 
        KeyInputSet = KeyInputSet.Empty
        Inputs = List.empty
        CommandFlags = None
    }

    let _commandRanEvent = StandardEvent<CommandRunDataEventArgs>()
    
    let mutable _commandMap : Map<KeyInputSet, CommandBinding> = Map.empty

    /// Contains all of the state data for a Command operation
    let mutable _data = _emptyData

    /// The latest BindData we are waiting to receive KeyInput to complete
    let mutable _runBindData : BindData<Command * CommandBinding> option = None

    /// True during the running of a particular KeyInput 
    let mutable _inBind = false

    /// True during the binding of the count
    let mutable _inCount = false

    /// Try and get the VisualSpan for the provided kind
    member x.GetVisualSpan kind = VisualSpan.CreateForSelection _textView kind _localSettings.TabStop

    /// Used to wait for the character after the " which signals the Register.  When the register
    /// is found it will be passed to completeFunc
    member x.BindRegister completeFunc = 

        let inner (keyInput : KeyInput) = 
            match RegisterNameUtil.CharToRegister keyInput.Char with
            | None -> BindResult.Error
            | Some name -> completeFunc name

        BindResult.NeedMoreInput { KeyRemapMode = KeyRemapMode.None; BindFunction = inner }

    /// Used to wait for a count value to complete.  Passed in the initial digit 
    /// in the count
    member x.BindCount (initialDigit : KeyInput) completeFunc =

        let rec inner (num:string) (ki:KeyInput) = 
            if ki.IsDigit then
                _inCount <- true
                let num = num + ki.Char.ToString()
                BindResult.NeedMoreInput { KeyRemapMode = _defaultKeyRemapMode; BindFunction = inner num }
            else
                _inCount <- false
                let count = System.Int32.Parse(num)
                completeFunc count ki

        inner StringUtil.empty initialDigit 

    /// Bind the optional register and count operations and then pass that off to the command 
    /// infrastructure
    member x.BindCountAndRegister (keyInput : KeyInput) =

        let tryRegister (keyInput : KeyInput) foundFunc missingFunc = 
            if keyInput.Char = '"' then x.BindRegister foundFunc
            else missingFunc keyInput

        let tryCount (keyInput : KeyInput) foundFunc missingFunc = 
            if keyInput.IsDigit && keyInput.Char <> '0' then x.BindCount keyInput foundFunc
            else missingFunc keyInput

        // Okay now we get to build some fun.  It's possible to enter register and count
        // in either order with either being missing.  So we need to try all of the possibilities
        // here
        let bindCommandResult register count =
            let func = x.BindCommand register count
            let data = { KeyRemapMode = KeyRemapMode.None; BindFunction = func }
            BindResult.NeedMoreInput data

        let tryCountThenRegister keyInput missingCount = 
            let foundCount count keyInput =
                let count = Some count
                tryRegister keyInput (fun register -> bindCommandResult (Some register) count) (fun keyInput -> x.BindCommand None count keyInput)

            tryCount keyInput foundCount missingCount

        let tryRegisterThenCount keyInput missingRegister = 
            let foundRegister register = 
                let register = Some register
                let next keyInput = tryCount keyInput (fun count keyInput -> x.BindCommand register (Some count) keyInput) (fun keyInput -> x.BindCommand register None keyInput)
                BindResult.NeedMoreInput { KeyRemapMode = _defaultKeyRemapMode; BindFunction = next }

            tryRegister keyInput foundRegister missingRegister

        // Now we put it altogether
        tryRegisterThenCount keyInput (fun keyInput -> tryCountThenRegister keyInput (fun keyInput -> x.BindCommand None None keyInput))

    /// Bind a Motion command.  
    ///
    /// This function will take into account the special motion commands delete and yank.  Both of these special
    /// cases are specific to these commands and these commands only.  For example 'd#d' makes it appear like
    /// 'd' is a valid motion but it is not and can be verified with other commands (like yank or change).
    ///
    /// This makes the function feel a bit hacky but sadly it's just a special case in Vim which requires a 
    /// corresponding special case here
    member x.BindMotion (commandBinding : CommandBinding) (commandData : CommandData) motionFunc keyInput =

        // Convert the motion information into a BindResult.Complete value
        let convertMotion motion motionCount =
            let argument = { MotionContext = MotionContext.AfterOperator; OperatorCount = commandData.Count; MotionCount = motionCount }
            let data = { Motion = motion; MotionArgument = argument }
            let command = motionFunc data
            (Command.NormalCommand (command, commandData), commandBinding)

        // Handle the special case of 'd' and 'y' 
        let doSpecialBinding normalCommand targetChar = 

            let result = CountCapture.GetCount KeyRemapMode.None keyInput
            result.Map (fun (countOpt, keyInput) -> 

                if keyInput.Char = targetChar then

                    // This is the special case.  Get the count for the combined command.  It's the 
                    // multiplier of the two counts
                    let count = 
                        match commandData.Count, countOpt with
                        | Some left, Some right -> Some (left * right)
                        | Some left, None -> Some left
                        | None, Some right -> Some right
                        | None, None -> None
                    let commandData = { commandData with Count = count }
                    let command = Command.NormalCommand (normalCommand, commandData)
                    BindResult.Complete (command, commandBinding)
                else
                    // Not the special case so just do a normal motion mapping.  We have the count at 
                    // this point so go straight for the motion
                    let result = _motionCapture.GetMotion keyInput
                    result.Convert (fun motion -> convertMotion motion countOpt))

        if Util.IsFlagSet commandBinding.CommandFlags CommandFlags.Delete then
            doSpecialBinding NormalCommand.DeleteLines 'd'
        elif Util.IsFlagSet commandBinding.CommandFlags CommandFlags.Yank then
            doSpecialBinding NormalCommand.YankLines 'y'
        elif Util.IsFlagSet commandBinding.CommandFlags CommandFlags.Change then
            doSpecialBinding NormalCommand.ChangeLines 'c'
        elif Util.IsFlagSet commandBinding.CommandFlags CommandFlags.ShiftRight then
            doSpecialBinding NormalCommand.ShiftLinesRight '>'
        elif Util.IsFlagSet commandBinding.CommandFlags CommandFlags.ShiftLeft then
            doSpecialBinding NormalCommand.ShiftLinesLeft '<'
        else
            let result = _motionCapture.GetMotionAndCount keyInput
            result.Convert (fun (motion, motionCount) -> convertMotion motion motionCount)

    /// Bind the Command instance
    member x.BindCommand registerName count keyInput = 

        // Find any commands which have the given prefix
        let findPrefixMatches (commandName : KeyInputSet) =
            let commandInputs = commandName.KeyInputs
            let count = List.length commandInputs
            let commandInputsSeq = commandInputs |> Seq.ofList
            _commandMap
            |> Seq.map (fun pair -> pair.Value)
            |> Seq.filter (fun command -> command.KeyInputSet.KeyInputs.Length >= count)
            |> Seq.filter (fun command -> 
                let short = command.KeyInputSet.KeyInputs |> Seq.ofList |> Seq.take count
                SeqUtil.contentsEqual commandInputsSeq short)

        let commandData = { RegisterName = registerName; Count = count }

        let rec inner (commandName : KeyInputSet) previousCommandName currentInput = 

            // Used to continue driving the 'inner' function a BindData value.
            let bindNext keyRemapMode = 
                let inner keyInput = 
                    let previousCommandName = commandName
                    let commandName = previousCommandName.Add keyInput
                    inner commandName previousCommandName keyInput
                BindResult.NeedMoreInput { KeyRemapMode = keyRemapMode; BindFunction = inner }

            match Map.tryFind commandName _commandMap with
            | Some commandBinding ->
                match commandBinding with
                | CommandBinding.NormalBinding (_, _, normalCommand) -> 
                    BindResult.Complete (Command.NormalCommand (normalCommand, commandData), commandBinding)
                | CommandBinding.InsertBinding (_, _, insertCommand) ->
                    BindResult.Complete (Command.InsertCommand insertCommand, commandBinding)
                | CommandBinding.VisualBinding (_, _, visualCommand) ->
                    let visualSpan = x.GetVisualSpan _visualKind
                    let visualCommand = Command.VisualCommand (visualCommand, commandData, visualSpan)
                    BindResult.Complete (visualCommand, commandBinding)
                | CommandBinding.MotionBinding (_, _, func) -> 
                    // Can't just call this.  It's possible there is a non-motion command with a 
                    // longer command commandInputs.  If there are any other commands which have a 
                    // matching prefix we can't bind to the command yet
                    let withPrefix = 
                        findPrefixMatches commandName
                        |> Seq.filter (fun c -> c.KeyInputSet <> commandBinding.KeyInputSet)
                    if Seq.isEmpty withPrefix then 
                        // Nothing else matched so we are good to go for this motion.
                        _data <- { _data with CommandFlags = Some commandBinding.CommandFlags }
                        BindResult<_>.CreateNeedMoreInput KeyRemapMode.OperatorPending (fun keyInput -> x.BindMotion commandBinding commandData func keyInput)
                    else 
                        // At least one other command matched so we need at least one more piece of input to
                        // differentiate the commands.  At this point though because the command is of the
                        // motion variety we are in operator pending
                        _data <- { _data with CommandFlags = Some commandBinding.CommandFlags }
                        bindNext KeyRemapMode.OperatorPending

                | CommandBinding.ComplexNormalBinding (_, _, bindDataStorage) -> 
                    _data <- { _data with CommandFlags = Some commandBinding.CommandFlags }
                    let bindData = bindDataStorage.CreateBindData()
                    let bindData = bindData.Convert (fun normalCommand -> (Command.NormalCommand (normalCommand, commandData), commandBinding))
                    BindResult.NeedMoreInput bindData
                | CommandBinding.ComplexVisualBinding (_, _, bindDataStorage) -> 
                    _data <- { _data with CommandFlags = Some commandBinding.CommandFlags }
                    let bindData = bindDataStorage.CreateBindData()
                    let bindData = bindData.Map (fun visualCommand -> 
                        let visualSpan = x.GetVisualSpan _visualKind
                        let visualCommand = Command.VisualCommand (visualCommand, commandData, visualSpan)
                        BindResult.Complete (visualCommand, commandBinding))
                    BindResult.NeedMoreInput bindData
            | None ->
                let hasPrefixMatch = findPrefixMatches commandName |> SeqUtil.isNotEmpty
                if commandName.KeyInputs.Length > 1 && not hasPrefixMatch then
    
                    // It's possible to have 2 commands with similar prefixes where one of them is a 
                    // MotionCommand.  Consider
                    //
                    //  g~{motion}
                    //  g~g~
                    //
                    // This code path triggers when we get the first character after the motion 
                    // command name.  If the new name isn't the prefix of any other command then we can
                    // choose the motion 
                    match Map.tryFind previousCommandName _commandMap with
                    | Some command ->
                        match command with
                        | CommandBinding.MotionBinding (_, _, func) -> 
                            _data <- { _data with CommandFlags = Some command.CommandFlags }
                            x.BindMotion command commandData func currentInput
                        | CommandBinding.NormalBinding _ -> 
                            bindNext KeyRemapMode.None
                        | CommandBinding.VisualBinding _ -> 
                            bindNext KeyRemapMode.None
                        | CommandBinding.InsertBinding _ ->
                            bindNext KeyRemapMode.None
                        | CommandBinding.ComplexNormalBinding _ -> 
                            bindNext KeyRemapMode.None
                        | CommandBinding.ComplexVisualBinding _ -> 
                            bindNext KeyRemapMode.None
                    | None -> 
                        // No prefix matches and no previous motion so won't ever match a command
                        BindResult.Error
                elif hasPrefixMatch then
                    // At least one command with a prefix matching the current input.  Wait for the 
                    // next keystroke
                    bindNext KeyRemapMode.None
                else
                    // No prospect of matching a command at this point
                    BindResult.Error

        // Lets get it started
        inner (KeyInputSet.OneKeyInput keyInput) KeyInputSet.Empty keyInput

    /// Should the Escape key cancel the current command
    member x.ShouldEscapeCancelCurrentCommand () = 
        match _data.CommandFlags with
        | None -> true
        | Some flags -> not (Util.IsFlagSet flags CommandFlags.HandlesEscape)

    /// Function which handles all incoming input
    member x.Run (ki:KeyInput) =
        if ki = KeyInputUtil.EscapeKey && x.ShouldEscapeCancelCurrentCommand() then 
            x.ResetState()
            BindResult.Cancelled
        elif _inBind then 
            // If we're in the middle of binding the previous value then error.  We don't
            // support reentrancy while binding
            BindResult.Error
        else
            _data <- {_data with Inputs = ki :: _data.Inputs }
            let result = 
                _inBind <- true
                try
                    match _runBindData with
                    | Some bindData -> bindData.BindFunction ki
                    | None -> x.BindCountAndRegister ki
                finally 
                    _inBind <-  false

            match result with
            | BindResult.Complete (command, commandBinding) -> 
                x.ResetState()
                let result = _commandUtil.RunCommand command
                let data = { Command = command; CommandBinding = commandBinding; CommandResult = result }
                let args = CommandRunDataEventArgs(data)
                _commandRanEvent.Trigger x args
                BindResult.Complete data
            | BindResult.Cancelled ->
                x.ResetState()
                BindResult.Cancelled
            | BindResult.Error ->
                x.ResetState()
                BindResult.Error
            | BindResult.NeedMoreInput bindData ->
                _runBindData <- Some bindData
                BindResult.NeedMoreInput { KeyRemapMode = bindData.KeyRemapMode; BindFunction = x.Run }
            
    member x.Add (command : CommandBinding) = 
        if Map.containsKey command.KeyInputSet _commandMap then 
            invalidArg "command" Resources.CommandRunner_CommandNameAlreadyAdded
        _commandMap <- Map.add command.KeyInputSet command _commandMap
    member x.Remove (name:KeyInputSet) = _commandMap <- Map.remove name _commandMap
    member x.ResetState () =
        _data <- _emptyData
        _runBindData <- None
        _inCount <- false

    interface ICommandRunner with
        member x.Commands = _commandMap |> Seq.map (fun pair -> pair.Value)
        member x.IsHandlingEscape =
            match _data.CommandFlags with
            | None -> false
            | Some flags -> Util.IsFlagSet flags CommandFlags.HandlesEscape
        member x.IsWaitingForMoreInput = Option.isSome _runBindData
        member x.InCount = _inCount
        member x.KeyRemapMode = 
            match _runBindData with
            | None -> KeyRemapMode.None
            | Some bindData -> bindData.KeyRemapMode
        member x.Add command = x.Add command
        member x.Remove name = x.Remove name
        member x.ResetState () = x.ResetState()
        member x.Run ki = x.Run ki
        [<CLIEvent>]
        member x.CommandRan = _commandRanEvent.Publish








