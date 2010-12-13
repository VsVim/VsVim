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
        _broker : IDisplayWindowBroker, 
        _editorOptions : IEditorOptions,
        _isReplace : bool ) as this =

    let mutable _commandMap : Map<KeyInput,CommandFunction> = Map.empty

    do
        let commands : (string * CommandFunction) list = 
            [
                ("<Esc>", this.ProcessEscape);
                ("<C-[>", this.ProcessEscape);
                ("<C-d>", this.ProcessShiftLeft)
                ("<C-t>", this.ProcessShiftRight)
                ("<C-o>", this.ProcessNormalModeOneCommand)
            ]

        _commandMap <-
            commands 
            |> Seq.ofList
            |> Seq.map (fun (str,func) -> (KeyNotationUtil.StringToKeyInput str),func)
            |> Map.ofSeq


    /// Enter normal mode for a single command
    member private this.ProcessNormalModeOneCommand() =
        ProcessResult.SwitchModeWithArgument (ModeKind.Normal, ModeArgument.OneTimeCommand ModeKind.Insert)

    /// Process the CTRL-D combination and do a shift left
    member private this.ProcessShiftLeft() = 
        _operations.ShiftLinesLeft 1
        ProcessResult.Processed

    /// Process the CTRL-T combination and do a shift right
    member private this.ProcessShiftRight() = 
        _operations.ShiftLinesRight 1
        ProcessResult.Processed

    member private this.ProcessEscape () =

        if _broker.IsCompletionActive || _broker.IsSignatureHelpActive || _broker.IsQuickInfoActive then
            _broker.DismissDisplayWindows()
            _operations.MoveCaretLeft 1 
            ProcessResult.SwitchMode ModeKind.Normal

        else
            _operations.MoveCaretLeft 1 
            ProcessResult.SwitchMode ModeKind.Normal

    interface IMode with 
        member x.VimBuffer = _data
        member x.CommandNames =  _commandMap |> Seq.map (fun p -> p.Key) |> Seq.map OneKeyInput
        member x.ModeKind = if _isReplace then ModeKind.Replace else ModeKind.Insert
        member x.CanProcess ki = Map.containsKey ki _commandMap 
        member x.Process (ki : KeyInput) = 
            match Map.tryFind ki _commandMap with
            | Some(func) -> func()
            | None -> Processed
        member x.OnEnter _ = 
            // If this is replace mode then go ahead and setup overwrite
            if _isReplace then
                _editorOptions.SetOptionValue(DefaultTextViewOptions.OverwriteModeId, true)
        member x.OnLeave () = 
            // If this is replace mode then go ahead and undo overwrite
            if _isReplace then
                _editorOptions.SetOptionValue(DefaultTextViewOptions.OverwriteModeId, false)
        member x.OnClose() = ()
