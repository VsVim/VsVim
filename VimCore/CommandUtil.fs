

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

    /// The column of the caret
    member x.CaretColumn = SnapshotPointUtil.GetColumn x.CaretPoint

    /// The SnapshotPoint for the caret
    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    /// The ITextSnapshotLine for the caret
    member x.CaretLine = TextViewUtil.GetCaretLine _textView

    /// The line number for the caret
    member x.CaretLineNumber = x.CaretLine.LineNumber

    /// The SnapshotPoint and ITextSnapshotLine for the caret
    member x.CaretPointAndLine = TextViewUtil.GetCaretPointAndLine _textView

    /// The current ITextSnapshot instance for the ITextBuffer
    member x.CurrentSnapshot = _textBuffer.CurrentSnapshot

    /// Calculate the VisualSpan value for the associated ITextBuffer given the 
    /// StoreVisualSpan value
    member x.CalculateVisualSpan stored =

        match stored with
        | StoredVisualSpan.Line count -> 
            // Repeating a Linewise operation just creates a span with the same 
            // number of lines as the original operation
            let range = SnapshotLineRangeUtil.CreateForLineAndMaxCount x.CaretLine count
            VisualSpan.Line range

        | StoredVisualSpan.Character (endLineOffset, endOffset) -> 
            // Repeating a characterise span starts from the caret position.  There
            // are 2 cases to consider
            //
            //  1. Single Line: endOffset is the offset from the caret
            //  2. Multi Line: endOffset is the offset from the last line

            let offsetOrEnd (line : ITextSnapshotLine) offset = 
                if offset >= line.Length then line.End
                else line.Start.Add(offset)

            let startPoint = x.CaretPoint

            /// Calculate the end point being careful not to go past the end of the buffer
            let endPoint = 
                if 0 = endLineOffset then
                    let column = SnapshotPointUtil.GetColumn x.CaretPoint
                    offsetOrEnd x.CaretLine (column + endOffset)
                else
                    let endLineNumber = x.CaretLine.LineNumber + endLineOffset
                    match SnapshotUtil.TryGetLine x.CurrentSnapshot endLineNumber with
                    | None -> SnapshotUtil.GetEndPoint x.CurrentSnapshot
                    | Some endLine -> offsetOrEnd endLine endOffset

            let span = SnapshotSpan(startPoint, endPoint)
            VisualSpan.Character span
        | StoredVisualSpan.Block (length, count) ->
            // Need to rehydrate spans of length 'length' on 'count' lines from the 
            // current caret position
            let column = x.CaretColumn
            let col = 
                SnapshotUtil.GetLines x.CurrentSnapshot x.CaretLineNumber SearchKind.Forward
                |> Seq.truncate count
                |> Seq.map (fun line ->
                    let startPoint = 
                        if column >= line.Length then line.End 
                        else line.Start.Add(column)
                    let endPoint = 
                        if startPoint.Position + length >= line.End.Position then line.End 
                        else startPoint.Add(length)
                    SnapshotSpan(startPoint, endPoint))
                |> NormalizedSnapshotSpanCollectionUtil.OfSeq
            VisualSpan.Block col

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

            // Calculate the new CommandData based on the old and 
            // current CommandData values
            let getCommandData (oldData : CommandData) = 
                match repeatData.Count with
                | Some count -> { oldData with Count = repeatData.Count }
                | None -> oldData

            // Repeat a text change.  
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
                let data = getCommandData data
                x.RunNormalCommand command data
            | StoredCommand.VisualCommand (command, data, storedVisualSpan, _) -> 
                let data = getCommandData data
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
                _operations.Beep()
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

    /// Replace the char under the cursor in visual mode.
    member x.ReplaceCharVisual keyInput (visualSpan : VisualSpan) = 

        let replaceText = 
            if keyInput = KeyInputUtil.EnterKey then System.Environment.NewLine
            else System.String(keyInput.Char, 1)

        // First step is we want to update the selection.  A replace char operation
        // in visual mode should position the caret on the first character and clear
        // the selection (both before and after).
        //
        // The caret can be anywhere at the start of the operation so move it to the
        // first point before even begining the edit transaction
        _textView.Selection.Clear()
        let points = 
            visualSpan.Spans
            |> Seq.map SnapshotSpanUtil.GetPoints
            |> Seq.concat
        let editPoint = 
            match points |> SeqUtil.tryHeadOnly with
            | Some point -> point
            | None -> x.CaretPoint
        TextViewUtil.MoveCaretToPoint _textView editPoint

        x.EditWithUndoTransaciton "ReplaceChar" (fun () -> 
            use edit = _textBuffer.CreateEdit()
            points |> Seq.iter (fun point -> edit.Replace((Span(point.Position, 1)), replaceText) |> ignore)
            let snapshot = edit.Apply()

            // Reposition the caret at the start of the edit
            let editPoint = SnapshotPoint(snapshot, editPoint.Position)
            TextViewUtil.MoveCaretToPoint _textView editPoint)

        CommandResult.Completed (ModeSwitch.SwitchMode ModeKind.Normal)

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
        | VisualCommand.ReplaceChar keyInput -> x.ReplaceCharVisual keyInput visualSpan

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
            _operations.Beep()
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

