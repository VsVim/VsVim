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

type internal Operation = unit -> VisualModeResult

type internal VisualMode
    (
        _bufferData : IVimBufferData,
        _operations : ICommonOperations,
        _kind : ModeKind ) = 
    let mutable _anchor : VirtualSnapshotPoint = new VirtualSnapshotPoint()
    let mutable _operationsMap : Map<KeyInput,Operation> = Map.empty
    do 
        match _kind with 
        | ModeKind.VisualBlock -> ()
        | ModeKind.VisualCharacter -> ()
        | ModeKind.VisualLineWise -> ()
        | _ -> invalidArg "_kind" "Invalid kind for Visual Mode"

    /// Update the selection based on the current method
    member private x.UpdateSelection() =
        let cursor = _bufferData.TextView.Caret.Position.VirtualBufferPosition
        _bufferData.TextView.Selection.Select(_anchor, cursor)

    member private x.BuildMotionSequence() = 
        let wrap func = 
            fun () ->
                func() 
                x.UpdateSelection()
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
                yield (InputUtil.CharToKeyInput('h'), moveUp)
                yield (InputUtil.KeyToKeyInput(Key.Up), moveUp)
                yield (KeyInput('p', Key.P, ModifierKeys.Control), moveUp)
                yield (InputUtil.CharToKeyInput('j'), moveDown)
                yield (InputUtil.KeyToKeyInput(Key.Down), moveDown)
                yield (KeyInput('n', Key.N, ModifierKeys.Control),moveDown)
                yield (KeyInput('j', Key.J, ModifierKeys.Control),moveDown)
                }
        s

    member x.EnsureOperationsMap () = 
        if _operationsMap.Count = 0 then
            let map = 
                x.BuildMotionSequence() 
                |> Map.ofSeq
                |> Map.add (InputUtil.KeyToKeyInput(Key.Escape)) (fun () -> SwitchMode ModeKind.Normal)
            _operationsMap <- map

    interface IMode with
        member x.Commands = 
            x.EnsureOperationsMap()
            _operationsMap |> Seq.map (fun pair -> pair.Key)
        member x.ModeKind = _kind
        member x.CanProcess (ki:KeyInput) = _operationsMap.ContainsKey(ki)
        member x.Process (ki : KeyInput) =  
            let op = Map.find ki _operationsMap
            let res = op()
            match res with
            | VisualModeResult.Complete -> ProcessResult.ProcessNotHandled
            | VisualModeResult.SwitchMode m -> ProcessResult.SwitchMode m

        member x.OnEnter () = 
            x.EnsureOperationsMap()
            _anchor <- _bufferData.TextView.Caret.Position.VirtualBufferPosition
            x.UpdateSelection()
        member x.OnLeave () = 
            _anchor <- new VirtualSnapshotPoint()
            _bufferData.EditorOperations.ResetSelection()


