#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Vim
open Vim.Modes

type internal VisualModeResult =
    | Complete
    | SwitchPreviousMode
    | SwitchInsertMode
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
        _kind : ModeKind ) = 

    let _selectionTracker = _operations.SelectionTracker 

    /// Tracks the count of explicit moves we are seeing.  Normally an explicit character
    /// move causes the selection to be removed.  Updating this counter is a way of our 
    /// consumers to tell us the caret move is legal
    let mutable _explicitMoveCount = 0

    let mutable _operationsMap : Map<KeyInput,Operation> = Map.empty

    let mutable _data = { RunFunc = (fun _ -> VisualModeResult.Complete); IsWaitingForData = false; Count=1;Register = _buffer.RegisterMap.DefaultRegister }

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
        let changeSelection _ reg = 
            _operations.DeleteSelection reg |> ignore
            VisualModeResult.SwitchInsertMode
        let changeLines _ reg = 
            _operations.DeleteSelectedLines reg |> ignore
            VisualModeResult.SwitchInsertMode
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
                yield (InputUtil.VimKeyToKeyInput VimKey.DeleteKey, deleteSelection)
                yield (InputUtil.CharToKeyInput('c'), changeSelection)
                yield (InputUtil.CharToKeyInput('s'), changeSelection)
                yield (InputUtil.CharToKeyInput('C'), changeLines)
                yield (InputUtil.CharToKeyInput('S'), changeLines)
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
                |> Map.add (InputUtil.VimKeyToKeyInput VimKey.EscapeKey) (fun _ _ -> SwitchPreviousMode)
            _operationsMap <- map

    member private x.ProcessInputCore ki = 
        match Map.tryFind ki _operationsMap with
        | Some(op) -> op _data.Count _data.Register
        | None -> 
            _buffer.VimHost.Beep()
            VisualModeResult.Complete

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
        member x.CanProcess (ki:KeyInput) = true
        member x.Process (ki : KeyInput) =  
            if ki.Key = VimKey.EscapeKey then
                ProcessResult.SwitchPreviousMode
            else
                let res = _data.RunFunc ki
                match res with
                | VisualModeResult.Complete -> 
                    _buffer.VimHost.UpdateStatus(Resources.VisualMode_Banner)
                    x.ResetCommandData()
                    ProcessResult.Processed
                | VisualModeResult.SwitchPreviousMode -> ProcessResult.SwitchPreviousMode
                | VisualModeResult.SwitchInsertMode -> ProcessResult.SwitchMode ModeKind.Insert
                | VisualModeResult.NeedMore -> ProcessResult.Processed
    
        member x.OnEnter () = 
            x.ResetCommandData()
            x.EnsureOperationsMap()
            _selectionTracker.Start()
            _buffer.VimHost.UpdateStatus(Resources.VisualMode_Banner)
        member x.OnLeave () = 
            _selectionTracker.Stop()
            _buffer.VimHost.UpdateStatus(System.String.Empty)

    interface IVisualMode with
        member x.Operations = _operations
        member x.InExplicitMove = x.InExplicitMove



