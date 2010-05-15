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
            | Command.LongCommand (name,kind,func) -> None )
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
                yield (InputUtil.CharToKeyInput('y'), 
                    (fun _ (reg:Register) -> 
                        let opKind = match _kind with
                                     | ModeKind.VisualLine -> OperationKind.LineWise
                                     | _ -> OperationKind.CharacterWise
                        _operations.YankText (_selectionTracker.SelectedText) MotionKind.Inclusive opKind reg
                        CommandResult.Completed ModeSwitch.SwitchPreviousMode))
                yield (InputUtil.CharToKeyInput('Y'),
                    (fun _ (reg:Register) ->
                        let selection = _buffer.TextView.Selection
                        let startPoint = selection.Start.Position.GetContainingLine().Start
                        let endPoint = selection.End.Position.GetContainingLine().EndIncludingLineBreak
                        let span = SnapshotSpan(startPoint,endPoint)
                        _operations.Yank span MotionKind.Inclusive OperationKind.LineWise reg
                        CommandResult.Completed ModeSwitch.SwitchPreviousMode))
                yield (InputUtil.CharToKeyInput('d'), deleteSelection)
                yield (InputUtil.CharToKeyInput('x'), deleteSelection)
                yield (InputUtil.VimKeyToKeyInput VimKey.DeleteKey, deleteSelection)
                yield (InputUtil.CharToKeyInput('c'), changeSelection)
                yield (InputUtil.CharToKeyInput('s'), changeSelection)
                yield (InputUtil.CharToKeyInput('C'), changeLines)
                yield (InputUtil.CharToKeyInput('S'), changeLines)
                yield (InputUtil.CharToKeyInput('J'), 
                        (fun _ _ ->         
                            _operations.JoinSelection JoinKind.RemoveEmptySpaces|> ignore
                            CommandResult.Completed ModeSwitch.SwitchPreviousMode))
                yield (
                    InputUtil.CharToKeyInput '~', 
                    (fun _ _ -> editOverSpanOperation None _operations.ChangeLetterCase resultSwitchPrevious))
                yield (
                    InputUtil.CharToKeyInput '<', 
                    (fun count _ -> 
                        let count = CommandUtil.CountOrDefault count
                        editOverSpanOperation None (_operations.ShiftSpanLeft count) resultSwitchPrevious))
                yield (
                    InputUtil.CharToKeyInput '>',
                    (fun count _ -> 
                        let count = CommandUtil.CountOrDefault count
                        editOverSpanOperation None (_operations.ShiftSpanRight count) resultSwitchPrevious))
                yield (
                    InputUtil.CharToKeyInput 'p',
                    (fun _ reg -> 
                        _operations.PasteOverSelection reg.StringValue reg
                        CommandResult.Completed ModeSwitch.SwitchPreviousMode ) )
            }
            |> Seq.map (fun (ki,func) -> Command.SimpleCommand(OneKeyInput ki,CommandFlags.None, func))


        /// Commands consisting of more than a single character
        let complex = 
            seq { 
                yield ("gJ", fun _ _ -> _operations.JoinSelection JoinKind.KeepEmptySpaces |> ignore)
            }
            |> Seq.map (fun (str,func) -> (str, fun count reg -> func count reg; CommandResult.Completed ModeSwitch.SwitchPreviousMode))
            |> Seq.map (fun (name,func) -> Command.SimpleCommand (CommandUtil.CreateCommandName name, CommandFlags.Repeatable, func))

        Seq.append simples complex

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
            _runner.Commands |> Seq.map (fun command -> command.CommandName)
        member x.ModeKind = _kind
        member x.CanProcess (ki:KeyInput) = true
        member x.Process (ki : KeyInput) =  
            if ki.Key = VimKey.EscapeKey then
                ProcessResult.SwitchPreviousMode
            else
                match _runner.Run ki with
                | RunKeyInputResult.NeedMoreKeyInput -> ProcessResult.Processed
                | RunKeyInputResult.NestedRunDetected -> ProcessResult.Processed
                | RunKeyInputResult.CommandRan(_,modeSwitch) ->
                    match modeSwitch with
                    | ModeSwitch.NoSwitch -> ProcessResult.Processed
                    | ModeSwitch.SwitchMode(kind) -> ProcessResult.SwitchMode kind
                    | ModeSwitch.SwitchPreviousMode -> ProcessResult.SwitchPreviousMode
                | RunKeyInputResult.CommandErrored(_) -> ProcessResult.SwitchPreviousMode
                | RunKeyInputResult.CommandCancelled -> ProcessResult.SwitchPreviousMode
                | RunKeyInputResult.NoMatchingCommand -> 
                    _operations.Beep()
                    ProcessResult.Processed
    
        member x.OnEnter () = 
            x.EnsureCommandsBuilt()
            _selectionTracker.Start()
        member x.OnLeave () = 
            _runner.ResetState()
            _selectionTracker.Stop()
        member x.OnClose() = ()

    interface IVisualMode with
        member x.InExplicitMove = x.InExplicitMove
        member x.CommandRunner = _runner



