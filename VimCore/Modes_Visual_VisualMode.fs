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
        _selectionTracker : ISelectionTracker ) = 

    let _textView = _buffer.TextView
    let _textBuffer = _buffer.TextBuffer
    let _registerMap = _buffer.RegisterMap
    let _motionKind = MotionKind.Inclusive
    let _operationKind, _visualKind = 
        match _kind with
        | ModeKind.VisualBlock -> (OperationKind.CharacterWise, VisualKind.Block)
        | ModeKind.VisualCharacter -> (OperationKind.CharacterWise, VisualKind.Character)
        | ModeKind.VisualLine -> (OperationKind.LineWise, VisualKind.Line)
        | _ -> failwith "Invalid"

    let mutable _builtCommands = false

    member x.SelectedSpan = (TextSelectionUtil.GetStreamSelectionSpan _buffer.TextView.Selection).SnapshotSpan

    /// Build the set of CommandBinding which will be used to move the caret.  Typically
    /// these are just MotionBinding instances which are converted to movement commands
    member x.BuildMoveSequence() = 
        let factory = Vim.Modes.CommandFactory(_operations, _capture, _buffer.TextViewMotionUtil, _buffer.JumpList, _buffer.Settings)
        factory.CreateMovementCommands()

    member x.BuildOperationsSequence() =

        let runVisualCommand funcNormal funcBlock count reg visualSpan = 
            match visualSpan with
            | VisualSpan.Character span -> funcNormal count reg span
            | VisualSpan.Line range -> funcNormal count reg range.ExtentIncludingLineBreak
            | VisualSpan.Block col -> funcBlock count reg col

        /// Commands which do not need a span to operate on
        let simples =
            let resultSwitchPrevious = CommandResult.Completed ModeSwitch.SwitchPreviousMode
            seq {
                yield (
                    "<C-u>", 
                    CommandFlags.Movement,
                    ModeSwitch.NoSwitch |> Some,
                    fun count _ -> _operations.MoveCaretAndScrollLines ScrollDirection.Up count)
                yield (
                    "<C-d>", 
                    CommandFlags.Movement, 
                    ModeSwitch.NoSwitch |> Some,
                    fun count _ -> _operations.MoveCaretAndScrollLines ScrollDirection.Down count)
                yield (
                    "<PageUp>",
                    CommandFlags.Movement,
                    ModeSwitch.NoSwitch |> Some,
                    fun _ _ -> _operations.EditorOperations.PageUp(false))
                yield (
                    "<PageDown>",
                    CommandFlags.Movement,
                    ModeSwitch.NoSwitch |> Some,
                    fun _ _ -> _operations.EditorOperations.PageDown(false))
                yield (
                    "zo", 
                    CommandFlags.Special, 
                    None,
                    fun _ _ -> _operations.OpenFold x.SelectedSpan 1)
                yield (
                    "zO", 
                    CommandFlags.Special, 
                    None,
                    fun _ _ -> _operations.OpenAllFolds x.SelectedSpan )
                yield (
                    "zc", 
                    CommandFlags.Special, 
                    None,
                    fun _ _ -> _operations.CloseFold x.SelectedSpan 1)
                yield (
                    "zC", 
                    CommandFlags.Special, 
                    None,
                    fun _ _ -> _operations.CloseAllFolds x.SelectedSpan )
                yield (
                    "zf", 
                    CommandFlags.Special, 
                    None,
                    fun _ _ -> _operations.FoldManager.CreateFold x.SelectedSpan)
                yield (
                    "zd", 
                    CommandFlags.Special, 
                    None,
                    fun _ _ -> _operations.DeleteOneFoldAtCursor() )
                yield (
                    "zD", 
                    CommandFlags.Special, 
                    None,
                    fun _ _ -> _operations.DeleteAllFoldsAtCursor() )
                yield (
                    "zE", 
                    CommandFlags.Special, 
                    None,
                    fun _ _ -> _operations.FoldManager.DeleteAllFolds() )
                yield (
                    "zF", 
                    CommandFlags.Special, 
                    None,
                    fun count _ -> 
                        let span = SnapshotSpanUtil.ExtendDownIncludingLineBreak x.SelectedSpan (count-1)
                        _operations.FoldManager.CreateFold span )
            }
            |> Seq.map (fun (str,flags,mode,func) ->
                let kiSet = KeyNotationUtil.StringToKeyInputSet str
                let modeSwitch = 
                    match mode with
                    | None -> ModeSwitch.SwitchPreviousMode
                    | Some(switch) -> switch
                let func2 count reg = 
                    let count = CommandUtil2.CountOrDefault count
                    func count reg 
                    CommandResult.Completed modeSwitch
                CommandBinding.LegacySimpleCommand (kiSet, flags, func2) )

        /// Commands which must customize their return
        let customReturn = 
            seq {
                yield ( 
                    ":", 
                    CommandFlags.Special,
                    fun _ _ -> ModeSwitch.SwitchModeWithArgument (ModeKind.Command,ModeArgument.FromVisual) |> CommandResult.Completed) 
            }
            |> Seq.map (fun (name,flags,func) ->
                let name = KeyNotationUtil.StringToKeyInputSet name
                CommandBinding.LegacySimpleCommand (name, flags, func) )

        /// Visual Commands
        let visualSimple = 
            seq {
                yield (
                    ["D"; "X"],
                    CommandFlags.Repeatable,
                    Some ModeKind.Normal,
                    (fun _ reg span -> 
                        let range = SnapshotLineRangeUtil.CreateForSpan span
                        _operations.DeleteSpan range.ExtentIncludingLineBreak
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Delete range.ExtentIncludingLineBreak OperationKind.LineWise),
                    (fun _ reg (col : NormalizedSnapshotSpanCollection) ->
                        let col = 
                            col
                            |> Seq.map (fun span -> SnapshotSpan(span.Start, span |> SnapshotSpanUtil.GetStartLine |> SnapshotLineUtil.GetEnd))
                            |> NormalizedSnapshotSpanCollectionUtil.OfSeq
                        _operations.DeleteBlock col
                        _operations.UpdateRegisterForCollection reg RegisterOperation.Delete col OperationKind.CharacterWise))
                yield (
                    ["c"], 
                    CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextTextChange,
                    Some ModeKind.Insert,
                    (fun _ reg span -> 
                        _operations.DeleteSpan span 
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Delete span _operationKind),
                    (fun _ reg col -> 
                        _operations.DeleteBlock col
                        _operations.UpdateRegisterForCollection reg RegisterOperation.Delete col _operationKind))
                yield (
                    ["s"], 
                    CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextTextChange,
                    Some ModeKind.Insert,
                    (fun _ reg span -> 
                        _operations.DeleteSpan span 
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Delete span _operationKind),
                    (fun _ reg col -> 
                        _operations.DeleteBlock col 
                        _operations.UpdateRegisterForCollection reg RegisterOperation.Delete col _operationKind))
                yield ( 
                    ["S"],
                    CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextTextChange,
                    Some ModeKind.Insert,
                    (fun _ reg span -> 
                        let span = _operations.DeleteLinesInSpan span 
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Delete span OperationKind.LineWise),
                    (fun _ reg col -> 
                        let span = NormalizedSnapshotSpanCollectionUtil.GetCombinedSpan col 
                        let span = _operations.DeleteLinesInSpan span
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Delete span OperationKind.LineWise))
                yield (
                    ["C"],
                    CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextTextChange,
                    Some ModeKind.Insert,
                    (fun _ reg span -> 
                        let span = _operations.DeleteLinesInSpan span
                        _operations.UpdateRegisterForSpan reg RegisterOperation.Delete span OperationKind.CharacterWise),
                    (fun _ reg col -> 
                        let col = 
                            col 
                            |> Seq.map (fun span -> 
                                let line = SnapshotSpanUtil.GetStartLine span
                                SnapshotSpan(span.Start,line.End) )
                            |> NormalizedSnapshotSpanCollectionUtil.OfSeq
                        _operations.DeleteBlock col 
                        _operations.UpdateRegisterForCollection reg RegisterOperation.Delete col OperationKind.CharacterWise))
                yield (
                    ["J"],
                    CommandFlags.Repeatable,
                    None,
                    (fun _ _ span -> 
                        let range = SnapshotLineRangeUtil.CreateForSpan span
                        _operations.Join range JoinKind.RemoveEmptySpaces),
                    (fun _ _ col ->
                        let range = SnapshotLineRangeUtil.CreateForNormalizedSnapshotSpanCollection col
                        _operations.Join range JoinKind.RemoveEmptySpaces))
                yield (
                    ["gJ"],
                    CommandFlags.Repeatable,
                    None,
                    (fun _ _ span -> 
                        let range = SnapshotLineRangeUtil.CreateForSpan span
                        _operations.Join range JoinKind.KeepEmptySpaces),
                    (fun _ _ col ->
                        let range = SnapshotLineRangeUtil.CreateForNormalizedSnapshotSpanCollection col
                        _operations.Join range JoinKind.KeepEmptySpaces))
                yield (
                    ["y"],
                    CommandFlags.ResetCaret,
                    None,
                    (fun _ (reg:Register) span -> 
                        let data = StringData.OfSpan span
                        reg.Value <- { Value = data; OperationKind = _operationKind } ),
                    (fun _ (reg:Register) col -> 
                        let data = StringData.OfNormalizedSnasphotSpanCollection col
                        reg.Value <- { Value = data; OperationKind = _operationKind } ))
                yield (
                    ["Y"],
                    CommandFlags.ResetCaret,
                    None,
                    (fun _ (reg:Register) span -> 
                        let data = span |> SnapshotSpanUtil.ExtendToFullLineIncludingLineBreak |> StringData.OfSpan 
                        reg.Value <- { Value = data; OperationKind = OperationKind.LineWise } ),
                    (fun _ (reg:Register) col -> 
                        let data = 
                            let normal() = col |> Seq.map SnapshotSpanUtil.ExtendToFullLine |> StringData.OfSeq 
                            match _visualKind with 
                            | VisualKind.Character -> normal()
                            | VisualKind.Line -> normal()
                            | VisualKind.Block -> StringData.OfNormalizedSnasphotSpanCollection col
                        reg.Value <- { Value = data; OperationKind = OperationKind.LineWise} ))
            }
            |> Seq.map (fun (strList,flags,mode,funcNormal,funcBlock) ->
                strList 
                |> Seq.map (fun str ->
                    let kiSet = KeyNotationUtil.StringToKeyInputSet str
                    let modeSwitch = 
                        match mode with
                        | None -> ModeSwitch.SwitchPreviousMode
                        | Some(kind) -> ModeSwitch.SwitchMode kind
                    let func2 count reg visualSpan = 
                        let count = CommandUtil2.CountOrDefault count
                        runVisualCommand funcNormal funcBlock count reg visualSpan
                        CommandResult.Completed modeSwitch
    
                    CommandBinding.LegacyVisualCommand(kiSet, flags, _visualKind, func2)))
            |> Seq.concat

        Seq.append simples visualSimple 
        |> Seq.append customReturn
        |> Seq.append (x.CreateCommandBindings())

    /// Create the CommandBinding instances for the supported command values
    member x.CreateCommandBindings() =
        let visualSeq = 
            seq {
                yield ("d", CommandFlags.Repeatable, VisualCommand.DeleteSelectedText)
                yield ("gp", CommandFlags.Repeatable, VisualCommand.PutOverSelection true)
                yield ("gP", CommandFlags.Repeatable, VisualCommand.PutOverSelection true)
                yield ("g?", CommandFlags.Repeatable, VisualCommand.ChangeCase ChangeCharacterKind.Rot13)
                yield ("p", CommandFlags.Repeatable, VisualCommand.PutOverSelection false)
                yield ("P", CommandFlags.Repeatable, VisualCommand.PutOverSelection false)
                yield ("u", CommandFlags.Repeatable, VisualCommand.ChangeCase ChangeCharacterKind.ToLowerCase)
                yield ("U", CommandFlags.Repeatable, VisualCommand.ChangeCase ChangeCharacterKind.ToUpperCase)
                yield ("x", CommandFlags.Repeatable, VisualCommand.DeleteSelectedText)
                yield ("<Del>", CommandFlags.Repeatable, VisualCommand.DeleteSelectedText)
                yield ("<lt>", CommandFlags.Repeatable, VisualCommand.ShiftLinesLeft)
                yield (">", CommandFlags.Repeatable, VisualCommand.ShiftLinesRight)
                yield ("~", CommandFlags.Repeatable, VisualCommand.ChangeCase ChangeCharacterKind.ToggleCase)
                yield ("=", CommandFlags.Repeatable, VisualCommand.FormatLines)
            } |> Seq.map (fun (str, flags, command) -> 
                let keyInputSet = KeyNotationUtil.StringToKeyInputSet str
                CommandBinding.VisualCommand(keyInputSet, flags, command))

        let complexSeq = 
            seq {
                yield ("r", CommandFlags.Repeatable, BindData<_>.CreateForSingle None (fun keyInput -> VisualCommand.ReplaceSelection keyInput))
            } |> Seq.map (fun (str, flags, bindCommand) -> 
                let keyInputSet = KeyNotationUtil.StringToKeyInputSet str
                let storage = BindDataStorage.Simple bindCommand
                CommandBinding.ComplexVisualCommand(keyInputSet, flags, storage))

        Seq.append visualSeq complexSeq

    member x.EnsureCommandsBuilt() =
        if not _builtCommands then
            let map = 
                x.BuildMoveSequence() 
                |> Seq.append (x.BuildOperationsSequence())
                |> Seq.iter _runner.Add 
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
                    ProcessResult.SwitchPreviousMode
                else
                    let original = _buffer.TextSnapshot.Version.VersionNumber
                    match _runner.Run ki with
                    | BindResult.NeedMoreInput _ -> 
                        // Commands like incremental search can move the caret and be incomplete.  Need to 
                        // update the selection while waiting for the next key
                        _selectionTracker.UpdateSelection()
                        ProcessResult.Processed
                    | BindResult.Complete commandRanData ->

                        if Util.IsFlagSet commandRanData.CommandBinding.CommandFlags CommandFlags.ResetCaret then
                            _selectionTracker.ResetCaret()

                        match commandRanData.ModeSwitch with
                        | ModeSwitch.NoSwitch -> _selectionTracker.UpdateSelection()
                        | ModeSwitch.SwitchMode(_) -> ()
                        | ModeSwitch.SwitchModeWithArgument(_,_) -> ()
                        | ModeSwitch.SwitchPreviousMode -> ()
                        ProcessResult.OfModeSwitch commandRanData.ModeSwitch
                    | BindResult.Error ->
                        _operations.Beep()
                        ProcessResult.Processed
                    | BindResult.Cancelled -> 
                        ProcessResult.Processed

            // If we are switching out Visual Mode then reset the selection
            if result.IsAnySwitch then
                // Is this a switch to command mode? 
                let toCommandMode = 
                    match result with 
                    | ProcessResult.Processed -> false
                    | ProcessResult.ProcessNotHandled -> false
                    | ProcessResult.SwitchMode(kind) -> kind = ModeKind.Command
                    | ProcessResult.SwitchModeWithArgument(kind,_) -> kind = ModeKind.Command
                    | ProcessResult.SwitchPreviousMode -> false

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
        member x.OnClose() = ()

    interface IVisualMode with
        member x.CommandRunner = _runner
        member x.SyncSelection () = 
            if _selectionTracker.IsRunning then
                _selectionTracker.Stop()
                _selectionTracker.Start()



