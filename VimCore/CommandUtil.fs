

namespace Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining

type internal CommandUtil 
    (
        _operations : ICommonOperations,
        _registerMap : IRegisterMap,
        _motionUtil : ITextViewMotionUtil,
        _vimData : IVimData ) = 

    let _textView = _operations.TextView
    let _textBuffer = _operations.TextView.TextBuffer
    let mutable _inRepeatLastChange = false

    /// The SnapshotPoint for the caret
    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    /// The ITextSnapshotLine for the caret
    member x.CaretLine = TextViewUtil.GetCaretLine _textView

    /// The SnapshotPoint and ITextSnapshotLine for the caret
    member x.CaretPointAndLine = TextViewUtil.GetCaretPointAndLine _textView

    /// The current ITextSnapshot instance for the ITextBuffer
    member x.CurrentSnapshot = _textBuffer.CurrentSnapshot

    /// Put the contents of the specified register after the cursor.  Used for the
    /// normal / visual 'p' command
    member x.PutAfterCursor (register : Register) count =
        let point = SnapshotPointUtil.AddOneOrCurrent x.CaretPoint
        let stringData = register.StringData.ApplyCount count
        _operations.PutAt point stringData register.OperationKind
        Command2Result.Completed

    /// Calculate the VisualSpan value for the associated ITextBuffer given the 
    /// StoreVisualSpan value
    member x.CalculateVisualSpan stored =

        // REPEAT TODO: Actually implement
        let span = SnapshotSpan(x.CurrentSnapshot, 0, 0)
        let span = VisualSpan.Single (VisualKind.Character, span)
        match stored with
        | StoredVisualSpan.Linewise _ -> span
        | StoredVisualSpan.Characterwise _ -> span
        | StoredVisualSpan.Block _-> span

    /// Repeat the last executed command against the current buffer
    member x.RepeatLastCommand (repeatData : CommandData) = 

        // Function to actually repeat the last change 
        let rec repeat (command : StoredCommand) = 

            /// Repeat a text change.  
            let repeatTextChange change = 
                match change with 
                | TextChange.Insert text -> 
                    _operations.InsertText text repeatData.CountOrDefault
                | TextChange.Delete(count) -> 
                    let caretPoint, caretLine = x.CaretPointAndLine
                    let length = min count (caretLine.EndIncludingLineBreak.Position - caretPoint.Position)
                    let span = SnapshotSpanUtil.CreateWithLength caretPoint length
                    _operations.DeleteSpan span
                Command2Result.Completed

            match command with
            | StoredCommand.NormalCommand (command, data) ->
                x.RunNormalCommand command data
            | StoredCommand.VisualCommand (command, data, storedVisualSpan) -> 
                let visualSpan = x.CalculateVisualSpan storedVisualSpan
                x.RunVisualCommand command data visualSpan
            | StoredCommand.TextChangeCommand change ->
                repeatTextChange change
            | StoredCommand.LinkedCommand (command1, command2) -> 

                // Run the commands in sequence.  Only continue onto the second if the first 
                // command succeeds
                match repeat command1 with
                | Command2Result.Error msg -> Command2Result.Error msg
                | Command2Result.Completed -> repeat command2

        if _inRepeatLastChange then
            Command2Result.Error Resources.NormalMode_RecursiveRepeatDetected
        else
            try
                _inRepeatLastChange <- true
                match _vimData.LastCommand with
                | None -> 
                    _operations.Beep()
                    Command2Result.Completed
                | Some command ->
                    repeat command
            finally
                _inRepeatLastChange <- false

    /// Yank the contents of the motion into the specified register
    member x.YankMotion register (result: MotionResult) = 
        _operations.UpdateRegisterForSpan register RegisterOperation.Yank result.OperationSpan result.OperationKind
        Command2Result.Completed

    /// Get the MotionResult value for the provided MotionData and pass it
    /// if found to the provided function
    member x.RunWithMotion (motion : MotionData) func = 
        match _motionUtil.GetMotion motion.Motion motion.MotionArgument with
        | None -> Command2Result.Error Resources.MotionCapture_InvalidMotion
        | Some data -> func data

    /// Run a NormalCommand against the buffer
    member x.RunNormalCommand command (data : CommandData) : Command2Result = 
        let register = _registerMap.GetRegister data.RegisterNameOrDefault
        let count = data.CountOrDefault
        match command with
        | NormalCommand.PutAfterCursor -> x.PutAfterCursor register count
        | NormalCommand.RepeatLastCommand -> x.RepeatLastCommand data
        | NormalCommand.Yank motion -> x.RunWithMotion motion (x.YankMotion register)

    /// Run a VisualCommand against the buffer
    member x.RunVisualCommand command (data : CommandData) (visualSpan : VisualSpan) = 
        let register = _registerMap.GetRegister data.RegisterNameOrDefault
        let count = data.CountOrDefault
        match command with
        | VisualCommand.PutAfterCursor -> x.PutAfterCursor register count

    interface ICommandUtil with
        member x.RunNormalCommand command data = x.RunNormalCommand command data
        member x.RunVisualCommand command data visualSpan = x.RunVisualCommand command data visualSpan 

