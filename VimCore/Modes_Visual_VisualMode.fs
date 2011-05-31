#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Vim
open Vim.Modes

type internal VisualMode
    (
        _buffer : IVimBuffer,
        _operations : ICommonOperations,
        _kind : ModeKind,
        _runner : ICommandRunner,
        _capture : IMotionCapture,
        _selectionTracker : ISelectionTracker
    ) = 

    let _textView = _buffer.TextView
    let _textBuffer = _buffer.TextBuffer
    let _registerMap = _buffer.RegisterMap
    let _eventHandlers = DisposableBag()
    let _operationKind, _visualKind = 
        match _kind with
        | ModeKind.VisualBlock -> (OperationKind.CharacterWise, VisualKind.Block)
        | ModeKind.VisualCharacter -> (OperationKind.CharacterWise, VisualKind.Character)
        | ModeKind.VisualLine -> (OperationKind.LineWise, VisualKind.Line)
        | _ -> failwith "Invalid"

    let mutable _builtCommands = false

    member x.SelectedSpan = (TextSelectionUtil.GetStreamSelectionSpan _buffer.TextView.Selection).SnapshotSpan

    /// Create the CommandBinding instances for the supported command values
    member x.CreateCommandBindings() =
        let visualSeq = 
            seq {
                yield ("c", CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextTextChange, VisualCommand.ChangeSelection)
                yield ("C", CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextTextChange, VisualCommand.ChangeLineSelection true)
                yield ("d", CommandFlags.Repeatable, VisualCommand.DeleteSelection)
                yield ("D", CommandFlags.Repeatable, VisualCommand.DeleteLineSelection)
                yield ("gJ", CommandFlags.Repeatable, VisualCommand.JoinSelection JoinKind.KeepEmptySpaces)
                yield ("gp", CommandFlags.Repeatable, VisualCommand.PutOverSelection true)
                yield ("gP", CommandFlags.Repeatable, VisualCommand.PutOverSelection true)
                yield ("g?", CommandFlags.Repeatable, VisualCommand.ChangeCase ChangeCharacterKind.Rot13)
                yield ("J", CommandFlags.Repeatable, VisualCommand.JoinSelection JoinKind.RemoveEmptySpaces)
                yield ("p", CommandFlags.Repeatable, VisualCommand.PutOverSelection false)
                yield ("P", CommandFlags.Repeatable, VisualCommand.PutOverSelection false)
                yield ("R", CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextTextChange, VisualCommand.ChangeLineSelection false)
                yield ("s", CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextTextChange, VisualCommand.ChangeSelection)
                yield ("S", CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextTextChange, VisualCommand.ChangeLineSelection false)
                yield ("u", CommandFlags.Repeatable, VisualCommand.ChangeCase ChangeCharacterKind.ToLowerCase)
                yield ("U", CommandFlags.Repeatable, VisualCommand.ChangeCase ChangeCharacterKind.ToUpperCase)
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
                yield ("zE", CommandFlags.Special, NormalCommand.DeleteAllFoldsInBuffer)
                yield ("[p", CommandFlags.Repeatable, NormalCommand.PutBeforeCaretWithIndent)
                yield ("[P", CommandFlags.Repeatable, NormalCommand.PutBeforeCaretWithIndent)
                yield ("]p", CommandFlags.Repeatable, NormalCommand.PutAfterCaretWithIndent)
                yield ("]P", CommandFlags.Repeatable, NormalCommand.PutBeforeCaretWithIndent)
                yield (":", CommandFlags.Special, NormalCommand.SwitchMode (ModeKind.Command, ModeArgument.FromVisual))
            } |> Seq.map (fun (str, flags, command) -> 
                let keyInputSet = KeyNotationUtil.StringToKeyInputSet str
                CommandBinding.NormalBinding (keyInputSet, flags, command))

        Seq.append visualSeq complexSeq |> Seq.append normalSeq

    member x.EnsureCommandsBuilt() =
        if not _builtCommands then
            let factory = Vim.Modes.CommandFactory(_operations, _capture, _buffer.MotionUtil, _buffer.JumpList, _buffer.Settings)

            // Add in the standard commands
            factory.CreateMovementCommands()
            |> Seq.append (factory.CreateScrollCommands())
            |> Seq.append (x.CreateCommandBindings())
            |> Seq.iter _runner.Add 

            // Add in macro editing
            factory.CreateMacroEditCommands _runner _buffer.Vim.MacroRecorder _eventHandlers

            _builtCommands <- true

    member x.ShouldHandleEscape = not _runner.IsHandlingEscape

    interface IMode with
        member x.VimBuffer = _buffer
        member x.CommandNames = 
            x.EnsureCommandsBuilt()
            _runner.Commands |> Seq.map (fun command -> command.KeyInputSet)
        member x.ModeKind = _kind
        member x.CanProcess (ki:KeyInput) = true
        member x.Process (ki : KeyInput) =  

            let result = 
                if ki = KeyInputUtil.EscapeKey && x.ShouldHandleEscape then
                    ProcessResult.Handled ModeSwitch.SwitchPreviousMode
                else
                    let original = _buffer.TextSnapshot.Version.VersionNumber
                    match _runner.Run ki with
                    | BindResult.NeedMoreInput _ -> 
                        // Commands like incremental search can move the caret and be incomplete.  Need to 
                        // update the selection while waiting for the next key
                        _selectionTracker.UpdateSelection()
                        ProcessResult.Handled ModeSwitch.NoSwitch
                    | BindResult.Complete commandRanData ->

                        if Util.IsFlagSet commandRanData.CommandBinding.CommandFlags CommandFlags.ResetCaret then
                            _selectionTracker.ResetCaret()

                        match commandRanData.CommandResult with
                        | CommandResult.Error ->
                            _selectionTracker.UpdateSelection()
                        | CommandResult.Completed modeSwitch ->
                            match modeSwitch with
                            | ModeSwitch.NoSwitch -> _selectionTracker.UpdateSelection()
                            | ModeSwitch.SwitchMode(_) -> ()
                            | ModeSwitch.SwitchModeWithArgument(_,_) -> ()
                            | ModeSwitch.SwitchPreviousMode -> ()

                        ProcessResult.OfCommandResult commandRanData.CommandResult
                    | BindResult.Error ->
                        _operations.Beep()
                        ProcessResult.Handled ModeSwitch.NoSwitch
                    | BindResult.Cancelled -> 
                        ProcessResult.Handled ModeSwitch.NoSwitch

            // If we are switching out Visual Mode then reset the selection
            if result.IsAnySwitch then
                // Is this a switch to command mode? 
                let toCommandMode = 
                    match result with
                    | ProcessResult.NotHandled -> 
                        false
                    | ProcessResult.Error ->
                        false
                    | ProcessResult.Handled switch ->
                        match switch with 
                        | ModeSwitch.NoSwitch -> false
                        | ModeSwitch.SwitchMode kind -> kind = ModeKind.Command
                        | ModeSwitch.SwitchModeWithArgument (kind, _) -> kind = ModeKind.Command
                        | ModeSwitch.SwitchPreviousMode -> false

                // On teardown we will get calls to Stop when the view is closed.  It's invalid to access 
                // the selection at that point
                let textView = _buffer.TextView
                if not textView.IsClosed && not toCommandMode then
                    textView.Selection.Clear()
                    textView.Selection.Mode <- TextSelectionMode.Stream

            result
        member x.OnEnter _ = 
            x.EnsureCommandsBuilt()
            _selectionTracker.Start()
        member x.OnLeave () = 
            _runner.ResetState()
            _selectionTracker.Stop()
        member x.OnClose() = 
            _eventHandlers.DisposeAll()

    interface IVisualMode with
        member x.CommandRunner = _runner
        member x.SyncSelection () = 
            if _selectionTracker.IsRunning then
                _selectionTracker.Stop()
                _selectionTracker.Start()



