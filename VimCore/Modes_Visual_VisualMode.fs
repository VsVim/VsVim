#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
open Vim
open Vim.Modes

type internal VisualModeResult =
    | Complete
    | SwitchMode of ModeKind

type internal Operation = Register -> VisualModeResult

type internal VisualMode
    (
        _bufferData : IVimBufferData,
        _operations : ICommonOperations,
        _kind : ModeKind ) = 

    let _selectionMode = 
        match _kind with 
        | ModeKind.VisualBlock -> SelectionMode.Block
        | ModeKind.VisualCharacter -> SelectionMode.Character
        | ModeKind.VisualLineWise -> SelectionMode.Line
        | _ -> invalidArg "_kind" "Invalid kind for Visual Mode"
    let _selectionTracker = SelectionTracker(_bufferData.TextView, _selectionMode)

    let mutable _operationsMap : Map<KeyInput,Operation> = Map.empty

    member private x.BuildMotionSequence() = 
        let wrap func = 
            fun _ ->
                func() 
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
                        VisualModeResult.SwitchMode ModeKind.Normal))
                }
        s

    member private x.EnsureOperationsMap () = 
        if _operationsMap.Count = 0 then
            let map = 
                x.BuildMotionSequence() 
                |> Seq.append (x.BuildOperationsSequence())
                |> Map.ofSeq
                |> Map.add (InputUtil.KeyToKeyInput(Key.Escape)) (fun _ -> SwitchMode ModeKind.Normal)
            _operationsMap <- map

    interface IMode with
        member x.Commands = 
            x.EnsureOperationsMap()
            _operationsMap |> Seq.map (fun pair -> pair.Key)
        member x.ModeKind = _kind
        member x.CanProcess (ki:KeyInput) = _operationsMap.ContainsKey(ki)
        member x.Process (ki : KeyInput) =  
            let op = Map.find ki _operationsMap
            let res = op(_bufferData.RegisterMap.DefaultRegister)
            match res with
            | VisualModeResult.Complete -> ProcessResult.Processed
            | VisualModeResult.SwitchMode m -> ProcessResult.SwitchMode m

        member x.OnEnter () = 
            x.EnsureOperationsMap()
            _selectionTracker.Start(None)
        member x.OnLeave () = 
            _selectionTracker.Stop()


