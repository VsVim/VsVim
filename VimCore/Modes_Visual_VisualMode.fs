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
        _operations : IOperations,
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

        let editOverSpanOperation description func result = 
            let selection = _buffer.TextView.Selection
            let spans = selection.SelectedSpans
            match spans.Count with
            | 0 -> result
            | 1 -> 
                func (spans.Item(0))
                result
            | _ -> 
                _operations.ApplyAsSingleEdit description (spans :> SnapshotSpan seq) func
                result
            
        let deleteSelection _ reg = 
            _operations.DeleteSelection reg |> ignore
            CommandResult.Completed ModeSwitch.SwitchPreviousMode
        let changeSelection _ reg = 
            _operations.DeleteSelection reg |> ignore
            CommandResult.Completed (ModeSwitch.SwitchMode ModeKind.Insert)
        let changeLines _ reg = 
            _operations.DeleteSelectedLines reg |> ignore
            CommandResult.Completed (ModeSwitch.SwitchMode ModeKind.Insert)

        /// Commands consisting of a single character
        let simples =
            let resultSwitchPrevious = CommandResult.Completed ModeSwitch.SwitchPreviousMode
            seq {
                yield ("y",
                    (fun _ (reg:Register) -> 
                        let opKind = match _kind with
                                     | ModeKind.VisualLine -> OperationKind.LineWise
                                     | _ -> OperationKind.CharacterWise
                        _operations.YankText (_selectionTracker.SelectedText) MotionKind.Inclusive opKind reg
                        CommandResult.Completed ModeSwitch.SwitchPreviousMode))
                yield ("Y",
                    (fun _ (reg:Register) ->
                        let selection = _buffer.TextView.Selection
                        let startPoint = selection.Start.Position.GetContainingLine().Start
                        let endPoint = selection.End.Position.GetContainingLine().EndIncludingLineBreak
                        let span = SnapshotSpan(startPoint,endPoint)
                        _operations.Yank span MotionKind.Inclusive OperationKind.LineWise reg
                        CommandResult.Completed ModeSwitch.SwitchPreviousMode))
                yield ("x", deleteSelection)
                yield ("<Del>", deleteSelection)
                yield ("c", changeSelection)
                yield ("s", changeSelection)
                yield ("C", changeLines)
                yield ("S", changeLines)
                yield ("J",
                        (fun _ _ ->         
                            _operations.JoinSelection JoinKind.RemoveEmptySpaces|> ignore
                            CommandResult.Completed ModeSwitch.SwitchPreviousMode))
                yield (
                    "~",
                    (fun _ _ -> editOverSpanOperation None _operations.ChangeLetterCase resultSwitchPrevious))
                yield (
                    "<lt>",
                    (fun count _ -> 
                        let count = CommandUtil.CountOrDefault count
                        editOverSpanOperation None (_operations.ShiftSpanLeft count) resultSwitchPrevious))
                yield (
                    ">",
                    (fun count _ -> 
                        let count = CommandUtil.CountOrDefault count
                        editOverSpanOperation None (_operations.ShiftSpanRight count) resultSwitchPrevious))
                yield (
                    "p",
                    (fun _ reg -> 
                        _operations.PasteOverSelection reg.StringValue reg
                        CommandResult.Completed ModeSwitch.SwitchPreviousMode ) )
                yield ( ":", fun _ _ -> ModeSwitch.SwitchModeWithArgument (ModeKind.Command,ModeArgument.FromVisual) |> CommandResult.Completed)
            }
            |> Seq.map (fun (str,func) -> ((KeyNotationUtil.StringToKeyInputSet str),func))
            |> Seq.map (fun (kiSet,func) -> Command.SimpleCommand(kiSet,CommandFlags.None, func))


        /// Commands consisting of more than a single character
        let complex = 
            seq { 
                yield ("gJ", CommandFlags.Repeatable, fun _ _ -> _operations.JoinSelection JoinKind.KeepEmptySpaces |> ignore)
                yield ("zo", CommandFlags.Special, fun _ _ -> _operations.OpenFold _operations.SelectedSpan 1)
                yield ("zO", CommandFlags.Special, fun _ _ -> _operations.OpenAllFolds _operations.SelectedSpan )
                yield ("zc", CommandFlags.Special, fun _ _ -> _operations.CloseFold _operations.SelectedSpan 1)
                yield ("zC", CommandFlags.Special, fun _ _ -> _operations.CloseAllFolds _operations.SelectedSpan )
                yield ("zf", CommandFlags.Special, fun _ _ -> _operations.FoldManager.CreateFold _operations.SelectedSpan)
                yield ("zd", CommandFlags.Special, fun _ _ -> _operations.DeleteOneFoldAtCursor() )
                yield ("zD", CommandFlags.Special, fun _ _ -> _operations.DeleteAllFoldsAtCursor() )
                yield ("zE", CommandFlags.Special, fun _ _ -> _operations.FoldManager.DeleteAllFolds() )
                yield ("zF", CommandFlags.Special, fun count _ -> 
                    let count = CommandUtil.CountOrDefault count
                    let span = SnapshotSpanUtil.ExtendDownIncludingLineBreak _operations.SelectedSpan (count-1)
                    _operations.FoldManager.CreateFold span )
            }
            |> Seq.map (fun (str,flags,func) -> (str, flags,fun count reg -> func count reg; CommandResult.Completed ModeSwitch.SwitchPreviousMode))
            |> Seq.map (fun (name,flags,func) -> 
                let name = KeyNotationUtil.StringToKeyInputSet name
                Command.SimpleCommand (name, flags, func))

        /// Visual Commands
        let visualSimple = 
            seq {
                yield ("d", CommandFlags.Repeatable, Some ModeKind.Normal, fun count reg span -> _operations.DeleteSpan span _motionKind _operationKind reg |> ignore)
            }
            |> Seq.map (fun (str,flags,mode,func) ->
                let kiSet = KeyNotationUtil.StringToKeyInputSet str
                let modeSwitch = 
                    match mode with
                    | None -> ModeSwitch.SwitchPreviousMode
                    | Some(kind) -> ModeSwitch.SwitchMode kind
                let func2 count reg span = 
                    let count = CommandUtil.CountOrDefault count
                    func count reg span 
                    CommandResult.Completed modeSwitch
                Command.VisualCommand(kiSet, flags, _visualKind, func2) )
                
        Seq.append simples complex |> Seq.append visualSimple

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
                | RunKeyInputResult.CommandRan(_,modeSwitch) -> ProcessResult.OfModeSwitch modeSwitch
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



