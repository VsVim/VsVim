#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal CommandFactory( _operations : ICommonOperations, _capture : IMotionCapture ) = 

    member private x.CreateStandardMovementCommandsCore () = 
        let moveLeft = fun count -> _operations.MoveCaretLeft(count)
        let moveRight = fun count -> _operations.MoveCaretRight(count)
        let moveUp = fun count -> _operations.MoveCaretUp(count)
        let moveDown = fun count -> _operations.MoveCaretDown(count)

        seq {
            yield ("h", moveLeft)
            yield ("<Left>", moveLeft)
            yield ("<Bs>", moveLeft)
            yield ("CTRL-h", moveLeft)
            yield ("l", moveRight)
            yield ("<Right>", moveRight)
            yield ("<Space>", moveRight)
            yield ("k", moveUp)
            yield ("<Up>", moveUp)
            yield ("CTRL-p", moveUp)
            yield ("j", moveDown)
            yield ("<Down>", moveDown)
            yield ("CTRL-n", moveDown)
            yield ("CTRL-j", moveDown)
        }

    member private x.CreateStandardMovementCommands() =
        x.CreateStandardMovementCommandsCore()
        |> Seq.map (fun (notation,func) ->
            let kiSet = notation |> KeyNotationUtil.StringToKeyInput |> OneKeyInput
            let funcWithReg opt reg = 
                func (CommandUtil.CountOrDefault opt)
                CommandResult.Completed NoSwitch
            Command.SimpleCommand (kiSet,CommandFlags.Movement, funcWithReg))

    /// Build up a set of MotionCommand values from applicable Motion values
    member private x.CreateMovementsFromMotions() =
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
                        | ComplexMotionResult.NeedMoreInput (func) -> LongCommandResult.NeedMoreInput (fun ki -> func ki |> inner)
                        | ComplexMotionResult.Cancelled -> LongCommandResult.Cancelled
                        | ComplexMotionResult.Error (msg) -> CommandResult.Error msg |> LongCommandResult.Finished

                    let initialResult = func()
                    inner initialResult
                Command.LongCommand(name, CommandFlags.Movement, coreFunc) 

        _capture.MotionCommands
        |> Seq.filter (fun command -> Utils.IsFlagSet command.MotionFlags MotionFlags.CursorMovement)
        |> Seq.map processMotionCommand

    /// Returns the set of commands which move the cursor.  This includes all motions which are 
    /// valid as movements.  Several of these are overridden with custom movement behavior though.
    member x.CreateMovementCommands() = 
        let standard = x.CreateStandardMovementCommands()
        let taken = standard |> Seq.map (fun command -> command.KeyInputSet) |> Set.ofSeq
        let motion = 
            x.CreateMovementsFromMotions()
            |> Seq.filter (fun command -> not (taken.Contains command.KeyInputSet))
        standard |> Seq.append motion

