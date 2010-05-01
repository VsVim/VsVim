#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal NormalModeData = {
    Command : string;
    IsInRepeatLastChange : bool;
    IsInReplace : bool;
}

type internal NormalMode 
    ( 
        _bufferData : IVimBuffer, 
        _operations : IOperations,
        _incrementalSearch : IIncrementalSearch,
        _statusUtil : IStatusUtil,
        _displayWindowBroker : IDisplayWindowBroker,
        _runner : ICommandRunner,
        _capture : IMotionCapture ) as this = 

    /// Reset state for data in Normal Mode
    let _emptyData = {
        Command = StringUtil.empty;
        IsInRepeatLastChange = false;
        IsInReplace = false;
    }

    /// Contains the state information for Normal mode
    let mutable _data = _emptyData

    let mutable _globalSettingsChangedHandler : System.IDisposable = null

    do
        // Up cast here to work around the F# bug which prevents accessing a CLIEvent from
        // a derived type
        let settings = _bufferData.Settings.GlobalSettings :> IVimSettings
        _globalSettingsChangedHandler <- settings.SettingChanged.Subscribe this.OnGlobalSettingsChanged

    member this.TextView = _bufferData.TextView
    member this.TextBuffer = _bufferData.TextBuffer
    member this.CaretPoint = _bufferData.TextView.Caret.Position.BufferPosition
    member this.Settings = _bufferData.Settings
    member this.IncrementalSearch = _incrementalSearch
    member this.IsCommandRunnerPopulated = _runner.Commands |> SeqUtil.isNotEmpty
    member this.Command = _data.Command
    member this.Commands = 
        this.EnsureCommands()
        _runner.Commands

    member this.IsOperatorPending = 
        let isOperator command = 
            match command with 
            | SimpleCommand(_) -> false
            | LongCommand(_) -> false
            | MotionCommand(_) -> not command.IsMovement

        match _runner.State with
        | NoInput -> false
        | NotEnoughInput -> false
        | NotEnoughMatchingPrefix(command,_) -> isOperator command
        | NotFinishWithCommand(command) -> isOperator command

    member private this.EnsureCommands() = 
        if not this.IsCommandRunnerPopulated then
            this.CreateSimpleCommands()
            |> Seq.append (this.CreateMovementCommands())
            |> Seq.append (this.CreateMotionCommands())
            |> Seq.append (this.CreateLongCommands())
            |> Seq.append (this.CreateCommandsOld())
            |> Seq.iter _runner.Add

            // Add in the special ~ command
            let _,command = this.GetTildeCommand()
            _runner.Add command

    /// Raised when a global setting is changed
    member private this.OnGlobalSettingsChanged (setting:Setting) = 
        
        // If the tildeop setting changes we need to update how we handle it
        if StringUtil.isEqual setting.Name GlobalSettingNames.TildeOpName && this.IsCommandRunnerPopulated then
            let name,command = this.GetTildeCommand()
            _runner.Remove name
            _runner.Add command

    /// Begin an incremental search.  Called when the user types / into the editor
    member this.BeginIncrementalSearch (kind:SearchKind) count reg =
        let before = TextViewUtil.GetCaretPoint _bufferData.TextView
        let rec inner (ki:KeyInput) = 
            match _incrementalSearch.Process ki with
            | SearchComplete -> 
                _bufferData.JumpList.Add before |> ignore
                CommandResult.Completed ModeSwitch.NoSwitch |> LongCommandResult.Finished
            | SearchCancelled -> LongCommandResult.Cancelled
            | SearchNeedMore ->  LongCommandResult.NeedMoreInput inner
        _incrementalSearch.Begin kind
        LongCommandResult.NeedMoreInput inner
    
    member private x.ReplaceChar count reg = 
        let inner (ki:KeyInput) = 
            _data <- { _data with IsInReplace = false }
            if ki.Key = VimKey.EscapeKey then LongCommandResult.Cancelled
            else 
                if not (_operations.ReplaceChar ki count) then
                    _operations.Beep()
                CommandResult.Completed ModeSwitch.NoSwitch |> LongCommandResult.Finished
        _data <- { _data with IsInReplace = true }
        LongCommandResult.NeedMoreInput inner

    member x.WaitJumpToMark (count:int) (reg:Register) =
        let waitForKey (ki:KeyInput)  =
            let res = _operations.JumpToMark ki.Char _bufferData.MarkMap 
            match res with 
            | Modes.Failed(msg) -> _statusUtil.OnError msg
            | _ -> ()
            CommandResult.Completed ModeSwitch.NoSwitch |> LongCommandResult.Finished
        LongCommandResult.NeedMoreInput waitForKey

    /// Process the m[a-z] command.  Called when the m has been input so wait for the next key
    member x.WaitMark (count:int) (reg:Register)= 
        let waitForKey (ki:KeyInput) =
            let cursor = TextViewUtil.GetCaretPoint _bufferData.TextView
            let res = _operations.SetMark _bufferData cursor ki.Char 
            match res with
            | Modes.Failed(_) -> _operations.Beep()
            | _ -> ()
            CommandResult.Completed ModeSwitch.NoSwitch |> LongCommandResult.Finished
        LongCommandResult.NeedMoreInput waitForKey

    /// Implements the '.' operator.  This is a special command in that it cannot be easily routed 
    /// to interfaces like ICommonOperations due to the complexity of repeating the command here.  
    member private this.RepeatLastChange countOpt reg =  
        if _data.IsInRepeatLastChange then _statusUtil.OnError Resources.NormalMode_RecursiveRepeatDetected
        else
            _data <- { _data with IsInRepeatLastChange = true }
            try
                match _bufferData.Vim.ChangeTracker.LastChange with
                | None -> _operations.Beep()
                | Some(lastChange) ->
                    match lastChange with
                    | TextChange(newText) -> _operations.InsertText newText (CommandUtil.CountOrDefault countOpt) |> ignore
                    | CommandChange(data) -> 
                        let countOpt = match countOpt with | Some(count) -> Some(count) | None -> data.Count
                        let reg = data.Register
                        match data.Command with 
                        | SimpleCommand(_,_,func) -> func countOpt reg |> ignore
                        | MotionCommand(_) -> _statusUtil.OnError (Resources.NormalMode_RepeatNotSupportedOnCommand data.Command.CommandName.Name)
                        | LongCommand(_) -> _statusUtil.OnError (Resources.NormalMode_RepeatNotSupportedOnCommand data.Command.CommandName.Name)
            finally
                _data <- { _data with IsInRepeatLastChange = false }

    /// Get the informatoin on how to handle the tilde command based on the current setting for tildeop
    member private this.GetTildeCommand count =
        let name = InputUtil.CharToKeyInput '~' |> OneKeyInput
        let command = 
            if _bufferData.Settings.GlobalSettings.TildeOp then
                let func count reg (data:MotionData) = 
                    _operations.ChangeLetterCase data.OperationSpan
                    CommandResult.Completed ModeSwitch.NoSwitch
                MotionCommand(name, CommandFlags.None, func)
            else
                let func count _ = 
                    let count = CommandUtil.CountOrDefault count
                    _operations.ChangeLetterCaseAtCursor count
                    CommandResult.Completed ModeSwitch.NoSwitch
                SimpleCommand(name, CommandFlags.None, func)
        name,command

    /// Create the set of Command values which are not repeatable 
    member this.CreateLongCommands() = 

        seq {
            yield ("/", CommandFlags.Movement ||| CommandFlags.HandlesEscape, fun count reg -> this.BeginIncrementalSearch SearchKind.ForwardWithWrap count reg)
            yield ("?", CommandFlags.Movement, fun count reg -> this.BeginIncrementalSearch SearchKind.BackwardWithWrap count reg)
            yield ("r", CommandFlags.None, fun count reg -> this.ReplaceChar count reg) 
            yield ("'", CommandFlags.Movement, fun count reg -> this.WaitJumpToMark count reg)
            yield ("`", CommandFlags.Movement, fun count reg -> this.WaitJumpToMark count reg)
            yield ("m", CommandFlags.Movement, fun count reg -> this.WaitMark count reg)
        }
        |> Seq.map (fun (str,kind, func) -> 
            let name = CommandUtil.CreateCommandName str
            let func2 count reg = 
                let count = CommandUtil.CountOrDefault count
                func count reg 
            LongCommand(name, kind, func2))

    /// Create the simple commands
    member this.CreateSimpleCommands() =

        let noSwitch = 
            seq {
                yield ("dd", CommandFlags.Repeatable, fun count reg -> _operations.DeleteLinesIncludingLineBreak count reg)
                yield ("yy", CommandFlags.Repeatable, fun count reg -> 
                    let point = TextViewUtil.GetCaretPoint _bufferData.TextView
                    let point = point.GetContainingLine().Start
                    let span = SnapshotPointUtil.GetLineRangeSpanIncludingLineBreak point count
                    _operations.Yank span MotionKind.Inclusive OperationKind.LineWise reg  )
                yield ("<<", CommandFlags.Repeatable, fun count _ -> _operations.ShiftLinesLeft count)
                yield (">>", CommandFlags.Repeatable, fun count _ -> _operations.ShiftLinesRight count)
                yield ("gJ", CommandFlags.Repeatable, fun count reg -> 
                    let view = _bufferData.TextView
                    let caret = TextViewUtil.GetCaretPoint view
                    _operations.Join caret Modes.JoinKind.KeepEmptySpaces count |> ignore )
                yield ("gp", CommandFlags.Repeatable, fun count reg -> _operations.PasteAfterCursor reg.StringValue 1 reg.Value.OperationKind true |> ignore)
                yield ("gP", CommandFlags.Repeatable, fun count reg -> _operations.PasteBeforeCursor reg.StringValue 1 reg.Value.OperationKind true |> ignore)
                yield ("g_", CommandFlags.Movement, fun _ _ -> _operations.EditorOperations.MoveToLastNonWhiteSpaceCharacter(false))
                yield ("g*", CommandFlags.Movement, fun count _ -> _operations.MoveToNextOccuranceOfPartialWordAtCursor SearchKind.ForwardWithWrap count)
                yield ("g#", CommandFlags.Movement, fun count _ -> _operations.MoveToNextOccuranceOfPartialWordAtCursor SearchKind.BackwardWithWrap count)
                yield ("gt", CommandFlags.Movement, fun count _ -> _operations.GoToNextTab count)
                yield ("gT", CommandFlags.Movement, fun count _ -> _operations.GoToPreviousTab count)
                yield ("zt", CommandFlags.Movement, fun _ _ ->  _operations.EditorOperations.ScrollLineTop())
                yield ("z.", CommandFlags.Movement, fun _ _ -> 
                    _operations.EditorOperations.ScrollLineCenter() 
                    _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false) )
                yield ("zz", CommandFlags.Movement, fun _ _ -> _operations.EditorOperations.ScrollLineCenter() )
                yield ("z-", CommandFlags.Movement, fun _ _ ->
                    _operations.EditorOperations.ScrollLineBottom() 
                    _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false) )
                yield ("zb", CommandFlags.Movement, fun _ _ -> _operations.EditorOperations.ScrollLineBottom() )
            }
            |> Seq.map(fun (str,kind,func) -> (str,kind,func,CommandResult.Completed ModeSwitch.NoSwitch))

        let doSwitch =
            seq {
                yield ("cc", ModeSwitch.SwitchMode ModeKind.Insert, fun count reg ->  
                    let point = TextViewUtil.GetCaretPoint _bufferData.TextView
                    let span = SnapshotPointUtil.GetLineRangeSpanIncludingLineBreak point count
                    let span = SnapshotSpan(point.GetContainingLine().Start,span.End)
                    _operations.DeleteSpan span MotionKind.Inclusive OperationKind.LineWise reg |> ignore )
            }
            |> Seq.map(fun (str,switch,func) -> (str,CommandFlags.None,func,CommandResult.Completed switch))

        let allWithCount = 
            Seq.append noSwitch doSwitch 
            |> Seq.map(fun (str,kind,func,result) -> 
                let name = CommandUtil.CreateCommandName str
                let func2 count reg =
                    let count = CommandUtil.CountOrDefault count
                    func count reg
                    result
                SimpleCommand(name, kind, func2))

        let needCountAsOpt = 
            seq {
                yield (".", CommandFlags.Special, fun count reg -> this.RepeatLastChange count reg)
            }
            |> Seq.map(fun (str,kind,func) -> 
                let name = CommandUtil.CreateCommandName str
                let func2 count reg = 
                    func count reg
                    CommandResult.Completed ModeSwitch.NoSwitch
                SimpleCommand(name, kind, func2))

        let hardName = 
            seq { 
                yield (
                    [InputUtil.CharToKeyInput('z'); InputUtil.VimKeyToKeyInput(VimKey.EnterKey)],
                    CommandFlags.Movement,
                    fun count reg -> 
                        _operations.EditorOperations.ScrollLineTop()
                        _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false) )

            }
            |> Seq.map(fun (list,kind,func) -> 
                let name = CommandName.ManyKeyInputs list
                let func2 count reg =
                    let count = CommandUtil.CountOrDefault count
                    func count reg
                    CommandResult.Completed ModeSwitch.NoSwitch 
                SimpleCommand(name, kind, func2))

        Seq.append allWithCount needCountAsOpt |> Seq.append hardName

    /// Create all motion commands
    member this.CreateMotionCommands() =
    
        let complex : seq<string * ModeKind option * (int -> Register -> MotionData -> unit)> =
            seq {
                yield ("d", None, fun count reg data -> _operations.DeleteSpan data.OperationSpan data.MotionKind data.OperationKind reg |> ignore)
                yield ("y", None, fun count reg data -> _operations.Yank data.OperationSpan data.MotionKind data.OperationKind reg)
                yield ("c", Some ModeKind.Insert, fun count reg data -> _operations.DeleteSpan data.OperationSpan data.MotionKind data.OperationKind reg |> ignore)
                yield ("<", None, fun _ _ data -> _operations.ShiftSpanLeft data.OperationSpan)
                yield (">", None, fun _ _ data -> _operations.ShiftSpanRight data.OperationSpan)
            }

        complex
        |> Seq.map (fun (str,modeKindOpt, func) ->
            let name = CommandUtil.CreateCommandName str
            let func2 count reg data =
                let count = CommandUtil.CountOrDefault count
                func count reg data
                match modeKindOpt with
                | None -> CommandResult.Completed ModeSwitch.NoSwitch
                | Some(modeKind) -> CommandResult.Completed (ModeSwitch.SwitchMode modeKind)
            MotionCommand(name, CommandFlags.None, func2))

    /// Create all of the movement commands
    member this.CreateMovementCommands() =
        let factory = Vim.Modes.CommandFactory(_operations, _capture)
        factory.CreateMovementCommands()

    member this.CreateCommandsOld() =

        let completeOps = 
            seq {
                yield (InputUtil.CharToKeyInput('x'), (fun count reg -> _operations.DeleteCharacterAtCursor count reg))
                yield (InputUtil.VimKeyToKeyInput VimKey.DeleteKey, (fun count reg -> _operations.DeleteCharacterAtCursor count reg))
                yield (InputUtil.CharToKeyInput('X'),  (fun count reg -> _operations.DeleteCharacterBeforeCursor count reg))
                yield (InputUtil.CharToKeyInput('p'), (fun count reg -> _operations.PasteAfterCursor reg.StringValue count reg.Value.OperationKind false))
                yield (InputUtil.CharToKeyInput('P'), (fun count reg -> _operations.PasteBeforeCursor reg.StringValue count reg.Value.OperationKind false))
                yield (InputUtil.CharToKeyInput('n'), (fun count _ -> _operations.MoveToNextOccuranceOfLastSearch count false))
                yield (InputUtil.CharToKeyInput('N'), (fun count _ -> _operations.MoveToNextOccuranceOfLastSearch count true))
                yield (InputUtil.CharToKeyInput('*'), (fun count _ -> _operations.MoveToNextOccuranceOfWordAtCursor SearchKind.ForwardWithWrap count))
                yield (InputUtil.CharToKeyInput('#'), (fun count _ -> _operations.MoveToNextOccuranceOfWordAtCursor SearchKind.BackwardWithWrap count))
                yield (InputUtil.CharToKeyInput('u'), (fun count _ -> _operations.Undo count))
                yield (InputUtil.CharToKeyInput('D'), (fun count reg -> _operations.DeleteLinesFromCursor count reg))
                yield (InputUtil.CharAndModifiersToKeyInput 'r' KeyModifiers.Control, (fun count _ -> _operations.Redo count))
                yield (InputUtil.CharAndModifiersToKeyInput 'u' KeyModifiers.Control, (fun count _ -> _operations.ScrollLines ScrollDirection.Up count))
                yield (InputUtil.CharAndModifiersToKeyInput 'd' KeyModifiers.Control, (fun count _ -> _operations.ScrollLines ScrollDirection.Down count))
                yield (InputUtil.CharAndModifiersToKeyInput 'f' KeyModifiers.Control, (fun count _ -> _operations.ScrollPages ScrollDirection.Down count))
                yield (InputUtil.VimKeyAndModifiersToKeyInput VimKey.DownKey KeyModifiers.Shift, (fun count _ -> _operations.ScrollPages ScrollDirection.Down count))
                yield (InputUtil.VimKeyToKeyInput VimKey.PageDownKey, (fun count _ -> _operations.ScrollPages ScrollDirection.Down count)) 
                yield (InputUtil.CharToKeyInput('J'), (fun count _ -> _operations.JoinAtCaret count))
                yield (InputUtil.CharAndModifiersToKeyInput 'b' KeyModifiers.Control, (fun count _ -> _operations.ScrollPages ScrollDirection.Up count))
                yield (InputUtil.VimKeyAndModifiersToKeyInput VimKey.UpKey KeyModifiers.Shift, (fun count _ -> _operations.ScrollPages ScrollDirection.Up count))
                yield (InputUtil.VimKeyToKeyInput VimKey.PageUpKey , (fun count _ -> _operations.ScrollPages ScrollDirection.Up count)) 
                yield (InputUtil.CharAndModifiersToKeyInput ']' KeyModifiers.Control, (fun _ _ -> _operations.GoToDefinitionWrapper()))
                yield (InputUtil.CharToKeyInput('Y'), (fun count reg -> _operations.YankLines count reg))
                yield (InputUtil.VimKeyToKeyInput VimKey.TabKey, (fun count _ -> _operations.JumpNext count))
                yield (InputUtil.CharAndModifiersToKeyInput 'i' KeyModifiers.Control, (fun count _ -> _operations.JumpNext count))
                yield (InputUtil.CharAndModifiersToKeyInput 'o' KeyModifiers.Control, (fun count _ -> _operations.JumpPrevious count))
                yield (InputUtil.CharToKeyInput('%'), (fun _ _ -> _operations.GoToMatch() |> ignore))
                yield (InputUtil.VimKeyAndModifiersToKeyInput VimKey.PageDownKey KeyModifiers.Control, (fun count _ -> _operations.GoToNextTab count))
                yield (InputUtil.VimKeyAndModifiersToKeyInput VimKey.PageUpKey KeyModifiers.Control, (fun count _ -> _operations.GoToPreviousTab count))
            }  |> Seq.map (fun (ki,func) ->
                    let name = OneKeyInput(ki)
                    let func2 count reg = 
                        let count = CommandUtil.CountOrDefault count
                        func count reg
                        CommandResult.Completed ModeSwitch.NoSwitch
                    SimpleCommand(name, CommandFlags.Repeatable, func2))
    
    
        // Similar to completeOps but take the conditional count value
        let completeOps2 = 
            seq {
                yield (InputUtil.VimKeyAndModifiersToKeyInput VimKey.HomeKey KeyModifiers.Control , (fun count _ -> _operations.GoToLineOrFirst(count)))
            } |> Seq.map (fun (ki,func) ->
                    let name = OneKeyInput(ki)
                    let func2 count reg =
                        func count reg 
                        CommandResult.Completed ModeSwitch.NoSwitch
                    SimpleCommand(name, CommandFlags.Movement, func2) )

        let doNothing _ _ = ()
        let changeOps = 
            seq {
                yield (InputUtil.CharToKeyInput('i'), ModeKind.Insert, doNothing)
                yield (InputUtil.CharToKeyInput('I'), ModeKind.Insert, (fun _ _ -> _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false)))
                yield (InputUtil.CharToKeyInput(':'), ModeKind.Command, doNothing)
                yield (InputUtil.CharToKeyInput('A'), ModeKind.Insert, (fun _ _ -> _operations.EditorOperations.MoveToEndOfLine(false)))
                yield (InputUtil.CharToKeyInput('o'), ModeKind.Insert, (fun _ _ -> _operations.InsertLineBelow() |> ignore))
                yield (InputUtil.CharToKeyInput('O'), ModeKind.Insert, (fun _ _ -> _operations.InsertLineAbove() |> ignore))
                yield (InputUtil.CharToKeyInput('v'), ModeKind.VisualCharacter, doNothing)
                yield (InputUtil.CharToKeyInput('V'), ModeKind.VisualLine, doNothing)
                yield (InputUtil.CharAndModifiersToKeyInput 'q' KeyModifiers.Control, ModeKind.VisualBlock, doNothing)
                yield (InputUtil.CharToKeyInput('s'), ModeKind.Insert, (fun count reg -> _operations.DeleteCharacterAtCursor count reg))
                yield (InputUtil.CharToKeyInput('C'), ModeKind.Insert, (fun count reg -> _operations.DeleteLinesFromCursor count reg))
                yield (InputUtil.CharToKeyInput('S'), ModeKind.Insert, (fun count reg -> _operations.DeleteLines count reg))
                yield (InputUtil.CharToKeyInput('a'), ModeKind.Insert, (fun _ _ -> _operations.MoveCaretRight 1))
            } |> Seq.map (fun (ki,mode,func) ->
                    let name = OneKeyInput(ki)
                    let func2 count reg =
                        let count = CommandUtil.CountOrDefault count
                        func count reg 
                        CommandResult.Completed (ModeSwitch.SwitchMode mode)
                    SimpleCommand(name, CommandFlags.Repeatable, func2))
    
        Seq.append completeOps completeOps2 |> Seq.append changeOps
   
    member this.Reset() =
        _runner.ResetState()
        _data <- _emptyData
    
    member this.ProcessCore (ki:KeyInput) =
        let command = _data.Command + ki.Char.ToString()
        _data <- {_data with Command=command }

        match _runner.Run ki with
        | RunKeyInputResult.NeedMoreKeyInput -> ProcessResult.Processed
        | RunKeyInputResult.NestedRunDetected -> ProcessResult.Processed
        | RunKeyInputResult.CommandRan(_,modeSwitch) ->
            this.Reset()
            match modeSwitch with
            | ModeSwitch.NoSwitch -> ProcessResult.Processed
            | ModeSwitch.SwitchMode(kind) -> ProcessResult.SwitchMode kind
            | ModeSwitch.SwitchPreviousMode -> ProcessResult.SwitchPreviousMode
        | RunKeyInputResult.CommandErrored(_) -> 
            this.Reset()
            ProcessResult.Processed
        | RunKeyInputResult.CommandCancelled -> 
            this.Reset()
            ProcessResult.Processed
        | RunKeyInputResult.NoMatchingCommand ->
            this.Reset()
            _operations.Beep()
            ProcessResult.Processed

    interface INormalMode with 
        member this.IsOperatorPending = this.IsOperatorPending
        member this.IsWaitingForInput = _runner.IsWaitingForMoreInput
        member this.IncrementalSearch = _incrementalSearch
        member this.IsInReplace = _data.IsInReplace
        member this.VimBuffer = _bufferData
        member this.Command = this.Command
        member this.CommandRunner = _runner
        member this.CommandNames = 
            this.EnsureCommands()
            _runner.Commands |> Seq.map (fun command -> command.CommandName)

        member this.ModeKind = ModeKind.Normal

        member this.CanProcess (ki:KeyInput) =
            let doesCommandStartWith ki =
                let name = OneKeyInput ki
                _runner.Commands 
                |> Seq.filter (fun command -> command.CommandName.StartsWith name)
                |> SeqUtil.isNotEmpty

            if _displayWindowBroker.IsSmartTagWindowActive then false                
            elif _runner.IsWaitingForMoreInput then  true
            elif CharUtil.IsLetterOrDigit(ki.Char) then true
            elif doesCommandStartWith ki then true
            elif InputUtil.CoreCharactersSet |> Set.contains ki.Char then true
            else false

        member this.Process ki = this.ProcessCore ki
        member this.OnEnter () = 
            this.EnsureCommands()
            this.Reset()
        member this.OnLeave () = ()
        member this.OnClose() = _globalSettingsChangedHandler.Dispose()
    

