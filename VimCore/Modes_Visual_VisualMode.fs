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

type internal Operation = Register -> VisualModeResult

type internal VisualMode
    (
        _buffer : IVimBuffer,
        _operations : ICommonOperations,
        _selectionTracker : ISelectionTracker,
        _kind : ModeKind ) as this = 

    /// Tracks the count of explicit moves we are seeing.  Normally an explicit character
    /// move causes the selection to be removed.  Updating this counter is a way of our 
    /// consumers to tell us the caret move is legal
    let mutable _explicitMoveCount = 0

    let mutable _operationsMap : Map<KeyInput,Operation> = Map.empty

    let mutable _caretMovedHandler = ToggleHandler.Empty

    do
        _caretMovedHandler <- ToggleHandler.Create (_buffer.TextView.Caret.PositionChanged) (fun _ -> this.OnCaretMoved())

    member x.InExplicitMove = _explicitMoveCount > 0
    member x.BeginExplicitMove() = _explicitMoveCount <- _explicitMoveCount + 1
    member x.EndExplicitMove() = _explicitMoveCount <- _explicitMoveCount - 1

    member private x.BuildMoveSequence() = 
        let wrap func = 
            fun _ ->
                x.BeginExplicitMove()
                func() 
                x.EndExplicitMove()
                VisualModeResult.Complete
        let moveLeft = wrap (fun () -> _operations.MoveCaretLeft(1))
        let moveRight = wrap (fun () -> _operations.MoveCaretRight(1))
        let moveUp = wrap (fun () -> _operations.MoveCaretUp(1))
        let moveDown = wrap (fun () -> _operations.MoveCaretDown(1))

        let s : seq<KeyInput * Operation> = 
            seq {
                yield (InputUtil.CharToKeyInput('h'), moveLeft)
                yield (InputUtil.KeyToKeyInput(Key.Left), moveLeft)
                yield (InputUtil.KeyToKeyInput(Key.Back), moveLeft)
                yield (KeyInput('h', Key.H, ModifierKeys.Control), moveLeft)
                yield (InputUtil.CharToKeyInput('l'), moveRight)
                yield (InputUtil.KeyToKeyInput(Key.Right), moveRight)
                yield (InputUtil.KeyToKeyInput(Key.Space), moveRight)
                yield (InputUtil.CharToKeyInput('k'), moveUp)
                yield (InputUtil.KeyToKeyInput(Key.Up), moveUp)
                yield (KeyInput('p', Key.P, ModifierKeys.Control), moveUp)
                yield (InputUtil.CharToKeyInput('j'), moveDown)
                yield (InputUtil.KeyToKeyInput(Key.Down), moveDown)
                yield (KeyInput('n', Key.N, ModifierKeys.Control),moveDown)
                yield (KeyInput('j', Key.J, ModifierKeys.Control),moveDown)
                }
        s

    member private x.BuildOperationsSequence() =
        let s : seq<KeyInput * Operation> = 
            seq {
                yield (InputUtil.CharToKeyInput('y'), 
                    (fun (reg:Register) -> 
                        _operations.YankText (_selectionTracker.SelectedText) MotionKind.Inclusive OperationKind.CharacterWise reg
                        VisualModeResult.SwitchPreviousMode))
                yield (InputUtil.CharToKeyInput('Y'),
                    (fun (reg:Register) ->
                        let selection = _buffer.TextView.Selection
                        let startPoint = selection.Start.Position.GetContainingLine().Start
                        let endPoint = selection.End.Position.GetContainingLine().EndIncludingLineBreak
                        let span = SnapshotSpan(startPoint,endPoint)
                        _operations.Yank span MotionKind.Inclusive OperationKind.LineWise reg
                        VisualModeResult.SwitchPreviousMode))
                }
        s

    member private x.EnsureOperationsMap () = 
        if _operationsMap.Count = 0 then
            let map = 
                x.BuildMoveSequence() 
                |> Seq.append (x.BuildOperationsSequence())
                |> Map.ofSeq
                |> Map.add (InputUtil.KeyToKeyInput(Key.Escape)) (fun _ -> SwitchPreviousMode)
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

    interface IMode with
        member x.VimBuffer = _buffer
        member x.Commands = 
            x.EnsureOperationsMap()
            _operationsMap |> Seq.map (fun pair -> pair.Key)
        member x.ModeKind = _kind
        member x.CanProcess (ki:KeyInput) = _operationsMap.ContainsKey(ki)
        member x.Process (ki : KeyInput) =  
            let op = Map.find ki _operationsMap
            let res = op(_buffer.RegisterMap.DefaultRegister)
            match res with
            | VisualModeResult.Complete -> 
                _buffer.VimHost.UpdateStatus(Resources.VisualMode_Banner)
                ProcessResult.Processed
            | VisualModeResult.SwitchPreviousMode -> ProcessResult.SwitchPreviousMode

        member x.OnEnter () = 
            _caretMovedHandler.Add()
            x.EnsureOperationsMap()
            _selectionTracker.Start()
            _buffer.VimHost.UpdateStatus(Resources.VisualMode_Banner)
        member x.OnLeave () = 
            _caretMovedHandler.Remove()
            _selectionTracker.Stop()
            _buffer.VimHost.UpdateStatus(System.String.Empty)


