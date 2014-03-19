namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Vim
open Vim.Modes

type internal SelectMode
    (
        _vimBufferData : IVimBufferData,
        _commonOperations : ICommonOperations,
        _motionUtil : IMotionUtil,
        _visualKind : VisualKind,
        _runner : ICommandRunner,
        _capture : IMotionCapture,
        _undoRedoOperations : IUndoRedoOperations,
        _selectionTracker : ISelectionTracker
    ) = 

    let _globalSettings = _vimBufferData.LocalSettings.GlobalSettings
    let _vimTextBuffer = _vimBufferData.VimTextBuffer
    let _textBuffer = _vimBufferData.TextBuffer
    let _textView = _vimBufferData.TextView
    let _modeKind = 
        match _visualKind with
        | VisualKind.Character -> ModeKind.SelectCharacter
        | VisualKind.Line -> ModeKind.SelectLine
        | VisualKind.Block -> ModeKind.SelectBlock

    /// Handles Ctrl+C/Ctrl+V/Ctrl+X a la $VIMRUNTIME/mswin.vim
    static let Commands =
        let visualSeq =
            seq {
                yield ("<C-g>", CommandFlags.Special, VisualCommand.SwitchModeOtherVisual)
                yield ("<C-x>", CommandFlags.Special, VisualCommand.CutSelection)
                yield ("<C-c>", CommandFlags.Special, VisualCommand.CopySelection)
                yield ("<C-v>", CommandFlags.Special, VisualCommand.CutSelectionAndPaste)
                yield ("<C-a>", CommandFlags.Special, VisualCommand.SelectAll)
            } |> Seq.map (fun (str, flags, command) ->
                let keyInputSet = KeyNotationUtil.StringToKeyInputSet str
                CommandBinding.VisualBinding (keyInputSet, flags, command))
        visualSeq

    /// A 'special key' is defined in :help keymodel as any of the following keys.  Depending
    /// on the value of the keymodel setting they can affect the selection
    static let GetCaretMovement (keyInput : KeyInput) =
        if not (Util.IsFlagSet keyInput.KeyModifiers KeyModifiers.Control) then
            match keyInput.Key with
            | VimKey.Up -> Some CaretMovement.Up
            | VimKey.Right -> Some CaretMovement.Right
            | VimKey.Down -> Some CaretMovement.Down
            | VimKey.Left -> Some CaretMovement.Left
            | VimKey.Home -> Some CaretMovement.Home
            | VimKey.End -> Some CaretMovement.End
            | VimKey.PageUp -> Some CaretMovement.PageUp
            | VimKey.PageDown -> Some CaretMovement.PageDown
            | _ -> None
        else
            match keyInput.Key with
            | VimKey.Up -> Some CaretMovement.ControlUp
            | VimKey.Right -> Some CaretMovement.ControlRight
            | VimKey.Down -> Some CaretMovement.ControlDown
            | VimKey.Left -> Some CaretMovement.ControlLeft
            | VimKey.Home -> Some CaretMovement.ControlHome
            | VimKey.End -> Some CaretMovement.ControlEnd
            | _ -> None

    /// Whether a key input corresponds to normal text
    static let IsTextKeyInput (keyInput : KeyInput) =
        Option.isSome keyInput.RawChar && not (CharUtil.IsControl keyInput.Char)

    let mutable _builtCommands = false

    member x.CommandNames =
        x.EnsureCommandsBuilt()
        _runner.Commands |> Seq.map (fun command -> command.KeyInputSet)

    member this.KeyRemapMode =
        if _runner.IsWaitingForMoreInput then
            _runner.KeyRemapMode
        else
            KeyRemapMode.Visual

    member x.EnsureCommandsBuilt() =

        /// Whether a command is bound to a key that is not insertable text
        let isNonTextCommand (command : CommandBinding) =
            match command.KeyInputSet.FirstKeyInput with
            | None ->
                true
            | Some keyInput ->
                not (IsTextKeyInput keyInput)

        if not _builtCommands then
            let factory = CommandFactory(_commonOperations, _capture)

            // Add in the standard non-conflicting movement commands
            factory.CreateScrollCommands() |> Seq.filter isNonTextCommand
            |> Seq.append Commands
            |> Seq.iter _runner.Add

            _builtCommands <- true

    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    member x.CaretVirtualPoint = TextViewUtil.GetCaretVirtualPoint _textView

    member x.CaretLine = TextViewUtil.GetCaretLine _textView

    member x.CurrentSnapshot = _textView.TextSnapshot

    member x.ShouldStopSelection (keyInput : KeyInput) =
        let hasShift = Util.IsFlagSet keyInput.KeyModifiers KeyModifiers.Shift
        let hasStopSelection = Util.IsFlagSet _globalSettings.KeyModelOptions KeyModelOptions.StopSelection
        not hasShift && hasStopSelection

    member x.ProcessCaretMovement caretMovement keyInput =
        _textView.Selection.Clear()
        _commonOperations.MoveCaretWithArrow caretMovement |> ignore

        if x.ShouldStopSelection keyInput then
            ProcessResult.Handled ModeSwitch.SwitchPreviousMode
        else
            // The caret moved so we need to update the selection 
            _selectionTracker.UpdateSelection()
            ProcessResult.Handled ModeSwitch.NoSwitch

    /// The user hit an input key.  Need to replace the current selection with the given
    /// text and put the caret just after the insert.  This needs to be a single undo
    /// transaction
    member x.ProcessInput text linked =

        let replaceSelection (span : SnapshotSpan) text =
            _undoRedoOperations.EditWithUndoTransaction "Replace" _textView <| fun () ->

                use edit = _textBuffer.CreateEdit()

                // First step is to replace the deleted text with the new one
                edit.Delete(span.Span) |> ignore
                edit.Insert(span.End.Position, text) |> ignore

                // Now move the caret past the insert point in the new snapshot. We don't need to
                // add one here (or even the length of the insert text).  The insert occurred at
                // the exact point we are tracking and we chose PointTrackingMode.Positive so this
                // will push the point past the insert
                let snapshot = edit.Apply()
                match TrackingPointUtil.GetPointInSnapshot span.End PointTrackingMode.Positive snapshot with
                | None -> ()
                | Some point -> TextViewUtil.MoveCaretToPoint _textView point

        // During undo we don't want the currently selected text to be reselected as that
        // would put the editor back into select mode.  Clear the selection now so that
        // it's not recorderd in the undo transaction and move the caret to the selection
        // start
        let span = _textView.Selection.StreamSelectionSpan.SnapshotSpan
        _textView.Selection.Clear()
        TextViewUtil.MoveCaretToPoint _textView span.Start

        if linked then
            let transaction = _undoRedoOperations.CreateLinkedUndoTransaction "Replace"
            try
                replaceSelection span text
            with
                | _ ->
                    transaction.Dispose()
                    reraise()
            let arg = ModeArgument.InsertWithTransaction transaction
            ProcessResult.Handled (ModeSwitch.SwitchModeWithArgument (ModeKind.Insert, arg))
        else
            replaceSelection span text
            ProcessResult.Handled (ModeSwitch.SwitchMode ModeKind.Insert)

    /// Select Mode doesn't actually process any mouse keys.  Actual mouse events for
    /// selection are handled by the selection tracker 
    member x.CanProcess (keyInput : KeyInput) = not keyInput.IsMouseKey

    member x.Process keyInput = 

        let processResult = 
            if keyInput = KeyInputUtil.EscapeKey then
                ProcessResult.Handled ModeSwitch.SwitchPreviousMode
            elif keyInput = KeyInputUtil.EnterKey then
                let caretPoint = TextViewUtil.GetCaretPoint _textView
                let text = _commonOperations.GetNewLineText caretPoint
                x.ProcessInput text true
            elif keyInput.Key = VimKey.Delete || keyInput.Key = VimKey.Back then
                x.ProcessInput "" false |> ignore
                ProcessResult.Handled ModeSwitch.SwitchPreviousMode
            elif keyInput = KeyInputUtil.CharWithControlToKeyInput 'o' then
                x.ProcessVisualModeOneCommand keyInput
            else
                match GetCaretMovement keyInput with
                | Some caretMovement ->
                    x.ProcessCaretMovement caretMovement keyInput
                | None ->
                    if IsTextKeyInput keyInput then
                        x.ProcessInput (StringUtil.ofChar keyInput.Char) true
                    else
                        match _runner.Run keyInput with
                        | BindResult.NeedMoreInput _ ->
                            ProcessResult.HandledNeedMoreInput
                        | BindResult.Complete commandRanData ->
                            ProcessResult.OfCommandResult commandRanData.CommandResult
                        | BindResult.Error ->
                            _commonOperations.Beep()
                            ProcessResult.NotHandled
                        | BindResult.Cancelled ->
                            _selectionTracker.UpdateSelection()
                            ProcessResult.Handled ModeSwitch.NoSwitch

        /// Restore or clear the selection depending on the next mode
        if processResult.IsAnySwitchToVisual then
            _selectionTracker.UpdateSelection()
        elif processResult.IsAnySwitch then
            _textView.Selection.Clear()
            _textView.Selection.Mode <- TextSelectionMode.Stream

        processResult

    /// Enter visual mode for a single command.
    member x.ProcessVisualModeOneCommand keyInput =
        match VisualKind.OfModeKind _modeKind with
        | Some visualKind ->
            let modeKind = visualKind.VisualModeKind
            let modeSwitch = ModeSwitch.SwitchModeOneTimeCommand modeKind
            ProcessResult.Handled modeSwitch
        | None ->
            ProcessResult.Error

    member x.OnEnter modeArgument =
        x.EnsureCommandsBuilt()
        _selectionTracker.RecordCaretTrackingPoint modeArgument
        _selectionTracker.Start()

    member x.OnLeave() =
        _runner.ResetState()
        _selectionTracker.Stop()

    member x.OnClose() = ()

    member x.SyncSelection() =
        if _selectionTracker.IsRunning then
            _selectionTracker.Stop()
            _selectionTracker.Start()

    interface ISelectMode with
        member x.SyncSelection() = x.SyncSelection()

    interface IMode with
        member x.VimTextBuffer = _vimTextBuffer
        member x.CommandNames = Seq.empty
        member x.ModeKind = _modeKind
        member x.CanProcess keyInput = x.CanProcess keyInput
        member x.Process keyInput =  x.Process keyInput
        member x.OnEnter modeArgument = x.OnEnter modeArgument
        member x.OnLeave () = x.OnLeave()
        member x.OnClose() = x.OnClose()
