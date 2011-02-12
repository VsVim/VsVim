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

    /// Tracks the count of explicit moves we are seeing.  Normally an explicit character
    /// move causes the selection to be removed.  Updating this counter is a way of our 
    /// consumers to tell us the caret move is legal
    let mutable _explicitMoveCount = 0

    member x.InExplicitMove = _explicitMoveCount > 0
    member x.BeginExplicitMove() = _explicitMoveCount <- _explicitMoveCount + 1
    member x.EndExplicitMove() = _explicitMoveCount <- _explicitMoveCount - 1
    member x.SelectedSpan = (TextSelectionUtil.GetStreamSelectionSpan _buffer.TextView.Selection).SnapshotSpan

    member x.BuildMoveSequence() = 

        let wrapSimple func = 
            fun count reg ->
                x.BeginExplicitMove()
                let res = func count reg
                x.EndExplicitMove()
                res

        let wrapMotion func = 
            fun count reg data ->
                x.BeginExplicitMove()
                let res = func count reg data
                x.EndExplicitMove()
                res

        let wrapLong func = 
            fun count reg ->
                x.BeginExplicitMove()
                let res = func count reg 
                x.EndExplicitMove()
                res

        let factory = Vim.Modes.CommandFactory(_operations, _capture, _buffer.TextViewMotionUtil, _buffer.JumpList, _buffer.Settings)
        factory.CreateMovementCommands()
        |> Seq.map (fun (command) ->
            match command with
            | CommandBinding.SimpleCommand(name, flags, func) -> CommandBinding.SimpleCommand (name, flags, wrapSimple func) |> Some
            | CommandBinding.MotionCommand (name, flags, func) -> CommandBinding.MotionCommand (name, flags,wrapMotion func) |> Some
            | CommandBinding.VisualCommand (_) -> Some command
            | CommandBinding.NormalCommand2 _ -> None
            | CommandBinding.MotionCommand2 _ -> None
            | CommandBinding.VisualCommand2 _ -> None
            | CommandBinding.ComplexNormalCommand _ -> None
            | CommandBinding.ComplexVisualCommand _ -> None)
        |> SeqUtil.filterToSome

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
                CommandBinding.SimpleCommand (kiSet, flags, func2) )

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
                CommandBinding.SimpleCommand (name, flags, func) )

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
                    ["<lt>"],
                    CommandFlags.Repeatable ||| CommandFlags.ResetCaret,
                    Some ModeKind.Normal,
                    (fun count _ span -> _operations.ShiftLineRangeLeft count (SnapshotLineRangeUtil.CreateForSpan span)) ,
                    (fun count _ col -> _operations.ShiftBlockLeft count col ))
                yield (
                    [">"],
                    CommandFlags.Repeatable ||| CommandFlags.ResetCaret,
                    Some ModeKind.Normal,
                    (fun count _ span ->  _operations.ShiftLineRangeRight count (SnapshotLineRangeUtil.CreateForSpan span)),
                    (fun count _ col -> _operations.ShiftBlockRight count col))
                yield (
                    ["~"],
                    CommandFlags.Repeatable,
                    Some ModeKind.Normal,
                    (fun _ _ span -> _operations.ChangeLetterCase (EditSpan.Single span)),
                    (fun _ _ col -> _operations.ChangeLetterCase (EditSpan.Block col)))
                yield (
                    ["U"],
                    CommandFlags.Repeatable,
                    Some ModeKind.Normal, 
                    (fun _ _ span -> _operations.ChangeLetterCaseToUpper (EditSpan.Single span)),
                    (fun _ _ col -> _operations.ChangeLetterCaseToUpper (EditSpan.Block col)))
                yield (
                    ["u"],
                    CommandFlags.Repeatable,
                    Some ModeKind.Normal, 
                    (fun _ _ span -> _operations.ChangeLetterCaseToLower (EditSpan.Single span)),
                    (fun _ _ col -> _operations.ChangeLetterCaseToLower (EditSpan.Block col)))
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
                    ["p"],
                    CommandFlags.Repeatable,
                    None,
                    (fun count reg _ -> _operations.PutAtCaret (reg.Value.Value.ApplyCount count) reg.Value.OperationKind PutKind.Before false),
                    (fun count reg _ -> _operations.PutAtCaret (reg.Value.Value.ApplyCount count) reg.Value.OperationKind PutKind.Before false))
                yield (
                    ["P"],
                    CommandFlags.Repeatable,
                    None,
                    (fun count reg _ -> _operations.PutAtCaret (reg.Value.Value.ApplyCount count) reg.Value.OperationKind PutKind.Before false),
                    (fun count reg _ -> _operations.PutAtCaret (reg.Value.Value.ApplyCount count) reg.Value.OperationKind PutKind.Before false))
                yield (
                    ["gp"],
                    CommandFlags.Repeatable,
                    None,
                    (fun count reg _ -> _operations.PutAtCaret (reg.Value.Value.ApplyCount count) reg.Value.OperationKind PutKind.Before true),
                    (fun count reg _ -> _operations.PutAtCaret (reg.Value.Value.ApplyCount count) reg.Value.OperationKind PutKind.Before true))
                yield (
                    ["gP"],
                    CommandFlags.Repeatable,
                    None,
                    (fun count reg _ -> _operations.PutAtCaret (reg.Value.Value.ApplyCount count) reg.Value.OperationKind PutKind.Before true),
                    (fun count reg _ -> _operations.PutAtCaret (reg.Value.Value.ApplyCount count) reg.Value.OperationKind PutKind.Before true))
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
                yield (
                    ["="],
                    CommandFlags.Repeatable,
                    None,
                    (fun _ _ span -> 
                        let range = SnapshotLineRangeUtil.CreateForSpan span
                        _buffer.Vim.VimHost.FormatLines _buffer.TextView range),
                    (fun _ _ col ->
                        let range = 
                            col
                            |> NormalizedSnapshotSpanCollectionUtil.GetCombinedSpan
                            |> SnapshotLineRangeUtil.CreateForSpan 
                        _buffer.Vim.VimHost.FormatLines _buffer.TextView range))
                yield (
                    ["g?"],
                    CommandFlags.Repeatable,
                    Some ModeKind.Normal, 
                    (fun _ _ span -> _operations.ChangeLetterRot13 (EditSpan.Single span)),
                    (fun _ _ col -> _operations.ChangeLetterRot13 (EditSpan.Block col)))
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
    
                    CommandBinding.VisualCommand(kiSet, flags, _visualKind, func2)))
            |> Seq.concat

        Seq.append simples visualSimple 
        |> Seq.append customReturn
        |> Seq.append (x.CreateCommandBindings())

    /// Create the CommandBinding instances for the supported command values
    member x.CreateCommandBindings() =
        let visualSeq = 
            seq {
                yield ("d", CommandFlags.Repeatable, VisualCommand.DeleteHighlightedText)
                yield ("x", CommandFlags.Repeatable, VisualCommand.DeleteHighlightedText)
                yield ("<Del>", CommandFlags.Repeatable, VisualCommand.DeleteHighlightedText)
            } |> Seq.map (fun (str, flags, command) -> 
                let keyInputSet = KeyNotationUtil.StringToKeyInputSet str
                CommandBinding.VisualCommand2(keyInputSet, flags, command))

        let complexSeq = 
            seq {
                yield ("r", CommandFlags.Repeatable, BindData<_>.CreateForSingle None (fun keyInput -> VisualCommand.ReplaceChar keyInput))
            } |> Seq.map (fun (str, flags, bindCommand) -> 
                let keyInputSet = KeyNotationUtil.StringToKeyInputSet str
                CommandBinding.ComplexVisualCommand(keyInputSet, flags, bindCommand))

        Seq.append visualSeq complexSeq

    member x.EnsureCommandsBuilt() =
        if not _builtCommands then
            let map = 
                x.BuildMoveSequence() 
                |> Seq.append (x.BuildOperationsSequence())
                |> Seq.iter _runner.Add 
            _builtCommands <- true

    member x.ShouldHandleEscape = 
        match _runner.State with
        | CommandRunnerState.NoInput -> true
        | CommandRunnerState.NotEnoughInput -> true
        | CommandRunnerState.NotEnoughMatchingPrefix (_) -> true
        | CommandRunnerState.NotFinishWithCommand (command, _) -> not (Util.IsFlagSet command.CommandFlags CommandFlags.HandlesEscape)

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
                        ProcessResult.SwitchPreviousMode
                    | BindResult.Cancelled -> 
                        ProcessResult.SwitchPreviousMode

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
        member x.InExplicitMove = x.InExplicitMove
        member x.CommandRunner = _runner
        member x.SyncSelection () = 
            if _selectionTracker.IsRunning then
                _selectionTracker.Stop()
                _selectionTracker.Start()



