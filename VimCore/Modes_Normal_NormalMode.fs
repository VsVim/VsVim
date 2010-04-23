#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal NormalModeData = {
    IsOperatorPending : bool;
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
        _runner : ICommandRunner ) =

    let _commandExecutedEvent = Event<_>()

    /// Reset state for data in Normal Mode
    let _emptyData = {
        IsOperatorPending = false;
        IsInRepeatLastChange = false;
        IsInReplace = false;
    }

    /// Contains the state information for Normal mode
    let mutable _data = _emptyData

    member this.TextView = _bufferData.TextView
    member this.TextBuffer = _bufferData.TextBuffer
    member this.CaretPoint = _bufferData.TextView.Caret.Position.BufferPosition
    member this.Settings = _bufferData.Settings
    member this.IncrementalSearch = _incrementalSearch

    /// Begin an incremental search.  Called when the user types / into the editor
    member this.BeginIncrementalSearch (kind:SearchKind) =
        let before = TextViewUtil.GetCaretPoint _bufferData.TextView
        let rec inner (ki:KeyInput) = 
            match _incrementalSearch.Process ki with
            | SearchComplete -> 
                _bufferData.JumpList.Add before |> ignore
                CommandResult.Completed ModeSwitch.NoSwitch |> LongCommandResult.Finished
            | SearchCancelled -> LongCommandResult.Cancelled
            | SearchNeedMore ->  LongCommandResult.NeedMoreInput inner
        _incrementalSearch.Begin kind
        (fun _ _ -> LongCommandResult.NeedMoreInput inner)
    
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
    member private this.RepeatLastChange countOpt =  
        failwith "Need to re-implement repeat last change"
//        if _isInRepeatLastChange then _statusUtil.OnError Resources.NormalMode_RecursiveRepeatDetected
//        else
//            _isInRepeatLastChange <- true
//            try
//                match _bufferData.Vim.ChangeTracker.LastChange with
//                | None -> _operations.Beep()
//                | Some(lastChange) ->
//                    match lastChange with
//                    | TextChange(newText) -> _operations.InsertText newText (CountOrDefault countOpt) |> ignore
//                    | NormalModeChange(keyInputs,count,_) -> 
//                        let count = match countOpt with | Some(c) -> c | None -> count
//                        _data <- {_data with Count=Some(count); }
//                        keyInputs |> Seq.iter (fun ki -> this.ProcessCore ki |> ignore)
//            finally
//                _isInRepeatLastChange <- false

    /// Handle the ~.  This is a special key because it's behavior changes based on the tildeop option
    member private this.HandleTilde count =
        failwith "Need to re-implement this"
//        if _bufferData.Settings.GlobalSettings.TildeOp then
//            let func (data:MotionData) =
//                _operations.ChangeLetterCase data.Span
//                NormalModeResult.Complete
//            this.WaitForMotionAsResult func
//        else
//            _operations.ChangeLetterCaseAtCursor count
//            NormalModeResult.Complete
//

    /// Create the set of Command values which are not repeatable 
    member this.CreateLongCommands() = 

        seq {
            yield ("/", CommandKind.Movement, this.BeginIncrementalSearch SearchKind.ForwardWithWrap)
            yield ("?", CommandKind.Movement, this.BeginIncrementalSearch SearchKind.BackwardWithWrap)
            yield ("r", CommandKind.NotRepeatable, fun count reg -> this.ReplaceChar count reg)
            yield ("'", CommandKind.Movement, fun count reg -> this.WaitJumpToMark count reg)
            yield ("`", CommandKind.Movement, fun count reg -> this.WaitJumpToMark count reg)
            yield ("m", CommandKind.Movement, fun count reg -> this.WaitMark count reg)
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
                yield ("dd", CommandKind.Repeatable, fun count reg -> _operations.DeleteLinesIncludingLineBreak count reg)
                yield ("yy", CommandKind.Repeatable, fun count reg -> 
                    let point = TextViewUtil.GetCaretPoint _bufferData.TextView
                    let point = point.GetContainingLine().Start
                    let span = SnapshotPointUtil.GetLineRangeSpanIncludingLineBreak point count
                    _operations.Yank span MotionKind.Inclusive OperationKind.LineWise reg  )
                yield ("<", CommandKind.Repeatable, fun count _ -> _operations.ShiftLinesLeft count)
                yield (">", CommandKind.Repeatable, fun count _ -> _operations.ShiftLinesRight count)
                yield ("gJ", CommandKind.Repeatable, fun count reg -> 
                    let view = _bufferData.TextView
                    let caret = TextViewUtil.GetCaretPoint view
                    _operations.Join caret Modes.JoinKind.KeepEmptySpaces count |> ignore )
                yield ("gp", CommandKind.Repeatable, fun count reg -> _operations.PasteAfterCursor reg.StringValue 1 reg.Value.OperationKind true |> ignore)
                yield ("gP", CommandKind.Repeatable, fun count reg -> _operations.PasteBeforeCursor reg.StringValue 1 reg.Value.OperationKind true |> ignore)
                yield ("g_", CommandKind.Movement, fun _ _ -> _operations.EditorOperations.MoveToLastNonWhiteSpaceCharacter(false))
                yield ("g*", CommandKind.Movement, fun count _ -> _operations.MoveToNextOccuranceOfPartialWordAtCursor SearchKind.ForwardWithWrap count)
                yield ("g#", CommandKind.Movement, fun count _ -> _operations.MoveToNextOccuranceOfPartialWordAtCursor SearchKind.BackwardWithWrap count)
                yield ("gt", CommandKind.Movement, fun count _ -> _operations.GoToNextTab count)
                yield ("gT", CommandKind.Movement, fun count _ -> _operations.GoToPreviousTab count)
                yield ("zt", CommandKind.Movement, fun _ _ ->  _operations.EditorOperations.ScrollLineTop())
                yield ("z.", CommandKind.Movement, fun _ _ -> 
                    _operations.EditorOperations.ScrollLineCenter() 
                    _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false) )
                yield ("zz", CommandKind.Movement, fun _ _ -> _operations.EditorOperations.ScrollLineCenter() )
                yield ("z-", CommandKind.Movement, fun _ _ ->
                    _operations.EditorOperations.ScrollLineBottom() 
                    _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false) )
                yield ("zb", CommandKind.Movement, fun _ _ -> _operations.EditorOperations.ScrollLineBottom() )
            }
            |> Seq.map(fun (str,kind,func) -> (str,kind,func,CommandResult.Completed ModeSwitch.NoSwitch))

        let doSwitch =
            seq {
                yield ("c", ModeSwitch.SwitchMode ModeKind.Insert, fun count reg ->  
                    let point = TextViewUtil.GetCaretPoint _bufferData.TextView
                    let span = SnapshotPointUtil.GetLineRangeSpanIncludingLineBreak point count
                    let span = SnapshotSpan(point.GetContainingLine().Start,span.End)
                    _operations.DeleteSpan span MotionKind.Inclusive OperationKind.LineWise reg |> ignore )
            }
            |> Seq.map(fun (str,switch,func) -> (str,CommandKind.NotRepeatable,func,CommandResult.Completed switch))

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
                yield ("gg", CommandKind.Movement, fun count _ -> _operations.GoToLineOrFirst count)
                yield (".", CommandKind.Special, fun count _ -> this.RepeatLastChange count)
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
                    CommandKind.Movement,
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
    
        let complex : seq<string * (int -> Register -> MotionData -> unit)> =
            seq {
                yield ("d", fun count reg data -> _operations.DeleteSpan data.OperationSpan |> ignore)
                yield ("y", fun count reg data -> _operations.Yank data.OperationSpan data.MotionKind data.OperationKind reg)
                yield ("c", fun count reg data -> _operations.DeleteSpan data.OperationSpan data.MotionKind data.OperationKind reg |> ignore)
                yield ("<", fun _ _ data -> _operations.ShiftSpanLeft data.OperationSpan)
                yield (">", fun _ _ data -> _operations.ShiftSpanRight data.OperationSpan)
            }

        complex
        |> Seq.map (fun (str,func) ->
            let name = CommandUtil.CreateCommandName str
            let func2 count reg data =
                let count = CommandUtil.CountOrDefault count
                func count reg data
                CommandResult.Completed ModeSwitch.NoSwitch
            MotionCommand(name, CommandKind.NotRepeatable, func2))

    /// Create all of the movement commands
    member this.CreateMovementCommands() =
        let factory = Vim.Modes.CommandFactory(_operations)
        factory.CreateMovementCommands()

    member this.CreateCommandsOld() =

        let completeOps = 
            seq {
                yield (InputUtil.CharToKeyInput('x'), (fun count reg -> _operations.DeleteCharacterAtCursor count reg))
                yield (InputUtil.VimKeyToKeyInput VimKey.DeleteKey, (fun count reg -> _operations.DeleteCharacterAtCursor count reg))
                yield (InputUtil.CharToKeyInput('X'),  (fun count reg -> _operations.DeleteCharacterBeforeCursor count reg))
                yield (InputUtil.CharToKeyInput('p'), (fun count reg -> _operations.PasteAfterCursor reg.StringValue count reg.Value.OperationKind false))
                yield (InputUtil.CharToKeyInput('P'), (fun count reg -> _operations.PasteBeforeCursor reg.StringValue count reg.Value.OperationKind false))
                yield (InputUtil.CharToKeyInput('0'), (fun _ _ -> _operations.EditorOperations.MoveToStartOfLine(false))) 
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
                    SimpleCommand(name, CommandKind.Repeatable, func2))
    
    
        // Similar to completeOps but take the conditional count value
        let completeOps2 = 
            seq {
                yield (InputUtil.CharToKeyInput('G'), (fun count _ -> _operations.GoToLineOrLast(count)))
                yield (InputUtil.VimKeyAndModifiersToKeyInput VimKey.HomeKey KeyModifiers.Control , (fun count _ -> _operations.GoToLineOrFirst(count)))
            } |> Seq.map (fun (ki,func) ->
                    let name = OneKeyInput(ki)
                    let func2 count reg =
                        func count reg 
                        CommandResult.Completed ModeSwitch.NoSwitch
                    SimpleCommand(name, CommandKind.Repeatable, func2) )

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
                    SimpleCommand(name, CommandKind.Repeatable, func2))
    
        Seq.append completeOps completeOps2 |> Seq.append changeOps
   
    /// Reset the internal data for the NormalMode instance
    member this.ResetData() = 
        _data <- {Count=None;Register=_bufferData.RegisterMap.DefaultRegister;KeyInputs=List.empty;Command=""}
        _runFunc <- this.StartCore
        _waitingForMoreInput <- false
        _isOperatingPending <- false
        _isInReplace <- false
        if _operationMap.Count = 0 then
            _operationMap <- this.BuildOperationsMap

    member this.Register = _data.Register   
    member this.Count = _data.Count
    member this.Command = _data.Command

    member this.ProcessCore ki =

        // Update the command string
        let commandInputs = ki :: _data.KeyInputs
        let command = _data.Command + (ki.Char.ToString())
        _data <- {_data with KeyInputs=commandInputs;Command=command }

        let rec inner ki = 
            match _runFunc ki this.Count this.Register with
                | NormalModeResult.Complete ->
                    this.ResetData()
                    _commandExecutedEvent.Trigger NonRepeatableCommand
                    Processed
                | NormalModeResult.CompleteNotCommand ->
                    this.ResetData()
                    Processed
                | NormalModeResult.CompleteRepeatable(count,reg) ->
                    let commandInputs = _data.KeyInputs
                    this.ResetData()
                    _commandExecutedEvent.Trigger (RepeatableCommand((commandInputs |> List.rev),count,reg))
                    Processed
                | NormalModeResult.NeedMoreInput(f) ->
                    _runFunc <- (fun ki count reg -> f ki (CountOrDefault count) reg)
                    _waitingForMoreInput <- true
                    Processed
                | NormalModeResult.NeedMoreInput2(f) ->
                    _runFunc <- f 
                    _waitingForMoreInput <- true
                    Processed
                | NormalModeResult.OperatorPending(f) ->
                    _runFunc <- (fun ki count reg -> f ki (CountOrDefault count) reg)
                    _waitingForMoreInput <- true
                    _isOperatingPending <- true
                    Processed
                | NormalModeResult.SwitchMode (kind) -> 
                    this.ResetData() // Make sure to reset information when switching modes
                    ProcessResult.SwitchMode kind
                | CountComplete (count,nextKi) ->
                    _data <- {_data with Count=Some(count);KeyInputs=[nextKi]};
                    _runFunc <- this.StartCore

                    // Do not go to Process or ProcessCore here because it will record the key for 
                    // a second time
                    inner nextKi
                | RegisterComplete (reg) ->     
                    _data <- {_data with Register=reg;KeyInputs=List.empty }
                    _runFunc <- this.StartCore
                    _waitingForMoreInput <- false
                    Processed

        inner ki
    
    interface INormalMode with 
        member this.IsOperatorPending = _isOperatingPending
        member this.IsWaitingForInput = _waitingForMoreInput
        member this.IncrementalSearch = _incrementalSearch
        member this.IsInReplace = _isInReplace
        member this.VimBuffer = _bufferData
        member this.Command = this.Command
        member this.Commands = 
            _operationMap
                |> Map.toSeq
                |> Seq.map (fun (k,v) -> k)

        member this.ModeKind = ModeKind.Normal

        [<CLIEvent>] 
        member this.CommandExecuted = _commandExecutedEvent.Publish

        member this.CanProcess (ki:KeyInput) =
            if _displayWindowBroker.IsSmartTagWindowActive then false                
            elif _waitingForMoreInput then  true
            elif CharUtil.IsLetterOrDigit(ki.Char) then true
            elif _operationMap.ContainsKey ki then true
            elif InputUtil.CoreCharactersSet |> Set.contains ki.Char then true
            else false

        member this.Process ki = this.ProcessCore ki
        member this.OnEnter ()  =
            this.ResetData()
        member this.OnLeave () = ()
    

