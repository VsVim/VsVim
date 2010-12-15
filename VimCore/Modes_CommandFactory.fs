#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal CommandFactory
    ( 
        _operations : ICommonOperations, 
        _capture : IMotionCapture,
        _incrementalSearch : IIncrementalSearch,
        _jumpList : IJumpList ) =

    let _textView = _operations.TextView

    member x.CreateStandardMovementCommandsCore () = 
        let moveLeft = fun count -> _operations.MoveCaretLeft(count)
        let moveRight = fun count -> _operations.MoveCaretRight(count)
        let moveUp = fun count -> _operations.MoveCaretUp(count)
        let moveDown = fun count -> _operations.MoveCaretDown(count)

        seq {
            yield ("h", moveLeft)
            yield ("<Left>", moveLeft)
            yield ("<Bs>", moveLeft)
            yield ("<C-h>", moveLeft)
            yield ("l", moveRight)
            yield ("<Right>", moveRight)
            yield ("<Space>", moveRight)
            yield ("k", moveUp)
            yield ("<Up>", moveUp)
            yield ("<C-p>", moveUp)
            yield ("j", moveDown)
            yield ("<Down>", moveDown)
            yield ("<C-n>", moveDown)
            yield ("<C-j>", moveDown)
            yield ("n", fun count -> _operations.MoveToNextOccuranceOfLastSearch count false)
            yield ("N", fun count -> _operations.MoveToNextOccuranceOfLastSearch count true)
            yield ("*", fun count -> _operations.MoveToNextOccuranceOfWordAtCursor SearchKind.ForwardWithWrap count)
            yield ("#", fun count -> _operations.MoveToNextOccuranceOfWordAtCursor SearchKind.BackwardWithWrap count)
            yield ("g*", fun count -> _operations.MoveToNextOccuranceOfPartialWordAtCursor SearchKind.ForwardWithWrap count)
            yield ("g#", fun count -> _operations.MoveToNextOccuranceOfPartialWordAtCursor SearchKind.BackwardWithWrap count)
            yield ("gd", fun _ -> _operations.GoToLocalDeclaration())
            yield ("gD", fun  _ -> _operations.GoToGlobalDeclaration())
        }

    member x.CreateStandardMovementCommands() =
        x.CreateStandardMovementCommandsCore()
        |> Seq.map (fun (notation,func) ->
            let kiSet = KeyNotationUtil.StringToKeyInputSet notation
            let funcWithReg opt reg = 
                func (CommandUtil.CountOrDefault opt)
                CommandResult.Completed NoSwitch
            Command.SimpleCommand (kiSet,CommandFlags.Movement, funcWithReg))

    member x.CreateSearchCommands() = 

        let searchFunc kind = 
            let before = TextViewUtil.GetCaretPoint _textView
            let rec inner (ki:KeyInput) = 
                match _incrementalSearch.Process ki with
                | SearchComplete -> 
                    _jumpList.Add before |> ignore
                    CommandResult.Completed ModeSwitch.NoSwitch |> LongCommandResult.Finished
                | SearchCancelled -> LongCommandResult.Cancelled
                | SearchNeedMore ->  LongCommandResult.NeedMoreInput (Some KeyRemapMode.Command, inner)
            _incrementalSearch.Begin kind
            LongCommandResult.NeedMoreInput (Some KeyRemapMode.Command, inner)

        seq {
            yield ("/", fun () -> searchFunc SearchKind.ForwardWithWrap)
            yield ("?", fun () -> searchFunc SearchKind.BackwardWithWrap)
        } |> Seq.map (fun (notation, func) ->
            let keyInputSet = KeyNotationUtil.StringToKeyInputSet notation
            let commandFunc _ _ = func()
            let flags = CommandFlags.Movement ||| CommandFlags.HandlesEscape
            Command.LongCommand (keyInputSet, flags, commandFunc))

    /// Build up a set of MotionCommand values from applicable Motion values
    member x.CreateMovementsFromMotions() =
        let processResult opt = 
            match opt with
            | None -> _operations.Beep()
            | Some(data) -> _operations.MoveCaretToMotionData data
            CommandResult.Completed NoSwitch

        let makeMotionArgument count = {MotionContext=MotionContext.Movement; OperatorCount=None; MotionCount=count}

        let processMotionCommand command =
            match command with
            | SimpleMotionCommand(name,_,func) -> 
                let inner count _ =  
                    let arg = makeMotionArgument count
                    func arg |> processResult
                Command.SimpleCommand(name,CommandFlags.Movement,inner) 
            | ComplexMotionCommand(name,_,func) -> 
                
                let coreFunc count _ = 
                    let rec inner result =  
                        match result with
                        | ComplexMotionResult.Finished(func) ->
                            let res = 
                                let arg = makeMotionArgument count
                                match func arg with
                                | None -> CommandResult.Error Resources.MotionCapture_InvalidMotion
                                | Some(data) -> 
                                    _operations.MoveCaretToMotionData data
                                    CommandResult.Completed NoSwitch 
                            res |> LongCommandResult.Finished
                        | ComplexMotionResult.NeedMoreInput (keyRemapMode, func) -> 
                            LongCommandResult.NeedMoreInput (keyRemapMode, fun ki -> func ki |> inner)
                        | ComplexMotionResult.Cancelled -> 
                            LongCommandResult.Cancelled
                        | ComplexMotionResult.Error (msg) -> 
                            CommandResult.Error msg |> LongCommandResult.Finished

                    let initialResult = func()
                    inner initialResult
                Command.LongCommand(name, CommandFlags.Movement, coreFunc) 

        _capture.MotionCommands
        |> Seq.filter (fun command -> Utils.IsFlagSet command.MotionFlags MotionFlags.CursorMovement)
        |> Seq.map processMotionCommand

    member x.CreateMovementCommands() = 
        let standard = x.CreateStandardMovementCommands()
        let taken = standard |> Seq.map (fun command -> command.KeyInputSet) |> Set.ofSeq
        let motion = 
            x.CreateMovementsFromMotions()
            |> Seq.filter (fun command -> not (taken.Contains command.KeyInputSet))
        standard |> Seq.append motion |> Seq.append (x.CreateSearchCommands())

