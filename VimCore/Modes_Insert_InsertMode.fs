#light

namespace Vim.Modes.Insert
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor

type internal InsertMode
    ( 
        _data : IVimBuffer, 
        _operations : Modes.ICommonOperations,
        _broker : ICompletionWindowBroker ) =
    let _commands = [
        InputUtil.WellKnownKeyToKeyInput EscapeKey;
        KeyInput('d', KeyModifiers.Control); ]

    /// Process the CTRL-D combination and do a shift left
    member private this.ShiftLeft() =
        let caret = ViewUtil.GetCaretPoint _data.TextView
        let line = caret.GetContainingLine()
        _operations.ShiftLeft line.Extent (_data.Settings.GlobalSettings.ShiftWidth) |> ignore

    member private this.ProcessEscape() =
        if _broker.IsCompletionWindowActive then
            _broker.DismissCompletionWindow()
            ProcessResult.Processed
        else
            ProcessResult.SwitchMode ModeKind.Normal

    interface IMode with 
        member x.VimBuffer = _data
        member x.Commands = _commands |> Seq.ofList
        member x.ModeKind = ModeKind.Insert
        member x.CanProcess (ki:KeyInput) = 
            match _commands |> List.tryFind (fun d -> d = ki) with
            | Some _ -> true
            | None -> false
        member x.Process (ki : KeyInput) = 
            if ki = InputUtil.WellKnownKeyToKeyInput(EscapeKey) then x.ProcessEscape()
            elif ki = KeyInput('d', KeyModifiers.Control) then 
                x.ShiftLeft()
                ProcessResult.Processed
            else Processed
        member x.OnEnter () = ()
        member x.OnLeave () = 
            
            // When leaving insert mode the caret should move one to the left on the
            // same line
            let point = ViewUtil.GetCaretPoint _data.TextView
            let line = point.GetContainingLine()
            if line.Start <> point then
                let point = point.Subtract(1)
                ViewUtil.MoveCaretToPoint _data.TextView point |> ignore
