#light

namespace Vim.Modes.Command
open Vim
open Vim.Interpreter
open Microsoft.VisualStudio.Text
open System.Text.RegularExpressions

type internal CommandMode
    ( 
        _buffer : IVimBuffer, 
        _operations : ICommonOperations
    ) =

    let _commandChangedEvent = StandardEvent()
    let _vimData = _buffer.VimData
    let _statusUtil = _buffer.VimBufferData.StatusUtil
    let _parser = Parser(_buffer.Vim.GlobalSettings, _vimData)

    // Command to show when entering command from Visual Mode
    static let FromVisualModeString = "'<,'>"

    let mutable _command = StringUtil.empty

    let mutable _bindData : BindData<RunResult> option = None

    /// Currently queued up command string
    member x.Command 
        with get() = _command
        and set value = 
            if value <> _command then
                _command <- value
                _commandChangedEvent.Trigger x

    member x.ParseAndRunInput (command : string) = 

        let command = 
            if command.Length > 0 && command.[0] = ':' then
                command.Substring(1)
            else
                command

        let lineCommand = _parser.ParseLineCommand command 
        let vimInterpreter = _buffer.Vim.GetVimInterpreter _buffer
        vimInterpreter.RunLineCommand lineCommand

    // Command mode can be validly entered with the selection active.  Consider
    // hitting ':' in Visual Mode.  The selection should be cleared when leaving
    member x.MaybeClearSelection moveCaretToStart = 
        let selection = _buffer.TextView.Selection
        if not selection.IsEmpty && not _buffer.TextView.IsClosed then 
            if moveCaretToStart then
                let point = selection.StreamSelectionSpan.SnapshotSpan.Start
                selection.Clear()
                TextViewUtil.MoveCaretToPoint _buffer.TextView point
            else 
                selection.Clear()

    member x.Process (keyInput : KeyInput) =

        let bindData = 
            match _bindData with
            | None -> 

                // The ProcessCommand call back just means a new command state was reached.  Until it's
                // completed we just keep updating the current state 
                let processCommand command = 
                    x.Command <- command
                    0

                /// Run the specified command
                let completed command =
                    x.Command <- StringUtil.empty
                    let result = x.ParseAndRunInput command
                    x.MaybeClearSelection false
                    result

                /// User cancelled input.  Reset the selection
                let cancelled () = 
                    x.Command <- StringUtil.empty
                    x.MaybeClearSelection true

                // First key stroke.  Create a history client and get going
                let historyClient = {
                    new IHistoryClient<int, RunResult> with
                        member this.HistoryList = _vimData.CommandHistory
                        member this.RemapMode = KeyRemapMode.Command
                        member this.Beep() = _operations.Beep()
                        member this.ProcessCommand _ command = processCommand command
                        member this.Completed _ command = completed command
                        member this.Cancelled _ = cancelled ()
                    }
                let historySession = HistoryUtil.CreateHistorySession historyClient 0 _command
                historySession.CreateBindDataStorage().CreateBindData()
            | Some bindData ->
                bindData
        _bindData <- None

        // Actually run the KeyInput value
        match bindData.BindFunction keyInput with
        | BindResult.Complete result ->
            match result with 
            | RunResult.Completed -> 
                ProcessResult.Handled ModeSwitch.SwitchPreviousMode
            | RunResult.SubstituteConfirm (span, range, data) -> 
                let switch = ModeSwitch.SwitchModeWithArgument (ModeKind.SubstituteConfirm, ModeArgument.Substitute (span, range, data))
                ProcessResult.Handled switch
        | BindResult.Cancelled ->
            ProcessResult.OfModeKind ModeKind.Normal
        | BindResult.Error ->
            ProcessResult.OfModeKind ModeKind.Normal
        | BindResult.NeedMoreInput bindData ->
            _bindData <- Some bindData
            ProcessResult.HandledNeedMoreInput

    interface ICommandMode with
        member x.VimTextBuffer = _buffer.VimTextBuffer
        member x.Command 
            with get() = x.Command
            and set value = 
                if value <> x.Command then
                    x.Command <- value

                    // When the command is reset from an external API we need to reset our binding 
                    // behavior.  This completely changes our history state 
                    _bindData <- None
        member x.CommandNames = HistoryUtil.CommandNames |> Seq.map KeyInputSet.OneKeyInput
        member x.ModeKind = ModeKind.Command
        member x.CanProcess keyInput = not keyInput.IsMouseKey
        member x.Process keyInput = x.Process keyInput
        member x.OnEnter arg =
            x.Command <- 
                match arg with
                | ModeArgument.None -> StringUtil.empty
                | ModeArgument.FromVisual -> FromVisualModeString
                | ModeArgument.Substitute _ -> StringUtil.empty
                | ModeArgument.InitialVisualSelection _ -> StringUtil.empty
                | ModeArgument.InsertBlock (_, transaction) -> transaction.Complete(); StringUtil.empty
                | ModeArgument.InsertWithCount _ -> StringUtil.empty
                | ModeArgument.InsertWithCountAndNewLine _ -> StringUtil.empty
                | ModeArgument.InsertWithTransaction transaction -> transaction.Complete(); StringUtil.empty
        member x.OnLeave () = 
            x.MaybeClearSelection true
            x.Command <- StringUtil.empty
        member x.OnClose() = ()

        member x.RunCommand command = 
            x.ParseAndRunInput command

        [<CLIEvent>]
        member x.CommandChanged = _commandChangedEvent.Publish


