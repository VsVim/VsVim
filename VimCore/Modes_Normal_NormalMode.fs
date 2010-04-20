#light

namespace Vim.Modes.Normal
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor


/// Operation in the normal mode
type internal Operation =  {
    KeyInput : Vim.KeyInput;
    IsRepeatable : bool;
    RunFunc : int option -> Register -> NormalModeResult
}

type internal CommandData = {
    Count : int option;
    Register : Register;
    KeyInputs : KeyInput list;
    Command : string
}

type internal NormalMode 
    ( 
        _bufferData : IVimBuffer, 
        _operations : IOperations,
        _incrementalSearch : IIncrementalSearch,
        _statusUtil : Vim.Modes.IStatusUtil,
        _displayWindowBroker : IDisplayWindowBroker ) =

    let _commandExecutedEvent = Event<_>()

    /// Command specific data (count,register,KeyInput list)  The KeyInput list
    /// value is a list of the KeyInputs for the current command.  The head of 
    /// the list is the most recent KeyInput
    let mutable _data = {Count=None;Register=_bufferData.RegisterMap.DefaultRegister;KeyInputs=List.empty;Command=""}

    let mutable _operationMap : Map<KeyInput,Operation> = Map.empty

    /// Function used to process the next piece of input
    let mutable _runFunc : KeyInput -> int option -> Register -> NormalModeResult = (fun _ _ _ -> NormalModeResult.Complete)

    /// Whether or not we are waiting for at least the second keystroke
    /// of a given command
    let mutable _waitingForMoreInput = false

    /// Are we in the operator pending mode?
    let mutable _isOperatingPending = false;

    /// True when in the middle of a repeat last change operation
    let mutable _isInRepeatLastChange = false

    let mutable _isInReplace = false;

    let CountOrDefault count =
        match count with 
        | Some(c) -> c
        | None -> 1

    member this.TextView = _bufferData.TextView
    member this.TextBuffer = _bufferData.TextBuffer
    member this.CaretPoint = _bufferData.TextView.Caret.Position.BufferPosition
    member this.Settings = _bufferData.Settings
    member this.IncrementalSearch = _incrementalSearch
    
    /// Begin an incremental search.  Called when the user types / into the editor
    member this.BeginIncrementalSearch (kind:SearchKind) =
        let before = TextViewUtil.GetCaretPoint _bufferData.TextView
        let rec inner (ki:KeyInput) _ _ = 
            match _incrementalSearch.Process ki with
            | SearchComplete -> 
                _bufferData.JumpList.Add before |> ignore
                NormalModeResult.Complete 
            | SearchCancelled -> NormalModeResult.Complete
            | SearchNeedMore ->  NormalModeResult.NeedMoreInput inner
        _incrementalSearch.Begin kind
        inner
    
    /// Wait for a motion sequence to complete and then call "doneFunc" with the resulting data
    member this.WaitForMotionCore doneFunc = 
        let rec f (result:MotionResult) = 
            match result with 
                | MotionResult.Complete (span) -> 
                    doneFunc span
                | NeedMoreInput (moreFunc) ->
                    let inputFunc ki _ _ = f (moreFunc ki)
                    NormalModeResult.NeedMoreInput inputFunc
                | InvalidMotion (msg,moreFunc) ->
                    _statusUtil.OnError msg
                    let inputFunc ki _ _ = f (moreFunc ki)
                    NormalModeResult.NeedMoreInput inputFunc 
                | Error (msg) ->
                    _statusUtil.OnError msg
                    NormalModeResult.Complete
                | Cancel -> 
                    NormalModeResult.Complete
        let func ki count = MotionCapture.ProcessView _bufferData.TextView ki count |> f
        func

    member this.WaitForMotion ki count doneFunc = (this.WaitForMotionCore doneFunc) ki count
    member this.WaitForMotionAsResult doneFunc = 
        let func = this.WaitForMotionCore doneFunc
        NormalModeResult.NeedMoreInput (fun ki count _ -> func ki count)

    // Respond to the d command.  Need the finish motion
    member this.WaitDelete =
        let inner (ki:KeyInput) count reg =
            match ki.Char with 
                | 'd' -> 
                    _operations.DeleteLinesIncludingLineBreak count reg 
                    NormalModeResult.CompleteRepeatable(count,reg)
                | _ -> 
                    let func (data:MotionData) = 
                        _operations.DeleteSpan data.OperationSpan data.MotionKind data.OperationKind reg |> ignore
                        NormalModeResult.CompleteRepeatable(count,reg)
                    this.WaitForMotion ki count func
        inner
        
    // Respond to the y command.  Need to wait for a complete motion
    member this.WaitYank =
        let inner (ki:KeyInput) count reg =
            match ki.Char with 
                | 'y' -> 
                    let point = TextViewUtil.GetCaretPoint _bufferData.TextView
                    let point = point.GetContainingLine().Start
                    let span = SnapshotPointUtil.GetLineRangeSpanIncludingLineBreak point count
                    _operations.Yank span MotionKind.Inclusive OperationKind.LineWise reg 
                    NormalModeResult.CompleteRepeatable (count,reg)
                | _ ->
                    let inner (data:MotionData) =
                        _operations.Yank data.OperationSpan data.MotionKind data.OperationKind reg
                        NormalModeResult.CompleteRepeatable (count,reg)
                    this.WaitForMotion ki count inner
        inner 

    // Respond to the c command.  Need the finish motion
    member this.WaitChange =
        let inner (ki:KeyInput) count reg =
            match ki.Char with 
                | 'c' -> 
                    let point = TextViewUtil.GetCaretPoint _bufferData.TextView
                    let span = SnapshotPointUtil.GetLineRangeSpanIncludingLineBreak point count
                    let span = SnapshotSpan(point.GetContainingLine().Start,span.End)
                    _operations.DeleteSpan span MotionKind.Inclusive OperationKind.LineWise reg |> ignore
                    NormalModeResult.SwitchMode ModeKind.Insert
                | _ -> 
                    let func (data:MotionData) = 
                        _operations.DeleteSpan data.OperationSpan data.MotionKind data.OperationKind reg |> ignore
                        NormalModeResult.SwitchMode ModeKind.Insert
                    this.WaitForMotion ki count func
        inner

    /// Implement the < operator        
    member this.WaitShiftLeft =
        let inner (ki:KeyInput) count reg =
            match ki.Char with 
                | '<' ->
                    _operations.ShiftLinesLeft count
                    NormalModeResult.CompleteRepeatable(count,reg)
                | _ ->
                    let inner2 (data:MotionData) =
                        _operations.ShiftSpanLeft data.OperationSpan
                        NormalModeResult.CompleteRepeatable(count,reg)
                    this.WaitForMotion ki count inner2
        inner                                            
                    
    /// Implements the > operator.  Check for the special > motion key and then default
    /// back to a standard motion                    
    member this.WaitShiftRight =
        let inner (ki:KeyInput) count reg =
            match ki.Char with
                | '>' ->
                    _operations.ShiftLinesRight count
                    NormalModeResult.CompleteRepeatable(count,reg) 
                | _ ->
                    let inner2 (data:MotionData) = 
                        _operations.ShiftSpanRight data.OperationSpan
                        NormalModeResult.CompleteRepeatable(count,reg) 
                    this.WaitForMotion ki count inner2
        inner
                            
    member private x.WaitReplaceChar = 
        let inner (ki:KeyInput) count reg =
            _isInReplace <- false
            if ki.Key = VimKey.EscapeKey then NormalModeResult.CompleteNotCommand
            else 
                if not (_operations.ReplaceChar ki count) then
                    _operations.Beep()
                NormalModeResult.CompleteRepeatable(count,reg) 
        _isInReplace <- true
        inner

    /// Handles commands which begin with g in normal mode.  This should be called when the g char is
    /// already processed
    member x.WaitCharGCommand =
        let inner (ki:KeyInput) countOpt (reg:Register) =  
            let count = CountOrDefault countOpt
            match ki.Char with
            | 'J' -> 
                let view = _bufferData.TextView
                let caret = TextViewUtil.GetCaretPoint view
                _operations.Join caret Modes.JoinKind.KeepEmptySpaces count |> ignore
            | 'p' -> _operations.PasteAfterCursor reg.StringValue 1 reg.Value.OperationKind true |> ignore
            | 'P' -> _operations.PasteBeforeCursor reg.StringValue 1 reg.Value.OperationKind true |> ignore
            | '_' -> _operations.EditorOperations.MoveToLastNonWhiteSpaceCharacter(false)
            | '*' -> _operations.MoveToNextOccuranceOfPartialWordAtCursor SearchKind.ForwardWithWrap count
            | '#' -> _operations.MoveToNextOccuranceOfPartialWordAtCursor SearchKind.BackwardWithWrap count
            | 'g' -> _operations.GoToLineOrFirst countOpt
            | 't' -> _operations.GoToNextTab count
            | 'T' -> _operations.GoToPreviousTab count
            | _ ->
                _operations.Beep()
            NormalModeResult.CompleteRepeatable(count,reg)
        inner

    /// Implement the commands associated with the z prefix in normal mode
    member x.WaitCharZCommand = 
        let inner (ki:KeyInput) count reg =  
            if ki.IsNewLine then 
                _operations.EditorOperations.ScrollLineTop()
                _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false) 
            else
                match ki.Char with
                | 't' ->  _operations.EditorOperations.ScrollLineTop() 
                | '.' -> 
                    _operations.EditorOperations.ScrollLineCenter() 
                    _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false) 
                | 'z' -> _operations.EditorOperations.ScrollLineCenter() 
                | '-' -> 
                    _operations.EditorOperations.ScrollLineBottom() 
                    _operations.EditorOperations.MoveToStartOfLineAfterWhiteSpace(false) 
                | 'b' -> _operations.EditorOperations.ScrollLineBottom() 
                | _ -> _operations.Beep()
            NormalModeResult.Complete
        inner

    member x.WaitJumpToMark =
        let waitForKey (ki:KeyInput) _ _ =
            let res = _operations.JumpToMark ki.Char _bufferData.MarkMap 
            match res with 
            | Modes.Failed(msg) -> _statusUtil.OnError msg
            | _ -> ()
            NormalModeResult.Complete
        waitForKey

    /// Process the m[a-z] command.  Called when the m has been input so wait for the next key
    member x.WaitMark = 
        let waitForKey (ki:KeyInput) _ _ =
            let cursor = TextViewUtil.GetCaretPoint _bufferData.TextView
            let res = _operations.SetMark _bufferData cursor ki.Char 
            match res with
            | Modes.Failed(_) -> _operations.Beep()
            | _ -> ()
            NormalModeResult.Complete
        waitForKey

    /// Complete the specified motion function            
    member this.MotionFunc view count func =
        let rec runCount count =
            func view |> ignore
            match count with
                | 1 -> NormalModeResult.Complete
                | _ -> runCount (count-1) 
        _operations.EditorOperations.ResetSelection()
        runCount count

    /// Implements the '.' operator.  This is a special command in that it cannot be easily routed 
    /// to interfaces like ICommonOperations due to the complexity of repeating the command here.  
    member private this.RepeatLastChange countOpt =  
        if _isInRepeatLastChange then _statusUtil.OnError Resources.NormalMode_RecursiveRepeatDetected
        else
            _isInRepeatLastChange <- true
            try
                match _bufferData.Vim.ChangeTracker.LastChange with
                | None -> _operations.Beep()
                | Some(lastChange) ->
                    match lastChange with
                    | TextChange(newText) -> _operations.InsertText newText (CountOrDefault countOpt) |> ignore
                    | NormalModeChange(keyInputs,count,_) -> 
                        let count = match countOpt with | Some(c) -> c | None -> count
                        _data <- {_data with Count=Some(count); }
                        keyInputs |> Seq.iter (fun ki -> this.ProcessCore ki |> ignore)
            finally
                _isInRepeatLastChange <- false

    /// Handle the ~.  This is a special key because it's behavior changes based on the tildeop option
    member private this.HandleTilde count =
        if _bufferData.Settings.GlobalSettings.TildeOp then
            let func (data:MotionData) =
                _operations.ChangeLetterCase data.Span
                NormalModeResult.Complete
            this.WaitForMotionAsResult func
        else
            _operations.ChangeLetterCaseAtCursor count
            NormalModeResult.Complete

    member private this.BuildMotionOperationsMap =

        // Wrap a simple motion command
        let wrapSimple func = 
            fun count _ -> 
                let count = CountOrDefault count
                func count
                NormalModeResult.CompleteNotCommand

        // Wrap a complex motion command
        let wrapComplex func = 
            
            /// Process a MovementResult
            let rec inner result = 
                match result with
                | Vim.Modes.MovementComplete -> NormalModeResult.CompleteNotCommand
                | Vim.Modes.MovementNeedMore func -> 
                    let func2 ki _ _ = func ki |> inner
                    NormalModeResult.NeedMoreInput func2
                | Vim.Modes.MovementError msg -> 
                    _statusUtil.OnError msg
                    NormalModeResult.CompleteNotCommand

            fun count _ ->
                let count = CountOrDefault count
                func count |> inner

        let doMap ki command =
            match command with
            | Vim.Modes.SimpleMovementCommand(func) -> (ki, wrapSimple func)
            | Vim.Modes.ComplexMovementCommand(func) -> (ki, wrapComplex func)

        let factory = Vim.Modes.CommandFactory(_operations)
        factory.CreateMovementCommands() |> Seq.map (fun (ki,command) -> doMap ki command)

    member this.BuildOperationsMap = 
        let waitOps = seq {
            yield (InputUtil.CharToKeyInput('d'), (fun () -> this.WaitDelete))
            yield (InputUtil.CharToKeyInput('y'), (fun () -> this.WaitYank))
            yield (InputUtil.CharToKeyInput('c'), (fun () -> this.WaitChange))
        }

        // Similar to waitOpts but contains items which should not go to OperatorPending
        let waitOps2 = seq {
            yield (InputUtil.CharToKeyInput('/'), (fun () -> this.BeginIncrementalSearch SearchKind.ForwardWithWrap))
            yield (InputUtil.CharToKeyInput('?'), (fun () -> this.BeginIncrementalSearch SearchKind.BackwardWithWrap))
            yield (InputUtil.CharToKeyInput('m'), (fun () -> this.WaitMark))
            yield (InputUtil.CharToKeyInput('<'), (fun () -> this.WaitShiftLeft))
            yield (InputUtil.CharToKeyInput('>'), (fun () -> this.WaitShiftRight))
            yield (InputUtil.CharToKeyInput('z'), (fun () -> this.WaitCharZCommand))
            yield (InputUtil.CharToKeyInput('r'), (fun () -> this.WaitReplaceChar))
            yield (InputUtil.CharToKeyInput('\''), (fun () -> this.WaitJumpToMark))
            yield (InputUtil.CharToKeyInput('`'), (fun () -> this.WaitJumpToMark))
        }

        // Similar to waitOpts but has items which need a count option
        let waitOps3 = seq {
            yield (InputUtil.CharToKeyInput('g'), (fun () -> this.WaitCharGCommand))
        }

        let completeOps : seq<KeyInput * (int -> Register -> unit)> = seq {
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
        }

        // Similar to completeOpts but take the conditional count value
        let completeOpts2= seq {
            yield (InputUtil.CharToKeyInput('G'), (fun count _ -> _operations.GoToLineOrLast(count)))
            yield (InputUtil.VimKeyToKeyInput(VimKey.HomeKey) |> InputUtil.SetModifiers KeyModifiers.Control), (fun count _ -> _operations.GoToLineOrFirst(count)) 
        }

        let doNothing _ _ = ()
        let changeOpts = seq {
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
        }

        let l =
            (waitOps |> Seq.map (fun (ki,func) -> (ki,false,fun _ _ -> NormalModeResult.OperatorPending(func()))))
            |> Seq.append (waitOps2 |> Seq.map (fun (ki,func) -> (ki,false,fun _ _  -> NormalModeResult.NeedMoreInput(func()))))
            |> Seq.append (waitOps3 |> Seq.map (fun (ki,func) -> (ki,false,fun _ _ -> NormalModeResult.NeedMoreInput2(func()))))
            |> Seq.append (completeOps |> Seq.map (fun (ki,func) -> (ki,true,(fun count reg -> 
                let count = CountOrDefault count
                func count reg
                NormalModeResult.CompleteRepeatable(count,reg)))))
            |> Seq.append (completeOpts2 |> Seq.map (fun (ki,func) -> (ki,true,fun count reg -> 
                func count reg; 
                NormalModeResult.CompleteRepeatable(CountOrDefault count,reg))))
            |> Seq.append ((InputUtil.CharToKeyInput('.'),true, fun count _ ->
                this.RepeatLastChange count
                NormalModeResult.CompleteNotCommand) |> Seq.singleton)
            |> Seq.append ((InputUtil.CharToKeyInput('~'),true, (fun count _ -> this.HandleTilde (CountOrDefault count))) |> Seq.singleton) 
            |> Seq.append (changeOpts |> Seq.map (fun (ki,kind,func) -> (ki,false,fun count reg -> func (CountOrDefault count) reg; NormalModeResult.SwitchMode kind)))
            |> Seq.map (fun (ki,isRepeatable,func) -> {KeyInput=ki;IsRepeatable=isRepeatable;RunFunc=func})
            |> Seq.append (this.BuildMotionOperationsMap |> Seq.map (fun (ki,func) -> {KeyInput=ki;IsRepeatable=true;RunFunc=func}))
            |> Seq.map (fun d -> d.KeyInput,d)
            |> Map.ofSeq
        l
   
    /// Responsible for getting the count                
    member this.GetCount (ki:KeyInput) = 
        let rec inner ki (func: KeyInput -> CountResult) = 
            match func ki with
                | CountResult.Complete (count,nextKi) ->
                    CountComplete(count, nextKi)
                | CountResult.NeedMore(f) ->
                    let nextFunc ki2 _ _ = inner ki2 f
                    NormalModeResult.NeedMoreInput(nextFunc)
        inner ki (CountCapture.Process)                    
        
    /// Responsible for getting the register 
    member this.GetRegister (m:IRegisterMap) (ki:KeyInput) =
        let reg = m.GetRegister ki.Char
        RegisterComplete(reg)

    member this.StartCore (ki:KeyInput) count reg =
        if ki.IsDigit && ki.Char <> '0' then this.GetCount ki
        elif ki.Char = '"' then 
            let f ki _ _ = this.GetRegister (_bufferData.RegisterMap) ki
            NormalModeResult.NeedMoreInput(f)
        else
            match Map.tryFind ki _operationMap with
            | Some op ->  op.RunFunc count reg 
            | None -> 
                _operations.Beep()
                NormalModeResult.Complete

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
    

