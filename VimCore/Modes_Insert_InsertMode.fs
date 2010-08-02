#light

namespace Vim.Modes.Insert
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor

type CommandFunction = unit -> ProcessResult

type internal InsertMode
    ( 
        _data : IVimBuffer, 
        _operations : Modes.ICommonOperations,
        _broker : IDisplayWindowBroker) as this =

    let mutable _commandMap : Map<KeyInput,CommandFunction> = Map.empty

    do
        let commands : (string * CommandFunction) list = 
            [
                ("<Esc>", this.ProcessEscape);
                ("CTRL-[", this.ProcessEscape);
                ("CTRL-d", this.ShiftLeft)
            ]

        _commandMap <-
            commands 
            |> Seq.ofList
            |> Seq.map (fun (str,func) -> (KeyNotationUtil.StringToKeyInput str),func)
            |> Map.ofSeq


    let _escapeCommands = [
        InputUtil.VimKeyToKeyInput VimKey.Escape;
        InputUtil.CharWithControlToKeyInput '[' ]
    let _commands = InputUtil.CharWithControlToKeyInput 'd' :: _escapeCommands

    /// Process the CTRL-D combination and do a shift left
    member private this.ShiftLeft() = 
        _operations.ShiftLinesLeft 1
        ProcessResult.Processed

    member private this.ProcessEscape () =

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
            match Map.tryFind ki _commandMap with
            | Some(func) -> func()
            | None -> Processed
        member x.OnEnter _ = ()
        member x.OnLeave () = ()
        member x.OnClose() = ()
