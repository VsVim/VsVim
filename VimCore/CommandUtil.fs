

namespace Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining

type internal CommandUtil 
    (
        _textView : ITextView,
        _operations : ICommonOperations,
        _motionUtil : ITextViewMotionUtil,
        _statusUtil : IStatusUtil,
        _registerMap : IRegisterMap,
        _markMap : IMarkMap,
        _vimData : IVimData
    ) =

    let _textBuffer = _textView.TextBuffer
    let mutable _inRepeatLastChange = false

    /// The SnapshotPoint for the caret
    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    /// The ITextSnapshotLine for the caret
    member x.CaretLine = TextViewUtil.GetCaretLine _textView

    /// The SnapshotPoint and ITextSnapshotLine for the caret
    member x.CaretPointAndLine = TextViewUtil.GetCaretPointAndLine _textView

    /// The current ITextSnapshot instance for the ITextBuffer
    member x.CurrentSnapshot = _textBuffer.CurrentSnapshot

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

    /// Run the specified action with a wrapped undo transaction.  This is often necessary when
    /// an edit command manipulates the caret
    member x.EditWithUndoTransaciton name action = _operations.WrapEditInUndoTransaction name action

    /// Jump to the specified mark
    member x.JumpToMark c =
        match _operations.JumpToMark c _markMap with
        | Modes.Result.Failed msg ->
            _statusUtil.OnError msg
            CommandResult.Error
        | Modes.Result.Succeeded ->
            CommandResult.Completed ModeSwitch.NoSwitch

    /// Move the caret to the result of the motion
    member x.MoveCaretToMotion motion count = 
        let argument = { MotionContext = MotionContext.Movement; OperatorCount = None; MotionCount = count}
        match _motionUtil.GetMotion motion argument with
        | None -> _operations.Beep()
        | Some result -> _operations.MoveCaretToMotionResult result
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Run the Ping command
    member x.Ping func data = 
        func data
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Put the contents of the specified register after the cursor.  Used for the
    /// normal / visual 'p' command
    member x.PutAfterCursor (register : Register) count switch =
        let point = SnapshotPointUtil.AddOneOrCurrent x.CaretPoint
        let stringData = register.StringData.ApplyCount count
        _operations.PutAt point stringData register.OperationKind
        CommandResult.Completed switch

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
                CommandResult.Completed ModeSwitch.NoSwitch

            match command with
            | StoredCommand.NormalCommand (command, data, _) ->
                x.RunNormalCommand command data
            | StoredCommand.VisualCommand (command, data, storedVisualSpan, _) -> 
                let visualSpan = x.CalculateVisualSpan storedVisualSpan
                x.RunVisualCommand command data visualSpan
            | StoredCommand.TextChangeCommand change ->
                repeatTextChange change
            | StoredCommand.LinkedCommand (command1, command2) -> 

                // Run the commands in sequence.  Only continue onto the second if the first 
                // command succeeds
                match repeat command1 with
                | CommandResult.Error -> CommandResult.Error
                | CommandResult.Completed _ -> repeat command2

        if _inRepeatLastChange then
            _statusUtil.OnError Resources.NormalMode_RecursiveRepeatDetected
            CommandResult.Error 
        else
            try
                _inRepeatLastChange <- true
                match _vimData.LastCommand with
                | None -> 
                    _operations.Beep()
                    CommandResult.Completed ModeSwitch.NoSwitch
                | Some command ->
                    repeat command
            finally
                _inRepeatLastChange <- false

    /// Replace the char under the cursor with the specified character
    member x.ReplaceChar keyInput count = 

        let succeeded = 
            let point = x.CaretPoint
            if (point.Position + count) > point.GetContainingLine().End.Position then
                // If the replace operation exceeds the line length then the operation
                // can't succeed
                false
            else
                // Do the replace in an undo transaction since we are explicitly positioning
                // the caret
                x.EditWithUndoTransaciton "ReplaceChar" (fun () -> 

                    let replaceText = 
                        if keyInput = KeyInputUtil.EnterKey then System.Environment.NewLine
                        else new System.String(keyInput.Char, count)
                    let span = new Span(point.Position, count)
                    let snapshot = _textView.TextBuffer.Replace(span, replaceText) 

                    // The caret should move to the end of the replace operation which is 
                    // 'count - 1' characters from the original position 
                    let point = SnapshotPoint(snapshot, point.Position + (count - 1))

                    _textView.Caret.MoveTo(point) |> ignore)
                true

        // If the replace failed then we should beep the console
        if not succeeded then
            _operations.Beep()

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Run the specified Command
    member x.RunCommand command = 
        match command with
        | Command.NormalCommand (command, data) -> x.RunNormalCommand command data
        | Command.VisualCommand (command, data, visualSpan) -> x.RunVisualCommand command data visualSpan
        | Command.LegacyCommand func -> func()

    /// Run a NormalCommand against the buffer
    member x.RunNormalCommand command (data : CommandData) =
        let register = _registerMap.GetRegister data.RegisterNameOrDefault
        let count = data.CountOrDefault
        match command with
        | NormalCommand.MoveCaretToMotion motion -> x.MoveCaretToMotion motion data.Count
        | NormalCommand.JumpToMark c -> x.JumpToMark c
        | NormalCommand.Ping func -> x.Ping func data
        | NormalCommand.PutAfterCursor -> x.PutAfterCursor register count ModeSwitch.NoSwitch
        | NormalCommand.SetMarkToCaret c -> x.SetMarkToCaret c
        | NormalCommand.RepeatLastCommand -> x.RepeatLastCommand data
        | NormalCommand.ReplaceChar keyInput -> x.ReplaceChar keyInput data.CountOrDefault
        | NormalCommand.Yank motion -> x.RunWithMotion motion (x.YankMotion register)

    /// Run a VisualCommand against the buffer
    member x.RunVisualCommand command (data : CommandData) (visualSpan : VisualSpan) = 
        let register = _registerMap.GetRegister data.RegisterNameOrDefault
        let count = data.CountOrDefault
        match command with
        | VisualCommand.PutAfterCursor -> x.PutAfterCursor register count (ModeSwitch.SwitchMode ModeKind.Normal)

    /// Get the MotionResult value for the provided MotionData and pass it
    /// if found to the provided function
    member x.RunWithMotion (motion : MotionData) func = 
        match _motionUtil.GetMotion motion.Motion motion.MotionArgument with
        | None ->  
            _statusUtil.OnError Resources.MotionCapture_InvalidMotion
            CommandResult.Error
        | Some data -> func data

    /// Process the m[a-z] command
    member x.SetMarkToCaret c = 
        let caretPoint = TextViewUtil.GetCaretPoint _textView
        match _operations.SetMark caretPoint c _markMap with
        | Modes.Result.Failed msg ->
            _statusUtil.OnError msg
            CommandResult.Error
        | Modes.Result.Succeeded ->
            CommandResult.Completed ModeSwitch.NoSwitch

    /// Yank the contents of the motion into the specified register
    member x.YankMotion register (result: MotionResult) = 
        _operations.UpdateRegisterForSpan register RegisterOperation.Yank result.OperationSpan result.OperationKind
        CommandResult.Completed ModeSwitch.NoSwitch

    interface ICommandUtil with
        member x.RunNormalCommand command data = x.RunNormalCommand command data
        member x.RunVisualCommand command data visualSpan = x.RunVisualCommand command data visualSpan 
        member x.RunCommand command = x.RunCommand command

