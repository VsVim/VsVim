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

    member private x.BuildMoveSequence() = 

        let wrapSimple func = 
            fun count reg ->
                x.BeginExplicitMove()
                let res = func count reg
                x.EndExplicitMove()
                res

        let wrapComplex func = 
            fun count reg data ->
                x.BeginExplicitMove()
                let res = func count reg data
                x.EndExplicitMove()
                res

        let factory = Vim.Modes.CommandFactory(_operations, _capture)
        factory.CreateMovementCommands()
        |> Seq.map (fun (command) ->
            match command with
            | Command.SimpleCommand(name,kind,func) -> Command.SimpleCommand (name,kind, wrapSimple func) |> Some
            | Command.MotionCommand (name,kind,func) -> Command.MotionCommand (name, kind,wrapComplex func) |> Some
            | Command.LongCommand (name,kind,func) -> None 
            | Command.VisualCommand (name,kind,visualKind,func) -> None )
        |> SeqUtil.filterToSome

    member private x.BuildOperationsSequence() =

        let runVisualCommand funcNormal funcBlock count reg visualSpan = 
            match visualSpan with
            | VisualSpan.Single(_,span) -> funcNormal count reg span
            | VisualSpan.Multiple(_,col) -> funcBlock count reg col


        /// Commands consisting of a single character
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
                    let count = CommandUtil.CountOrDefault count
                    func count reg 
                    CommandResult.Completed modeSwitch
                Command.SimpleCommand (kiSet, flags, func2) )

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
                Command.SimpleCommand (name, flags, func) )

        /// Visual Commands
        let visualSimple = 
            seq {
                yield (
                    "d", 
                    CommandFlags.Repeatable, 
                    Some ModeKind.Normal, 
                    (fun _ reg span -> _operations.DeleteSpan span _motionKind _operationKind reg |> ignore),
                    (fun _ reg col -> _operations.DeleteBlock col reg))
                yield (
                    "x", 
                    CommandFlags.Repeatable, 
                    Some ModeKind.Normal, 
                    (fun count reg span -> _operations.DeleteSpan span _motionKind _operationKind reg |> ignore),
                    (fun _ reg col -> _operations.DeleteBlock col reg))
                yield (
                    "<Del>", 
                    CommandFlags.Repeatable, 
                    Some ModeKind.Normal, 
                    (fun count reg span -> _operations.DeleteSpan span _motionKind _operationKind reg |> ignore),
                    (fun _ reg col -> _operations.DeleteBlock col reg))
                yield (
                    "<lt>",
                    CommandFlags.Repeatable,
                    Some ModeKind.Normal,
                    (fun count _ span -> _operations.ShiftSpanLeft count span) ,
                    (fun count _ col -> _operations.ShiftBlockLeft count col ))
                yield (
                    ">",
                    CommandFlags.Repeatable,
                    Some ModeKind.Normal,
                    (fun count _ span ->  _operations.ShiftSpanRight count span),
                    (fun count _ col -> _operations.ShiftBlockRight count col))
                yield (
                    "~",
                    CommandFlags.Repeatable,
                    Some ModeKind.Normal,
                    (fun _ _ span -> _operations.ChangeLetterCase span),
                    (fun _ _ col -> _operations.ChangeLetterCaseBlock col))
                yield (
                    "c", 
                    CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextTextChange,
                    Some ModeKind.Insert,
                    (fun _ reg span -> _operations.DeleteSpan span _motionKind _operationKind reg |> ignore),
                    (fun _ reg col -> _operations.DeleteBlock col reg ))
                yield (
                    "s", 
                    CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextTextChange,
                    Some ModeKind.Insert,
                    (fun _ reg span -> _operations.DeleteSpan span _motionKind _operationKind reg |> ignore),
                    (fun _ reg col -> _operations.DeleteBlock col reg))
                yield ( 
                    "S",
                    CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextTextChange,
                    Some ModeKind.Insert,
                    (fun _ reg span -> _operations.DeleteLinesInSpan span reg),
                    (fun _ reg col -> 
                        let span = NormalizedSnapshotSpanCollectionUtil.GetFullSpan col 
                        _operations.DeleteLinesInSpan span reg))
                yield (
                    "C",
                    CommandFlags.Repeatable ||| CommandFlags.LinkedWithNextTextChange,
                    Some ModeKind.Insert,
                    (fun _ reg span -> _operations.DeleteLinesInSpan span reg),
                    (fun _ reg col -> 
                        let col = 
                            col 
                            |> Seq.map (fun span -> 
                                let line = SnapshotSpanUtil.GetStartLine span
                                SnapshotSpan(span.Start,line.End) )
                            |> NormalizedSnapshotSpanCollectionUtil.OfSeq
                        _operations.DeleteBlock col reg))
                yield (
                    "J",
                    CommandFlags.Repeatable,
                    None,
                    (fun _ _ span -> _operations.JoinSpan span JoinKind.RemoveEmptySpaces),
                    (fun _ _ col ->
                        let span = NormalizedSnapshotSpanCollectionUtil.GetFullSpan col 
                        _operations.JoinSpan span JoinKind.RemoveEmptySpaces))
                yield (
                    "gJ",
                    CommandFlags.Repeatable,
                    None,
                    (fun _ _ span -> _operations.JoinSpan span JoinKind.KeepEmptySpaces),
                    (fun _ _ col ->
                        let span = NormalizedSnapshotSpanCollectionUtil.GetFullSpan col 
                        _operations.JoinSpan span JoinKind.KeepEmptySpaces))
                yield (
                    "p",
                    CommandFlags.None,
                    None,
                    (fun _ reg span -> _operations.PasteOver span reg),
                    (fun _ _ col -> ()) )
                yield (
                    "y",
                    CommandFlags.None,
                    None,
                    (fun _ (reg:Register) span -> 
                        let data = StringData.OfSpan span
                        reg.UpdateValue { Value = data; MotionKind = _motionKind; OperationKind = _operationKind } ),
                    (fun _ (reg:Register) col -> 
                        let data = StringData.OfNormalizedSnasphotSpanCollection col
                        reg.UpdateValue { Value = data; MotionKind = _motionKind; OperationKind = _operationKind } ))
                yield (
                    "Y",
                    CommandFlags.None,
                    None,
                    (fun _ (reg:Register) span -> 
                        let data = span |> SnapshotSpanUtil.ExtendToFullLineIncludingLineBreak |> StringData.OfSpan 
                        reg.UpdateValue { Value = data; MotionKind = _motionKind; OperationKind = OperationKind.LineWise } ),
                    (fun _ (reg:Register) col -> 
                        let data = 
                            let normal() = col |> Seq.map SnapshotSpanUtil.ExtendToFullLine |> StringData.OfSeq 
                            match _visualKind with 
                            | VisualKind.Character -> normal()
                            | VisualKind.Line -> normal()
                            | VisualKind.Block -> StringData.OfNormalizedSnasphotSpanCollection col
                        reg.UpdateValue { Value = data; MotionKind = _motionKind; OperationKind = OperationKind.LineWise} ))
            }
            |> Seq.map (fun (str,flags,mode,funcNormal,funcBlock) ->
                let kiSet = KeyNotationUtil.StringToKeyInputSet str
                let modeSwitch = 
                    match mode with
                    | None -> ModeSwitch.SwitchPreviousMode
                    | Some(kind) -> ModeSwitch.SwitchMode kind
                let func2 count reg visualSpan = 
                    let count = CommandUtil.CountOrDefault count
                    runVisualCommand funcNormal funcBlock count reg visualSpan
                    CommandResult.Completed modeSwitch

                Command.VisualCommand(kiSet, flags, _visualKind, func2) )
                
        Seq.append simples visualSimple |> Seq.append customReturn

    member private x.EnsureCommandsBuilt() =
        if not _builtCommands then
            let map = 
                x.BuildMoveSequence() 
                |> Seq.append (x.BuildOperationsSequence())
                |> Seq.iter _runner.Add 
            _builtCommands <- true

    interface IMode with
        member x.VimBuffer = _buffer
        member x.CommandNames = 
            x.EnsureCommandsBuilt()
            _runner.Commands |> Seq.map (fun command -> command.KeyInputSet)
        member x.ModeKind = _kind
        member x.CanProcess (ki:KeyInput) = true
        member x.Process (ki : KeyInput) =  
            if ki.Key = VimKey.Escape then
                ProcessResult.SwitchPreviousMode
            else
                match _runner.Run ki with
                | RunKeyInputResult.NeedMoreKeyInput -> ProcessResult.Processed
                | RunKeyInputResult.NestedRunDetected -> ProcessResult.Processed
                | RunKeyInputResult.CommandRan(_,modeSwitch) -> 
                    match modeSwitch with
                    | ModeSwitch.NoSwitch -> _selectionTracker.UpdateSelection()
                    | ModeSwitch.SwitchMode(_) -> ()
                    | ModeSwitch.SwitchModeWithArgument(_,_) -> ()
                    | ModeSwitch.SwitchPreviousMode -> ()
                    ProcessResult.OfModeSwitch modeSwitch
                | RunKeyInputResult.CommandErrored(_) -> ProcessResult.SwitchPreviousMode
                | RunKeyInputResult.CommandCancelled -> ProcessResult.SwitchPreviousMode
                | RunKeyInputResult.NoMatchingCommand -> 
                    _operations.Beep()
                    ProcessResult.Processed
    
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



