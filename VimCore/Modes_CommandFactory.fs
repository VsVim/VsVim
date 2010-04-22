#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal MovementResult =
    | MovementComplete
    | MovementNeedMore of (KeyInput -> MovementResult)
    | MovementError of string

type internal MovementCommand = 
    | SimpleMovementCommand of (int -> unit) 
    | ComplexMovementCommand of (int -> MovementResult)

type internal CommandFactory( _operations : ICommonOperations) = 

    member private x.CreateStandardMovementCommandsCore () = 
        let moveLeft = fun count -> _operations.MoveCaretLeft(count)
        let moveRight = fun count -> _operations.MoveCaretRight(count)
        let moveUp = fun count -> _operations.MoveCaretUp(count)
        let moveDown = fun count -> _operations.MoveCaretDown(count)

        seq {
            yield (InputUtil.CharToKeyInput('h'), moveLeft)
            yield (InputUtil.VimKeyToKeyInput VimKey.LeftKey, moveLeft)
            yield (InputUtil.VimKeyToKeyInput VimKey.BackKey, moveLeft)
            yield (KeyInput('h', KeyModifiers.Control), moveLeft)
            yield (InputUtil.CharToKeyInput('l'), moveRight)
            yield (InputUtil.VimKeyToKeyInput VimKey.RightKey, moveRight)
            yield (InputUtil.CharToKeyInput ' ', moveRight)
            yield (InputUtil.CharToKeyInput('k'), moveUp)
            yield (InputUtil.VimKeyToKeyInput VimKey.UpKey, moveUp)
            yield (KeyInput('p', KeyModifiers.Control), moveUp)
            yield (InputUtil.CharToKeyInput('j'), moveDown)
            yield (InputUtil.VimKeyToKeyInput VimKey.DownKey, moveDown)
            yield (KeyInput('n', KeyModifiers.Control),moveDown)
            yield (KeyInput('j', KeyModifiers.Control),moveDown)        
        }

    member private x.CreateStandardMovementCommands() =
        x.CreateStandardMovementCommandsCore()
        |> Seq.map (fun (ki,func) ->
            let funcWithReg opt reg = 
                func (CommandUtil.CountOrDefault opt)
                CommandCompleted
            SimpleCommand ([ki],funcWithReg))

    member private x.CreateStandardMovementCommandsOld () = 
        x.CreateStandardMovementCommandsCore()
        |> Seq.map (fun (x,y) -> x,SimpleMovementCommand y)

    /// Build up a set of MotionCommand values from applicable Motion values
    member private x.CreateMovementsFromMotions() =
        let processResult opt = 
            match opt with
            | None -> _operations.Beep()
            | Some(data) -> _operations.MoveCaretToMotionData data

        let filterMotionCommand command = 
            match command with
            | SimpleMotionCommand(ki,func) -> 
                let inner count = 
                    let startPoint = TextViewUtil.GetCaretPoint _operations.TextView
                    func startPoint count |> processResult
                Some (ki,SimpleMovementCommand inner)
            | ComplexMotionCommand(ki,false,func) -> None
            | ComplexMotionCommand(ki,true,func) -> 
                
                let coreFunc count = 
                    let rec inner result =  
                        match result with
                        | Complete (data) -> 
                            _operations.MoveCaretToMotionData data
                            MovementComplete
                        | MotionResult.NeedMoreInput (func) -> MovementNeedMore (fun ki -> func ki |> inner)
                        | InvalidMotion (_,func) -> MovementNeedMore (fun ki -> func ki |> inner)
                        | Cancel -> MovementComplete
                        | Error (msg) -> MovementError msg

                    let startPoint = TextViewUtil.GetCaretPoint _operations.TextView
                    let initialResult = func startPoint count
                    inner initialResult
                (ki,ComplexMovementCommand coreFunc) |> Some

        MotionCapture.MotionCommands
        |> Seq.map filterMotionCommand
        |> SeqUtil.filterToSome

    /// Returns the set of commands which move the cursor.  This includes all motions which are 
    /// valid as movements.  Several of these are overriden with custom movement behavior though.
    member x.CreateMovementCommands() = 
        let standard = x.CreateStandardMovementCommandsOld()
        let taken = standard |> Seq.map (fun (x,_) -> x) |> Set.ofSeq
        let motion = 
            x.CreateMovementsFromMotions()
            |> Seq.filter (fun (ki,_) -> not (taken.Contains ki))
        standard |> Seq.append motion

