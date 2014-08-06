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

    static let BindDataError : BindData<int> = {
        KeyRemapMode = KeyRemapMode.None;
        BindFunction = fun _ -> BindResult.Error
    }

    let mutable _command = StringUtil.empty
    let mutable _historySession : IHistorySession<int, int> option = None
    let mutable _bindData = BindDataError

    /// Currently queued up command string
    member x.Command 
        with get() = _command
        and set value = 
            if value <> _command then
                _command <- value
                _commandChangedEvent.Trigger x

    member x.InPasteWait = 
        match _historySession with
        | Some historySession -> historySession.InPasteWait
        | None -> false

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

        match _bindData.BindFunction keyInput with
        | BindResult.Complete _ ->
            _bindData <- BindDataError

            // It is possible for the execution of the command to change the mode (say :s.../c) 
            if _buffer.ModeKind = ModeKind.Command then
                ProcessResult.Handled ModeSwitch.SwitchPreviousMode
            else 
                ProcessResult.Handled ModeSwitch.NoSwitch
        | BindResult.Cancelled ->
            _bindData <- BindDataError
            ProcessResult.OfModeKind ModeKind.Normal
        | BindResult.Error ->
            _bindData <- BindDataError
            ProcessResult.OfModeKind ModeKind.Normal
        | BindResult.NeedMoreInput bindData ->
            _bindData <- bindData
            ProcessResult.HandledNeedMoreInput

    member x.CreateHistorySession() =

        // The ProcessCommand call back just means a new command state was reached.  Until it's
        // completed we just keep updating the current state 
        let processCommand command = 
            x.Command <- command
            0

        /// Run the specified command
        let completed command =
            x.Command <- StringUtil.empty
            x.ParseAndRunInput command
            x.MaybeClearSelection false
            0

        /// User cancelled input.  Reset the selection
        let cancelled () = 
            x.Command <- StringUtil.empty
            x.MaybeClearSelection true

        // First key stroke.  Create a history client and get going
        let historyClient = {
            new IHistoryClient<int, int> with
                member this.HistoryList = _vimData.CommandHistory
                member this.RegisterMap = _buffer.RegisterMap
                member this.RemapMode = KeyRemapMode.Command
                member this.Beep() = _operations.Beep()
                member this.ProcessCommand _ command = processCommand command
                member this.Completed _ command = completed command
                member this.Cancelled _ = cancelled ()
            }
        HistoryUtil.CreateHistorySession historyClient 0 _command

    member x.OnEnter arg = 
        let historySession = x.CreateHistorySession()

        _command <- ""
        _historySession <- Some historySession
        _bindData <- historySession.CreateBindDataStorage().CreateBindData()

        let commandText = 
            match arg with
            | ModeArgument.None -> StringUtil.empty
            | ModeArgument.FromVisual -> FromVisualModeString
            | ModeArgument.Substitute _ -> StringUtil.empty
            | ModeArgument.InitialVisualSelection _ -> StringUtil.empty
            | ModeArgument.InsertBlock (_, transaction) -> transaction.Complete(); StringUtil.empty
            | ModeArgument.InsertWithCount _ -> StringUtil.empty
            | ModeArgument.InsertWithCountAndNewLine _ -> StringUtil.empty
            | ModeArgument.InsertWithTransaction transaction -> transaction.Complete(); StringUtil.empty

        if not (StringUtil.isNullOrEmpty commandText) then
            x.ChangeCommand commandText

    member x.OnLeave() = 
        x.MaybeClearSelection true
        _command <- StringUtil.empty
        _historySession <- None
        _bindData <- BindDataError

    /// Called externally to update the command.  Do this by modifying the history 
    /// session.  If we aren't in command mode currently then this is a no-op 
    member x.ChangeCommand command = 
        match _historySession with
        | None -> ()
        | Some historySession -> historySession.ResetCommand command

    interface ICommandMode with
        member x.VimTextBuffer = _buffer.VimTextBuffer
        member x.Command 
            with get() = x.Command
            and set value = x.ChangeCommand value
        member x.CommandNames = HistoryUtil.CommandNames |> Seq.map KeyInputSet.OneKeyInput
        member x.InPasteWait = x.InPasteWait
        member x.ModeKind = ModeKind.Command
        member x.CanProcess keyInput = not keyInput.IsMouseKey
        member x.Process keyInput = x.Process keyInput
        member x.OnEnter arg = x.OnEnter arg
        member x.OnLeave () = x.OnLeave ()
        member x.OnClose() = ()

        member x.RunCommand command = 
            x.ParseAndRunInput command

        [<CLIEvent>]
        member x.CommandChanged = _commandChangedEvent.Publish


