#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
open System.Windows.Threading
open Vim
open Vim.Modes

type internal VisualModeResult =
    | Complete
    | SwitchPreviousMode
    | NeedMore

type internal Operation = int -> Register -> VisualModeResult

type internal CommandData =  {
    RunFunc : KeyInput -> VisualModeResult;
    IsWaitingForData: bool;
    Count : int;
    Register : Register } 

type internal VisualMode
    (
        _buffer : IVimBuffer,
        _operations : IOperations,
        _kind : ModeKind ) as this = 

    let _selectionTracker = _operations.SelectionTracker 

    /// Tracks the count of explicit moves we are seeing.  Normally an explicit character
    /// move causes the selection to be removed.  Updating this counter is a way of our 
    /// consumers to tell us the caret move is legal
    let mutable _explicitMoveCount = 0

    let mutable _operationsMap : Map<KeyInput,Operation> = Map.empty

    let mutable _caretMovedHandler = ToggleHandler.Empty

    let mutable _data = { RunFunc = (fun _ -> VisualModeResult.Complete); IsWaitingForData = false; Count=1;Register = _buffer.RegisterMap.DefaultRegister }

    do
        _caretMovedHandler <- ToggleHandler.Create (_buffer.TextView.Caret.PositionChanged) (fun _ -> this.OnCaretMoved())

    member x.InExplicitMove = _explicitMoveCount > 0
    member x.BeginExplicitMove() = _explicitMoveCount <- _explicitMoveCount + 1
    member x.EndExplicitMove() = _explicitMoveCount <- _explicitMoveCount - 1

    member private x.ResetCommandData() =
        _data <- {
            RunFunc=x.ProcessInput;
            IsWaitingForData=false;
            Count=1;
            Register=_buffer.RegisterMap.DefaultRegister }

    member private x.BuildMoveSequence() = 
        let wrap func = 
            fun count reg ->
                x.BeginExplicitMove()
                func(count) 
                x.EndExplicitMove()
                VisualModeResult.Complete
        let factory = Vim.Modes.CommandFactory(_operations)
        factory.CreateMovementCommands()
            |> Seq.map (fun (ki,com) -> (ki,wrap com))

    member private x.ProcessGChar count reg  =
        let inner (ki:KeyInput) =  
            match ki.Char with 
            | 'J' -> _operations.JoinSelection JoinKind.KeepEmptySpaces |> ignore
            | _ -> ()
            VisualModeResult.SwitchPreviousMode
        _data <- {_data with RunFunc=inner }
        VisualModeResult.NeedMore

    member private x.BuildOperationsSequence() =
        let deleteSelection _ reg = 
            _operations.DeleteSelection reg |> ignore
            VisualModeResult.SwitchPreviousMode
        let s : seq<KeyInput * Operation> = 
            seq {
                yield (InputUtil.CharToKeyInput('y'), 
                    (fun _ (reg:Register) -> 
                        _operations.YankText (_selectionTracker.SelectedText) MotionKind.Inclusive OperationKind.CharacterWise reg
                        VisualModeResult.SwitchPreviousMode))
                yield (InputUtil.CharToKeyInput('Y'),
                    (fun _ (reg:Register) ->
                        let selection = _buffer.TextView.Selection
                        let startPoint = selection.Start.Position.GetContainingLine().Start
                        let endPoint = selection.End.Position.GetContainingLine().EndIncludingLineBreak
                        let span = SnapshotSpan(startPoint,endPoint)
                        _operations.Yank span MotionKind.Inclusive OperationKind.LineWise reg
                        VisualModeResult.SwitchPreviousMode))
                yield (InputUtil.CharToKeyInput('d'), deleteSelection)
                yield (InputUtil.CharToKeyInput('x'), deleteSelection)
                yield (InputUtil.KeyToKeyInput(Key.Delete), deleteSelection)
                yield (InputUtil.CharToKeyInput('g'), x.ProcessGChar)
                yield (InputUtil.CharToKeyInput('J'), 
                        (fun _ _ ->         
                            _operations.JoinSelection JoinKind.RemoveEmptySpaces|> ignore
                            VisualModeResult.SwitchPreviousMode))
                }
        s

    member private x.EnsureOperationsMap () = 
        if _operationsMap.Count = 0 then
            let map = 
                x.BuildMoveSequence() 
                |> Seq.append (x.BuildOperationsSequence())
                |> Map.ofSeq
                |> Map.add (InputUtil.KeyToKeyInput(Key.Escape)) (fun _ _ -> SwitchPreviousMode)
            _operationsMap <- map

    /// Called when the caret is moved. If we are not explicitly moving the caret then we need to switch
    /// out of Visual mode and back to normal mode.  Do this at background though so we don't interfer with
    /// other processing
    member private x.OnCaretMoved() =
        if not x.InExplicitMove then
            let func() = 
                if _selectionTracker.IsRunning then
                    _buffer.SwitchMode ModeKind.Normal |> ignore
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new System.Action(func)) |> ignore

    member private x.ProcessInputCore ki = 
        let op = Map.find ki _operationsMap
        op (_data.Count) _data.Register

    member private x.ProcessInput ki = 
        if ki.Char = '"' then
            let waitReg (ki:KeyInput) = 
                let reg = _buffer.RegisterMap.GetRegister (ki.Char)
                _data <- { _data with
                            RunFunc=x.ProcessInput;
                            Register=reg;
                            IsWaitingForData=false; }
                NeedMore
            _data <- { _data with RunFunc=waitReg; IsWaitingForData=true }
            NeedMore
        elif ki.IsDigit then
            let rec waitCount (ki:KeyInput) (getResult: KeyInput->CountResult) = 
                let res = getResult ki
                match res with 
                | CountResult.Complete(count,ki) ->
                    _data <- { _data with
                                RunFunc=x.ProcessInput;
                                Count=count;
                                IsWaitingForData=false; }
                    x.ProcessInput ki 
                | CountResult.NeedMore(nextFunc) -> 
                    let runFunc = fun ki -> waitCount ki nextFunc
                    _data <- { _data with
                                RunFunc=runFunc;
                                IsWaitingForData=true };
                    VisualModeResult.NeedMore
            waitCount ki (CountCapture.Process)
        else 
            x.ProcessInputCore ki

    interface IMode with
        member x.VimBuffer = _buffer
        member x.Commands = 
            x.EnsureOperationsMap()
            _operationsMap |> Seq.map (fun pair -> pair.Key)
        member x.ModeKind = _kind
        member x.CanProcess (ki:KeyInput) = _operationsMap.ContainsKey(ki)
        member x.Process (ki : KeyInput) =  
            if ki = InputUtil.KeyToKeyInput(Key.Escape) then 
                ProcessResult.SwitchPreviousMode
            else
                let res = _data.RunFunc ki
                match res with
                | VisualModeResult.Complete -> 
                    _buffer.VimHost.UpdateStatus(Resources.VisualMode_Banner)
                    x.ResetCommandData()
                    ProcessResult.Processed
                | VisualModeResult.SwitchPreviousMode -> ProcessResult.SwitchPreviousMode
                | VisualModeResult.NeedMore -> ProcessResult.Processed
    
        member x.OnEnter () = 
            x.ResetCommandData()
            _caretMovedHandler.Add()
            x.EnsureOperationsMap()
            _selectionTracker.Start()
            _buffer.VimHost.UpdateStatus(Resources.VisualMode_Banner)
        member x.OnLeave () = 
            _caretMovedHandler.Remove()
            _selectionTracker.Stop()
            _buffer.VimHost.UpdateStatus(System.String.Empty)

    interface IVisualMode with
        member x.BeginExplicitMove() = x.BeginExplicitMove()
        member x.EndExplicitMove() = x.EndExplicitMove()


