#light

namespace Vim.Modes.Command
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open System.Text.RegularExpressions

type internal CommandMode
    ( 
        _data : IVimBuffer, 
        _processor : ICommandProcessor) =

    // Command to show when entering command from Visual Mode
    static let FromVisualModeString = "'<,'>"

    let mutable _command = System.String.Empty

    /// Reverse list of the inputted commands
    let mutable _input : list<KeyInput> = []

    /// Currently queued up command string
    member x.Command = _command

    interface ICommandMode with 
        member x.VimBuffer = _data 
        member x.Command = _command
        member x.CommandNames = Seq.empty
        member x.ModeKind = ModeKind.Command
        member x.CanProcess ki = true
        member x.Process ki = 

            // Command mode can be validly entered with the selection active.  Consider
            // hitting ':' in Visual Mode.  The selection should be cleared when leaving
            let maybeClearSelection moveCaretToStart = 
                let selection = _data.TextView.Selection
                if not selection.IsEmpty && not _data.TextView.IsClosed then 
                    if moveCaretToStart then
                        let point = selection.StreamSelectionSpan.SnapshotSpan.Start
                        selection.Clear()
                        TextViewUtil.MoveCaretToPoint _data.TextView point
                    else 
                        selection.Clear()

            if ki = KeyInputUtil.EnterKey then
                let command = _input |> List.rev |> Seq.map (fun ki -> ki.Char) |> List.ofSeq
                let result = _processor.RunCommand command 
                _input <- List.empty
                _command <- System.String.Empty
                maybeClearSelection false
                match result with
                | RunResult.Completed -> SwitchMode ModeKind.Normal
                | RunResult.SubstituteConfirm (span, range, data) -> SwitchModeWithArgument (ModeKind.SubstituteConfirm, ModeArgument.Subsitute (span, range, data))
            elif ki = KeyInputUtil.EscapeKey then
                _input <- List.empty
                _command <- System.String.Empty
                maybeClearSelection true
                SwitchMode ModeKind.Normal
            elif ki.Key = VimKey.Back then
                if not (List.isEmpty _input) then 
                    _input <- List.tail _input
                    _command <- _command.Substring(0, (_command.Length - 1))
                    Processed
                else
                    maybeClearSelection true
                    SwitchMode ModeKind.Normal
            else 
                let c = ki.Char
                _command <-_command + (c.ToString())
                _input <- ki :: _input
                Processed

        member x.OnEnter arg =
            _command <- 
                match arg with
                | ModeArgument.None -> StringUtil.empty
                | ModeArgument.OneTimeCommand(_) -> StringUtil.empty
                | ModeArgument.FromVisual -> FromVisualModeString
                | ModeArgument.Subsitute(_) -> StringUtil.empty
                | ModeArgument.InsertWithCount _ -> StringUtil.empty
                | ModeArgument.InsertWithCountAndNewLine _ -> StringUtil.empty
            _input <- _command |> Seq.map KeyInputUtil.CharToKeyInput |> List.ofSeq |> List.rev
        member x.OnLeave () = ()
        member x.OnClose() = ()

        member x.RunCommand command = 
            _processor.RunCommand (command |> List.ofSeq)


