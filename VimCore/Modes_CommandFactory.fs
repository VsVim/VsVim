#light

namespace Vim.Modes
open Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor


type internal CommandFactory
    ( 
        _operations : ICommonOperations, 
        _capture : IMotionCapture,
        _motionUtil : ITextViewMotionUtil, 
        _jumpList : IJumpList,
        _settings : IVimLocalSettings ) =

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
                func (CommandUtil2.CountOrDefault opt)
                CommandResult.Completed NoSwitch
            Command.SimpleCommand (kiSet,CommandFlags.Movement, funcWithReg))

    /// Build up a set of MotionCommand values from applicable Motion values.  These will 
    /// move the cursor to the result of the motion
    member x.CreateMovementsFromMotions() =
        let processResult opt = 
            match opt with
            | None -> _operations.Beep()
            | Some(data) -> _operations.MoveCaretToMotionResult data
            CommandResult.Completed NoSwitch

        let makeMotionArgument count = { MotionContext = MotionContext.Movement; OperatorCount = None; MotionCount = count }

        let processMotionCommand command =
            match command with
            | SimpleMotionCommand(name, _, motion) -> 

                // Create a function which will proces the simple motion and move the caret
                let inner count _ =  
                    let arg = makeMotionArgument count
                    let data = _motionUtil.GetMotion motion arg
                    processResult data

                Command.SimpleCommand(name,CommandFlags.Movement,inner) 
            | ComplexMotionCommand(name, motionFlags, func) -> 

                // The core function which accepts the initial count.  Will create a MotionArgument
                // from this value and then begin to loop over the MotionBindResult until we get 
                // a final one
                let coreFunc count _ = 

                    let rec inner result =  
                        match result with
                        | MotionBindResult.Complete (motionRunData, precalculatedData) ->
                            let res = 

                                // Calculate the resulting MotionResult.  Use the precalculated cached
                                // data if it's available 
                                let data = 
                                    match precalculatedData with
                                    | Some data -> Some data
                                    | None -> _motionUtil.GetMotion motionRunData.Motion motionRunData.MotionArgument

                                // Move the cursor based on the resulting motion
                                match data with
                                | None -> 
                                    CommandResult.Error Resources.MotionCapture_InvalidMotion
                                | Some(data) -> 
                                    _operations.MoveCaretToMotionResult data
                                    CommandResult.Completed NoSwitch 
                            res |> LongCommandResult.Finished
                        | MotionBindResult.NeedMoreInput (keyRemapMode, func) -> 
                            LongCommandResult.NeedMoreInput (keyRemapMode, fun ki -> func ki |> inner)
                        | MotionBindResult.Cancelled -> 
                            LongCommandResult.Cancelled
                        | MotionBindResult.Error (msg) -> 
                            CommandResult.Error msg |> LongCommandResult.Finished

                    let arg = makeMotionArgument count
                    let result = func arg
                    inner result

                // Create the flags.  Make sure that we set that Escape can be handled if the
                // motion itself can handle escape
                let flags = 
                    if Util.IsFlagSet motionFlags MotionFlags.HandlesEscape then 
                        CommandFlags.Movement ||| CommandFlags.HandlesEscape
                    else
                        CommandFlags.Movement
                Command.LongCommand(name, flags, coreFunc) 

        _capture.MotionCommands
        |> Seq.filter (fun command -> Util.IsFlagSet command.MotionFlags MotionFlags.CursorMovement)
        |> Seq.map processMotionCommand

    /// Create shared edit commands between Normal and Visual Mode.  Returns a sequence of tuples
    /// with the following form
    ///
    ///  name
    ///  CommandFlags 
    ///  Mode to switch to after in normal
    ///  normal function
    ///  Mode to switch to after in visual
    ///  visual span func
    ///  visual block func 
    member x.CreateEditCommandsCore () = 
        (*
        let funcp() =
                    
            (
                "p",
                CommandFlags.Repeatable,
                ModeSwitch.NoSwitch,
                (fun count reg -> 
                    match reg.
                    let point = TextViewUtil.GetCaretPoint _textView |> SnapshotPointUtil.AddOneOrCurrent
                    _operations.PutAtAndMoveCaret point (reg.Value.Value.ApplyCount count) reg.Value.OperationKind)
                    // Wrap in an undo transaction so the caret position will be preserved 
                    // during an undo
                    _operations.WrapEditInUndoTransaction "Put" (fun () ->

                        // If this is a linewise edit then we need to move the caret 
                        // to the point at the end of the 
                        let point = TextViewUtil.GetCaretPoint _textView
                        let point =
                            match reg.Value.OperationKind with 
                            | OperationKind.LineWise -> point |> SnapshotPointUtil.GetContainingLine |> SnapshotLineUtil.GetEndIncludingLineBreak
                            | OperationKind.CharacterWise -> point
                        _operations
                    _operations.PasteAfterCursor reg.StringValue count reg.Value.OperationKind false),
                ModeSwitch.SwitchMode ModeKind.Normal,
                (fun _ _ reg span ->
                    // Move the caret to the start of the span so that it goes back there during an undo
                    TextViewUtil.MoveCaretToPoint _textView span.Start
                    _operations.WrapEditInUndoTransction "Put" (fun () -> 
                        _operations.PasteAfter span.Start reg.StringValue count

                    
                yield (
                    "p", 
                    CommandFlags.Repeatable, 
                    fun count reg -> _operations.PasteAfterCursor reg.StringValue count reg.Value.OperationKind false)
                yield (
                    "P", 
                    CommandFlags.Repeatable, 
                    fun count reg -> _operations.PasteBeforeCursor reg.StringValue countreg.Value.OperationKind false)

        let funcCC () = 
            let getDeleteSpan (range:SnapshotLineRange) =
                if _settings.AutoIndent then 
                    let start = 
                        range.StartLine 
                        |> SnapshotLineUtil.GetPoints 
                        |> Seq.skipWhile SnapshotPointUtil.IsWhitespace
                        |> SeqUtil.tryHeadOnly
                        |> OptionUtil.getOrDefault range.StartLine.End
                    TextViewUtil.MoveCaretToPoint _textView start
                    SnapshotSpanUtil.Create start range.End
                else
                    range.Extent

            (
                "cc",
                CommandFlags.LinkedWithNextTextChange ||| CommandFlags.Repeatable,
                ModeSwitch.SwitchMode ModeKind.Insert,
                (fun count reg ->  
                    let span = TextViewUtil.GetCaretLineRange _textView (CommandUtil2.CountOrDefault count) |> getDeleteSpan
                    _operations.DeleteSpan span 
                    _operations.UpdateRegisterForSpan reg RegisterOperation.Delete span OperationKind.LineWise),
                ModeSwitch.SwitchMode ModeKind.Insert,
                (fun _ _ reg span -> 
                    let span = span |> SnapshotLineRangeUtil.CreateForSpan |> getDeleteSpan 
                    _operations.DeleteSpan span
                    _operations.UpdateRegisterForSpan reg RegisterOperation.Delete span OperationKind.LineWise),
                (fun _ _ reg col -> 
                    _operations.DeleteBlock col 
                    _operations.UpdateRegisterForCollection reg RegisterOperation.Delete col OperationKind.CharacterWise))

        seq {   
            yield funcCC()
        } *)

        Seq.empty

    member x.CreateEditCommandsForNormalMode () = 
        x.CreateEditCommandsCore()
        |> Seq.map (fun (name, flags, normalSwitch, normalFunc, _, _, _) ->
            let keyInputSet = KeyNotationUtil.StringToKeyInputSet name
            let func count reg = 
                normalFunc count reg
                CommandResult.Completed normalSwitch
            SimpleCommand(keyInputSet, flags, func))

    member x.CreateEditCommandsForVisualMode visualKind = 
        x.CreateEditCommandsCore()
        |> Seq.map (fun (name, flags, _, _, visualSwitch, spanFunc, blockFunc) ->
            let keyInputSet = KeyNotationUtil.StringToKeyInputSet name
            let func count reg visualSpan = 
                match visualSpan with
                | VisualSpan.Single (kind, span) ->
                    spanFunc kind count reg span
                    CommandResult.Completed visualSwitch
                | VisualSpan.Multiple (kind, block) ->
                    blockFunc kind count reg block
                    CommandResult.Completed visualSwitch
            VisualCommand(keyInputSet, flags, visualKind, func))

    member x.CreateMovementCommands() = 
        let standard = x.CreateStandardMovementCommands()
        let taken = standard |> Seq.map (fun command -> command.KeyInputSet) |> Set.ofSeq
        let motion = 
            x.CreateMovementsFromMotions()
            |> Seq.filter (fun command -> not (taken.Contains command.KeyInputSet))
        standard |> Seq.append motion

