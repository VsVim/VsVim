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
        _broker : IDisplayWindowBroker) =

    let _escapeCommands = [
        InputUtil.VimKeyToKeyInput VimKey.EscapeKey;
        InputUtil.CharWithControlToKeyInput '[' ]
    let _commands = InputUtil.CharWithControlToKeyInput 'd' :: _escapeCommands

    /// Process the CTRL-D combination and do a shift left
    member private this.ShiftLeft() = _operations.ShiftLinesLeft 1

    member private this.ProcessEscape() =

        if _broker.IsCompletionActive || _broker.IsSignatureHelpActive || _broker.IsQuickInfoActive then
            _broker.DismissDisplayWindows()

            if _data.Settings.GlobalSettings.DoubleEscape then ProcessResult.Processed
            else 
                _operations.MoveCaretLeft 1 
                ProcessResult.SwitchMode ModeKind.Normal

        else
            _operations.MoveCaretLeft 1 
            ProcessResult.SwitchMode ModeKind.Normal

    interface IMode with 
        member x.VimBuffer = _data
        member x.CommandNames =  _commands |> Seq.map OneKeyInput
        member x.ModeKind = ModeKind.Insert
        member x.CanProcess (ki:KeyInput) = 
            match _commands |> List.tryFind (fun d -> d = ki) with
            | Some _ -> true
            | None -> false
        member x.Process (ki : KeyInput) = 
            if ListUtil.contains ki _escapeCommands then x.ProcessEscape()
            elif ki = InputUtil.CharWithControlToKeyInput 'd' then
                x.ShiftLeft()
                ProcessResult.Processed
            else Processed
        member x.OnEnter () = ()
        member x.OnLeave () = ()
        member x.OnClose() = ()
