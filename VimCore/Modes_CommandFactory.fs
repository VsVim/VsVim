#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal MovementCommand = int -> unit

type internal CommandFactory( _operations : ICommonOperations) = 

    member private x.CreateMovementsFromSimpleMotions() =
        let processResult opt = 
            match opt with
            | None -> _operations.Beep()
            | Some(data) -> _operations.MoveCaretToMotionData data

        let filterToSimple command =    
            match command with
            | SimpleMotionCommand(ki,func) -> 
                let startPoint = TextViewUtil.GetCaretPoint _operations.TextView
                let func2 count = func startPoint count |> processResult
                Some (ki,func2)
            | ComplexMotionCommand(_) -> None

        MotionCapture.MotionCommands
        |> Seq.map filterToSimple
        |> SeqUtil.filterToSome


    /// The sequence of commands which move the cursor.  Applicable in both Normal and Visual Mode
    member x.CreateMovementCommands() = x.CreateMovementsFromSimpleMotions()
