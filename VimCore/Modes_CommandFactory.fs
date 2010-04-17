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

    /// The sequence of commands which move the cursor.  Applicable in both Normal and Visual Mode
    member x.CreateMovementCommands() = x.CreateMovementsFromMotions()
