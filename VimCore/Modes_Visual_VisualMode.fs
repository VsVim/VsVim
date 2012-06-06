#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Vim
open Vim.Modes

type internal VisualMode
    (
        _vimBufferData : IVimBufferData,
        _operations : ICommonOperations,
        _motionUtil : IMotionUtil,
        _modeKind : ModeKind,
        _runner : ICommandRunner,
        _capture : IMotionCapture,
        _selectionTracker : ISelectionTracker
    ) = 

    let _vimTextBuffer = _vimBufferData.VimTextBuffer
    let _textView = _vimBufferData.TextView
    let _textBuffer = _vimTextBuffer.TextBuffer
    let _globalSettings = _vimTextBuffer.GlobalSettings
    let _eventHandlers = DisposableBag()
    let _operationKind, _visualKind = 
        match _modeKind with
        | ModeKind.VisualBlock -> (OperationKind.CharacterWise, VisualKind.Block)
        | ModeKind.VisualCharacter -> (OperationKind.CharacterWise, VisualKind.Character)
        | ModeKind.VisualLine -> (OperationKind.LineWise, VisualKind.Line)
        | _ -> failwith "Invalid"

    /// Get a mark and us the provided 'func' to create a Motion value
    static let BindMark func = 
        let bindFunc (keyInput : KeyInput) =
            match Mark.OfChar keyInput.Char with
            | None -> BindResult<NormalCommand>.Error
            | Some localMark -> BindResult<_>.Complete (func localMark)
        let bindData = {
            KeyRemapMode = None
            BindFunction = bindFunc }
        BindDataStorage<_>.Simple bindData

    static let SharedCommands = 
        let visualSeq = 
            seq {
                yield ("c", CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextCommand, VisualCommand.ChangeSelection)
                yield ("C", CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextCommand, VisualCommand.ChangeLineSelection true)
                yield ("d", CommandFlags.Repeatable, VisualCommand.DeleteSelection)
                yield ("D", CommandFlags.Repeatable, VisualCommand.DeleteLineSelection)
                yield ("gJ", CommandFlags.Repeatable, VisualCommand.JoinSelection JoinKind.KeepEmptySpaces)
                yield ("gp", CommandFlags.Repeatable, VisualCommand.PutOverSelection true)
                yield ("gP", CommandFlags.Repeatable, VisualCommand.PutOverSelection true)
                yield ("g?", CommandFlags.Repeatable, VisualCommand.ChangeCase ChangeCharacterKind.Rot13)
                yield ("J", CommandFlags.Repeatable, VisualCommand.JoinSelection JoinKind.RemoveEmptySpaces)
                yield ("p", CommandFlags.Repeatable, VisualCommand.PutOverSelection false)
                yield ("P", CommandFlags.Repeatable, VisualCommand.PutOverSelection false)
                yield ("R", CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextCommand, VisualCommand.ChangeLineSelection false)
                yield ("s", CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextCommand, VisualCommand.ChangeSelection)
                yield ("S", CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextCommand, VisualCommand.ChangeLineSelection false)
                yield ("u", CommandFlags.Repeatable, VisualCommand.ChangeCase ChangeCharacterKind.ToLowerCase)
                yield ("U", CommandFlags.Repeatable, VisualCommand.ChangeCase ChangeCharacterKind.ToUpperCase)
                yield ("v", CommandFlags.Special, VisualCommand.SwitchModeVisual VisualKind.Character)
                yield ("V", CommandFlags.Special, VisualCommand.SwitchModeVisual VisualKind.Line)
                yield ("x", CommandFlags.Repeatable, VisualCommand.DeleteSelection)
                yield ("X", CommandFlags.Repeatable, VisualCommand.DeleteLineSelection)
                yield ("y", CommandFlags.ResetCaret, VisualCommand.YankSelection)
                yield ("Y", CommandFlags.ResetCaret, VisualCommand.YankLineSelection)
                yield ("zf", CommandFlags.None, VisualCommand.FoldSelection)
                yield ("zF", CommandFlags.None, VisualCommand.FoldSelection)
                yield ("zo", CommandFlags.Special, VisualCommand.OpenFoldInSelection)
                yield ("zO", CommandFlags.Special, VisualCommand.OpenAllFoldsInSelection)
                yield ("zc", CommandFlags.Special, VisualCommand.CloseFoldInSelection)
                yield ("zC", CommandFlags.Special, VisualCommand.CloseAllFoldsInSelection)
                yield ("zd", CommandFlags.Special, VisualCommand.DeleteFoldInSelection)
                yield ("zD", CommandFlags.Special, VisualCommand.DeleteAllFoldsInSelection)
                yield ("<C-q>", CommandFlags.Special, VisualCommand.SwitchModeVisual VisualKind.Block)
                yield ("<C-v>", CommandFlags.Special, VisualCommand.SwitchModeVisual VisualKind.Block)
                yield ("<S-i>", CommandFlags.Special, VisualCommand.SwitchModeInsert)
                yield ("<Del>", CommandFlags.Repeatable, VisualCommand.DeleteSelection)
                yield ("<lt>", CommandFlags.Repeatable, VisualCommand.ShiftLinesLeft)
                yield (">", CommandFlags.Repeatable, VisualCommand.ShiftLinesRight)
                yield ("~", CommandFlags.Repeatable, VisualCommand.ChangeCase ChangeCharacterKind.ToggleCase)
                yield ("=", CommandFlags.Repeatable, VisualCommand.FormatLines)
            } |> Seq.map (fun (str, flags, command) -> 
                let keyInputSet = KeyNotationUtil.StringToKeyInputSet str
                CommandBinding.VisualBinding (keyInputSet, flags, command))

        let complexSeq = 
            seq {
                yield ("r", CommandFlags.Repeatable, BindData<_>.CreateForSingle None (fun keyInput -> VisualCommand.ReplaceSelection keyInput))
            } |> Seq.map (fun (str, flags, bindCommand) -> 
                let keyInputSet = KeyNotationUtil.StringToKeyInputSet str
                let storage = BindDataStorage.Simple bindCommand
                CommandBinding.ComplexVisualBinding (keyInputSet, flags, storage))

        let normalSeq = 
            seq {
                yield ("gv", CommandFlags.Special, NormalCommand.SwitchPreviousVisualMode)
                yield ("zE", CommandFlags.Special, NormalCommand.DeleteAllFoldsInBuffer)
                yield ("zM", CommandFlags.Special, NormalCommand.CloseAllFolds)
                yield ("zR", CommandFlags.Special, NormalCommand.OpenAllFolds)
                yield ("[p", CommandFlags.Repeatable, NormalCommand.PutBeforeCaretWithIndent)
                yield ("[P", CommandFlags.Repeatable, NormalCommand.PutBeforeCaretWithIndent)
                yield ("]p", CommandFlags.Repeatable, NormalCommand.PutAfterCaretWithIndent)
                yield ("]P", CommandFlags.Repeatable, NormalCommand.PutBeforeCaretWithIndent)
                yield (":", CommandFlags.Special, NormalCommand.SwitchMode (ModeKind.Command, ModeArgument.FromVisual))
            } |> Seq.map (fun (str, flags, command) -> 
                let keyInputSet = KeyNotationUtil.StringToKeyInputSet str
                CommandBinding.NormalBinding (keyInputSet, flags, command))

        let normalComplexSeq = 
            seq {
                yield ("'", CommandFlags.Movement, BindMark NormalCommand.JumpToMarkLine)
                yield ("`", CommandFlags.Movement, BindMark NormalCommand.JumpToMark)
            } |> Seq.map (fun (str, flags, storage) -> 
                let keyInputSet = KeyNotationUtil.StringToKeyInputSet str
                CommandBinding.ComplexNormalBinding (keyInputSet, flags, storage))

        Seq.append visualSeq complexSeq 
        |> Seq.append normalSeq
        |> Seq.append normalComplexSeq
        |> List.ofSeq

    let mutable _builtCommands = false

    member x.CurrentSnapshot = _textBuffer.CurrentSnapshot

    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    member x.CommandNames = 
        x.EnsureCommandsBuilt()
        _runner.Commands |> Seq.map (fun command -> command.KeyInputSet)

    member this.KeyRemapMode = 
        if _runner.IsWaitingForMoreInput then
            _runner.KeyRemapMode
        else
            Some KeyRemapMode.Visual

    member x.SelectedSpan = (TextSelectionUtil.GetStreamSelectionSpan _textView.Selection).SnapshotSpan

    member x.EnsureCommandsBuilt() =
        if not _builtCommands then
            let factory = Vim.Modes.CommandFactory(_operations, _capture, _motionUtil, _vimBufferData.JumpList, _vimTextBuffer.LocalSettings)

            // Add in the standard commands
            factory.CreateMovementCommands()
            |> Seq.append (factory.CreateMovementTextObjectCommands())
            |> Seq.append (factory.CreateScrollCommands())
            |> Seq.append SharedCommands
            |> Seq.iter _runner.Add 

            // Add in macro editing
            factory.CreateMacroEditCommands _runner _vimTextBuffer.Vim.MacroRecorder _eventHandlers

            _builtCommands <- true

    member x.OnEnter modeArgument = 
        x.EnsureCommandsBuilt()

        // If we are provided an InitialVisualSpan value here go ahead and use it.  Do this before we
        // begin selection tracking as it will properly update the resulting selection to the appropriate
        // mode
        let caretPoint = 
            match modeArgument with
            | ModeArgument.InitialVisualSelection (visualSelection, caretPoint) ->

                if visualSelection.ModeKind = _modeKind then
                    visualSelection.Select _textView
                    let visualCaretPoint = visualSelection.GetCaretPoint _globalSettings.SelectionKind
                    TextViewUtil.MoveCaretToPointRaw _textView visualCaretPoint MoveCaretFlags.EnsureOnScreen
                    caretPoint
                else
                    None
            | _ ->
                None

        // Save the start point of the visual selection so we can potentially reset to it later
        let caretPosition = 
            match caretPoint with
            | Some caretPoint -> caretPoint.Position
            | None -> 
                // If there is an existing explicit selection then the anchor point is considered
                // the original start point.  Else just use the caret point
                if _textView.Selection.IsEmpty then
                    x.CaretPoint.Position
                else 
                    _textView.Selection.AnchorPoint.Position.Position
        _vimBufferData.VisualCaretStartPoint <- x.CurrentSnapshot.CreateTrackingPoint(caretPosition, PointTrackingMode.Negative) |> Some

        _selectionTracker.Start()

    member x.OnClose() =
        _eventHandlers.DisposeAll()

    /// Called when the Visual Mode is left.  Need to update the LastVisualSpan based on our selection
    member x.OnLeave() =
        _runner.ResetState()
        _selectionTracker.Stop()

    member x.Process (ki : KeyInput) =  

        // Save the VisualSelection before executing the command.  Many commands which exit
        // visual mode such as 'y' change the selection during execution.  We want to restore
        // to the selection before the command executed so save it now
        let lastVisualSelection = VisualSelection.CreateForSelection _textView _visualKind _globalSettings.SelectionKind

        let result = 
            if ki = KeyInputUtil.EscapeKey && x.ShouldHandleEscape then
                ProcessResult.Handled ModeSwitch.SwitchPreviousMode
            else
                let original = _textBuffer.CurrentSnapshot.Version.VersionNumber
                match _runner.Run ki with
                | BindResult.NeedMoreInput _ -> 
                    // Commands like incremental search can move the caret and be incomplete.  Need to 
                    // update the selection while waiting for the next key
                    _selectionTracker.UpdateSelection()
                    ProcessResult.HandledNeedMoreInput
                | BindResult.Complete commandRanData ->

                    if Util.IsFlagSet commandRanData.CommandBinding.CommandFlags CommandFlags.ResetCaret then
                        x.ResetCaret()

                    match commandRanData.CommandResult with
                    | CommandResult.Error ->
                        _selectionTracker.UpdateSelection()
                    | CommandResult.Completed modeSwitch ->
                        match modeSwitch with
                        | ModeSwitch.NoSwitch -> _selectionTracker.UpdateSelection()
                        | ModeSwitch.SwitchMode(_) -> ()
                        | ModeSwitch.SwitchModeWithArgument(_,_) -> ()
                        | ModeSwitch.SwitchPreviousMode -> ()
                        | ModeSwitch.SwitchModeOneTimeCommand _ -> ()

                    ProcessResult.OfCommandResult commandRanData.CommandResult
                | BindResult.Error ->
                    _selectionTracker.UpdateSelection()
                    _operations.Beep()
                    ProcessResult.Handled ModeSwitch.NoSwitch
                | BindResult.Cancelled -> 
                    _selectionTracker.UpdateSelection()
                    ProcessResult.Handled ModeSwitch.NoSwitch

        // If we are switching out Visual Mode then reset the selection.  Only do this if 
        // we are the active IMode.  It's very possible that we were switched out already 
        // as part of a complex command
        if result.IsAnySwitch && _selectionTracker.IsRunning then
            // Is this a switch to command mode? 
            let toCommandMode = 
                match result with
                | ProcessResult.NotHandled -> 
                    false
                | ProcessResult.Error ->
                    false
                | ProcessResult.HandledNeedMoreInput ->
                    false
                | ProcessResult.Handled switch ->
                    match switch with 
                    | ModeSwitch.NoSwitch -> false
                    | ModeSwitch.SwitchMode modeKind -> modeKind = ModeKind.Command
                    | ModeSwitch.SwitchModeWithArgument (modeKind, _) -> modeKind = ModeKind.Command
                    | ModeSwitch.SwitchPreviousMode -> false
                    | ModeSwitch.SwitchModeOneTimeCommand -> false

            // On teardown we will get calls to Stop when the view is closed.  It's invalid to access 
            // the selection at that point
            if not _textView.IsClosed then

                // Before resetting the selection save it
                _vimTextBuffer.LastVisualSelection <- Some lastVisualSelection

                if not toCommandMode then
                    _textView.Selection.Clear()
                    _textView.Selection.Mode <- TextSelectionMode.Stream

        result

    /// Certain operations cause the caret to be reset to it's position before visual mode 
    /// started once visual mode is complete (y for example).  This function handles that
    member x.ResetCaret() =

        // Calculate the start point of the selection
        let startPoint = 

            match _vimBufferData.VisualCaretStartPoint with
            | None -> None
            | Some trackingPoint -> TrackingPointUtil.GetPoint x.CurrentSnapshot trackingPoint

        match startPoint with
        | None ->
            // If we couldn't calculate a start point then there is just nothing to do.  This
            // really shouldn't happen though
            ()
        | Some startPoint ->

            // Calculate the actual point to use.  If the selection turned backwards then we
            // prefer the current caret over the original
            let point =
                if startPoint.Position < x.CaretPoint.Position then 
                    startPoint
                else 
                    x.CaretPoint
            TextViewUtil.MoveCaretToPoint _textView point
            TextViewUtil.EnsureCaretOnScreen _textView

    member x.ShouldHandleEscape = not _runner.IsHandlingEscape

    member x.SyncSelection() =
        if _selectionTracker.IsRunning then
            _selectionTracker.Stop()
            _selectionTracker.Start()

    interface IMode with
        member x.VimTextBuffer = _vimTextBuffer
        member x.CommandNames = x.CommandNames
        member x.ModeKind = _modeKind
        member x.CanProcess _ = true
        member x.Process keyInput =  x.Process keyInput
        member x.OnEnter modeArgument = x.OnEnter modeArgument
        member x.OnLeave () = x.OnLeave()
        member x.OnClose() = x.OnClose()

    interface IVisualMode with
        member x.CommandRunner = _runner
        member x.KeyRemapMode = x.KeyRemapMode
        member x.VisualSelection = VisualSelection.CreateForSelection _textView _visualKind _globalSettings.SelectionKind
        member x.SyncSelection () = x.SyncSelection()



