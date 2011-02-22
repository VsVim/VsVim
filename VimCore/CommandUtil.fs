

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
        _vimData : IVimData,
        _localSettings : IVimLocalSettings
    ) =

    let _globalSettings = _localSettings.GlobalSettings
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
            let startPoint = x.CaretPoint

            /// Calculate the end point being careful not to go past the end of the buffer
            let endPoint = 
                if 0 = endLineOffset then
                    let column = SnapshotPointUtil.GetColumn x.CaretPoint
                    SnapshotLineUtil.GetOffsetOrEnd x.CaretLine (column + endOffset)
                else
                    let endLineNumber = x.CaretLine.LineNumber + endLineOffset
                    match SnapshotUtil.TryGetLine x.CurrentSnapshot endLineNumber with
                    | None -> SnapshotUtil.GetEndPoint x.CurrentSnapshot
                    | Some endLine -> SnapshotLineUtil.GetOffsetOrEnd endLine endOffset

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

    /// Change the characters in the given span via the specied change kind
    member x.ChangeCaseSpanCore kind (editSpan : EditSpan) =

        let func = 
            match kind with
            | ChangeCharacterKind.Rot13 -> CharUtil.ChangeRot13
            | ChangeCharacterKind.ToLowerCase -> CharUtil.ToLower
            | ChangeCharacterKind.ToUpperCase -> CharUtil.ToUpper
            | ChangeCharacterKind.ToggleCase -> CharUtil.ChangeCase

        use edit = _textBuffer.CreateEdit()
        editSpan.Spans
        |> Seq.map SnapshotSpanUtil.GetPoints
        |> Seq.concat
        |> Seq.filter (fun p -> CharUtil.IsLetter (p.GetChar()))
        |> Seq.iter (fun p ->
            let change = func (p.GetChar()) |> StringUtil.ofChar
            edit.Replace(p.Position, 1, change) |> ignore)
        edit.Apply() |> ignore

    /// Change the caret line via the specied ChangeCharacterKind.
    member x.ChangeCaseCaretLine kind =

        // The caret should be positioned on the first non-blank space in 
        // the line.  If the line is completely blank the caret should
        // not be moved.  Caret should be in the same place for undo / redo
        // so move before and inside the transaction
        let position = 
            x.CaretLine
            |> SnapshotLineUtil.GetPoints
            |> Seq.skipWhile SnapshotPointUtil.IsWhiteSpace
            |> Seq.map SnapshotPointUtil.GetPosition
            |> SeqUtil.tryHeadOnly

        let maybeMoveCaret () =
            match position with
            | Some position -> TextViewUtil.MoveCaretToPosition _textView position
            | None -> ()

        maybeMoveCaret()
        x.EditWithUndoTransaciton "Change" (fun () ->
            x.ChangeCaseSpanCore kind (EditSpan.Single x.CaretLine.Extent)
            maybeMoveCaret())

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Change the case of the specified motion
    member x.ChangeCaseMotion kind (result : MotionResult) =

        // The caret should be placed at the start of the motion for both
        // undo / redo so move before and inside the transaction
        TextViewUtil.MoveCaretToPoint _textView result.Span.Start
        x.EditWithUndoTransaciton "Change" (fun () ->
            x.ChangeCaseSpanCore kind result.EditSpan
            TextViewUtil.MoveCaretToPosition _textView result.Span.Start.Position)

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Change the case of the current caret point
    member x.ChangeCaseCaretPoint kind count =

        // The caret should be placed after the caret point but only 
        // for redo.  Undo should move back to the current position so 
        // don't move until inside the transaction
        x.EditWithUndoTransaciton "Change" (fun () ->

            let span = 
                let endPoint = SnapshotLineUtil.GetOffsetOrEnd x.CaretLine (x.CaretColumn + count)
                SnapshotSpan(x.CaretPoint, endPoint)

            let editSpan = EditSpan.Single span
            x.ChangeCaseSpanCore kind editSpan

            // Move the caret but make sure to respect the 'virtualedit' option
            TextViewUtil.MoveCaretToPosition _textView span.End.Position
            _operations.MoveCaretForVirtualEdit())

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Change the case of the selected text.  
    member x.ChangeCaseVisual kind (visualSpan : VisualSpan) = 

        // The caret should be positioned at the start of the VisualSpan for both 
        // undo / redo so move it before and inside the transaction
        let point = visualSpan.Start |> OptionUtil.getOrDefault x.CaretPoint
        let moveCaret () = TextViewUtil.MoveCaretToPosition _textView point.Position
        moveCaret()
        x.EditWithUndoTransaciton "Change" (fun () ->
            x.ChangeCaseSpanCore kind visualSpan.EditSpan
            moveCaret())

        CommandResult.Completed ModeSwitch.SwitchPreviousMode

    /// Delete the specified motion and enter insert mode
    member x.ChangeMotion register (result : MotionResult) = 

        // This command has legacy / special case behavior for forward word motions.  It will 
        // not delete any trailing whitespace in the span if the motion is created for a forward 
        // word motion. This behavior is detailed in the :help WORD section of the gVim 
        // documentation and is likely legacy behavior coming from the original vi 
        // implementation.  A larger discussion thread is available here
        // http://groups.google.com/group/vim_use/browse_thread/thread/88b6499bbcb0878d/561dfe13d3f2ef63?lnk=gst&q=whitespace+cw#561dfe13d3f2ef63

        let span = 
            if result.IsAnyWordMotion && result.IsForward then
                let point = 
                    result.OperationSpan
                    |> SnapshotSpanUtil.GetPointsBackward 
                    |> Seq.tryFind (fun x -> x.GetChar() |> CharUtil.IsWhiteSpace |> not)
                match point with 
                | Some(p) -> 
                    let endPoint = 
                        p
                        |> SnapshotPointUtil.TryAddOne 
                        |> OptionUtil.getOrDefault (SnapshotUtil.GetEndPoint (p.Snapshot))
                    SnapshotSpan(result.OperationSpan.Start, endPoint)
                | None -> result.OperationSpan 
            else
                result.OperationSpan

        // Use an undo transaction to preserve the caret position.  It should be at the start
        // of the span being deleted before and after the undo / redo so move it before and 
        // after the delete occurs
        TextViewUtil.MoveCaretToPoint _textView span.Start
        x.EditWithUndoTransaciton "Change" (fun () ->
            _textBuffer.Delete(span.Span) |> ignore
            TextViewUtil.MoveCaretToPosition _textView span.Start.Position)

        // Now that the delete is complete update the register
        let value = { Value = StringData.OfSpan span; OperationKind = result.OperationKind }
        _registerMap.SetRegisterValue register RegisterOperation.Delete value

        CommandResult.Completed (ModeSwitch.SwitchMode ModeKind.Insert)

    /// Delete 'count' lines and begin insert mode.  The documentation of this command 
    /// and behavior are a bit off.  It's documented like it behaves lke 'dd + insert mode' 
    /// but behaves more like ChangeTillEndOfLine but linewise and deletes the entire
    /// first line
    member x.ChangeLines count register = 

        let range = SnapshotLineRangeUtil.CreateForLineAndMaxCount x.CaretLine count
        let lineNumber = x.CaretLine.LineNumber

        // Caret position before the delete needs to be on the first non-whitespace character
        // of the first line.  If the line is blank the caret should remain un-moved
        let point =
            x.CaretLine
            |> SnapshotLineUtil.GetPoints
            |> Seq.skipWhile SnapshotPointUtil.IsWhiteSpace
            |> SeqUtil.tryHeadOnly
        match point with
        | None -> ()
        | Some point -> TextViewUtil.MoveCaretToPoint _textView point

        // Start an edit transaction to get the appropriate undo / redo behavior for the 
        // caret movement after the edit.
        x.EditWithUndoTransaciton "ChangeLines" (fun () -> 
            let snapshot = _textBuffer.Delete(range.Extent.Span)
            let line = SnapshotUtil.GetLine snapshot lineNumber

            if _localSettings.AutoIndent then
                // If auto-indent is on then we preserve the original indent.  The line is empty
                // right now so put the caret into virtual space
                match point with
                | Some point ->
                    let point = VirtualSnapshotPoint(line.Start, SnapshotPointUtil.GetColumn point)
                    TextViewUtil.MoveCaretToVirtualPoint _textView point
                | None -> 
                    TextViewUtil.MoveCaretToPoint _textView line.Start
            else
                // Put the caret at column 0
                TextViewUtil.MoveCaretToPoint _textView line.Start)

        // Update the register now that the operation is complete.  Register value is odd here
        // because we really didn't delete linewise but it's required to be a linewise 
        // operation.  
        let value = range.Extent.GetText() + System.Environment.NewLine
        let value = { Value = StringData.Simple value; OperationKind = OperationKind.LineWise }
        _registerMap.SetRegisterValue register RegisterOperation.Delete value

        CommandResult.Completed (ModeSwitch.SwitchMode ModeKind.Insert)

    /// Delete till the end of the line and start insert mode
    member x.ChangeTillEndOfLine count register =

        // Exact same operation as DeleteTillEndOfLine except we end by switching to 
        // insert mode
        x.DeleteTillEndOfLine count register |> ignore

        CommandResult.Completed (ModeSwitch.SwitchMode ModeKind.Insert)

    /// Delete 'count' characters after the cursor on the current line.  Caret should 
    /// remain at it's original position 
    member x.DeleteCharacterAtCaret count register =

        // Check for the case where the caret is past the end of the line.  Can happen
        // when 've=onemore'
        if x.CaretPoint.Position < x.CaretLine.End.Position then
            let endPoint = SnapshotLineUtil.GetOffsetOrEnd x.CaretLine (x.CaretColumn + count)
            let span = SnapshotSpan(x.CaretPoint, endPoint)

            // Use a transaction so we can guarantee the caret is in the correct
            // position on undo / redo
            x.EditWithUndoTransaciton "DeleteChar" (fun () -> 
                let position = x.CaretPoint.Position
                let snapshot = _textBuffer.Delete(span.Span)
                TextViewUtil.MoveCaretToPoint _textView (SnapshotPoint(snapshot, position))

                // Need to respect the virtual edit setting here as we could have 
                // deleted the last character on the line
                _operations.MoveCaretForVirtualEdit())

            // Put the deleted text into the specified register
            let value = { Value = StringData.OfSpan span; OperationKind = OperationKind.CharacterWise }
            _registerMap.SetRegisterValue register RegisterOperation.Delete value

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Delete 'count' characters before the cursor on the current line.  Caret should be
    /// positioned at the begining of the span for undo / redo
    member x.DeleteCharacterBeforeCaret count register = 

        let startPoint = 
            let position = x.CaretPoint.Position - count
            if position < x.CaretLine.Start.Position then x.CaretLine.Start else SnapshotPoint(x.CurrentSnapshot, position)
        let span = SnapshotSpan(startPoint, x.CaretPoint)

        // Use a transaction so we can guarantee the caret is in the correct position.  We 
        // need to position the caret to the start of the span before the transaction to 
        // ensure it appears there during an undo
        TextViewUtil.MoveCaretToPoint _textView startPoint
        x.EditWithUndoTransaciton "DeleteChar" (fun () ->
            let snapshot = _textBuffer.Delete(span.Span)
            TextViewUtil.MoveCaretToPosition _textView startPoint.Position)

        // Put the deleted text into the specified register once the delete completes
        let value = { Value = StringData.OfSpan span; OperationKind = OperationKind.CharacterWise }
        _registerMap.SetRegisterValue register RegisterOperation.Delete value

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Delete the highlighted text from the buffer and put it into the specified 
    /// register.  The caret should be positioned at the begining of the text for
    /// undo / redo
    member x.DeleteSelectedText register (visualSpan : VisualSpan) = 
        let startPoint = visualSpan.Start |> OptionUtil.getOrDefault x.CaretPoint 

        // Use a transaction to guarantee caret position.  Caret should be at the start
        // during undo and redo so move it before the edit
        TextViewUtil.MoveCaretToPoint _textView startPoint
        x.EditWithUndoTransaciton "DeleteHighlightedText" (fun () ->
            use edit = _textBuffer.CreateEdit()
            visualSpan.Spans |> Seq.iter (fun span -> 

                // If the span ends partially in a LineBreak extent put it fully across
                // the extent
                let span = 
                    let line = span.End.GetContainingLine()
                    let extent = SnapshotLineUtil.GetLineBreakSpan line
                    if extent.Contains span.End then SnapshotSpan(span.Start, line.EndIncludingLineBreak)
                    else span

                edit.Delete(span.Span) |> ignore)
            let snapshot = edit.Apply()
            TextViewUtil.MoveCaretToPosition _textView startPoint.Position)

        CommandResult.Completed ModeSwitch.SwitchPreviousMode

    /// Delete count lines from the cursor.  The caret should be positioned at the start
    /// of the first line for both undo / redo
    member x.DeleteLines count register = 
        let line = x.CaretLine
        let span, stringData = 
            if line.LineNumber = SnapshotUtil.GetLastLineNumber x.CurrentSnapshot && x.CurrentSnapshot.LineCount > 1 then
                // The last line is an unfortunate special case here as it does not have a line break.  Hence 
                // in order to delete the line we must delete the line break at the end of the preceeding line.  
                //
                // This cannot be normalized by always deleting the line break from the previous line because
                // it would still break for the first line.  This is an unfortunate special case we must 
                // deal with
                let above = SnapshotUtil.GetLine x.CurrentSnapshot (line.LineNumber - 1)
                let span = SnapshotSpan(above.End, line.EndIncludingLineBreak)
                let data = StringData.Simple (line.GetText() + System.Environment.NewLine)
                (span, data)
            else 
                // Simpler case.  Get the line range and delete
                let range = SnapshotLineRangeUtil.CreateForLineAndMaxCount x.CaretLine count
                (range.ExtentIncludingLineBreak, StringData.OfSpan range.ExtentIncludingLineBreak)

        // Use a transaction to properly position the caret for undo / redo.  We want it in the same
        // place for undo / redo so move it before the transaction
        TextViewUtil.MoveCaretToPoint _textView span.Start
        x.EditWithUndoTransaciton "DeleteLines" (fun() -> 
            let snapshot = _textBuffer.Delete(span.Span)
            TextViewUtil.MoveCaretToPoint _textView (SnapshotPoint(snapshot, span.Start.Position)))

        // Now update the register after the delete completes
        let value = { Value = stringData; OperationKind = OperationKind.LineWise }
        _registerMap.SetRegisterValue register RegisterOperation.Delete value

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Delete the specified motion of text
    member x.DeleteMotion register (result : MotionResult) = 

        // The d{motion} command has an exception listed which is visible by typing ':help d' in 
        // gVim.  In summary, if the motion is characterwise, begins and ends on different
        // lines and the start is preceeding by only whitespace and the end is followed
        // only by whitespace then it becomes a linewise motion for those lines.  However experimentation
        // shows that this does not appear to be the case.  For example type the following out 
        // where ^ is the caret
        //
        //  ^abc
        //   def
        //    
        //
        // Then try 'd/    '.  It will not delete the final line even though this meets all of
        // the requirements.  Choosing to ignore this exception for now until I can find
        // a better example


        // Caret should be placed at the start of the motion for both undo / redo so place it 
        // before starting the transaction
        let span = result.OperationSpan
        TextViewUtil.MoveCaretToPoint _textView span.Start
        x.EditWithUndoTransaciton "Delete" (fun () ->
            _textBuffer.Delete(span.Span) |> ignore
            TextViewUtil.MoveCaretToPosition _textView span.Start.Position)

        // Update the register with the result
        let value = { Value = StringData.OfSpan span; OperationKind = result.OperationKind }
        _registerMap.SetRegisterValue register RegisterOperation.Delete value

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Delete from the cursor to the end of the line and then 'count - 1' more lines into
    /// the buffer.
    member x.DeleteTillEndOfLine count register =
        let span = 
            if count = 1 then
                // Just deleting till the end of 
                SnapshotSpan(x.CaretPoint, x.CaretLine.End)
            else
                // Grab a SnapshotLineRange for the 'count - 1' lines and combine in with
                // the caret start to get the span
                let range = SnapshotLineRangeUtil.CreateForLineAndMaxCount x.CaretLine count
                SnapshotSpan(x.CaretPoint, range.End)

        // The caret is already at the start of the Span and it needs to be after the 
        // delete so wrap it in an undo transaction
        x.EditWithUndoTransaciton "Delete" (fun () -> 
            _textBuffer.Delete(span.Span) |> ignore
            TextViewUtil.MoveCaretToPosition _textView span.Start.Position)

        // Delete is complete so update the register.  Strangely enough this is a characterwise
        // operation even though it involves line deletion
        let value = { Value = StringData.OfSpan span; OperationKind = OperationKind.CharacterWise }
        _registerMap.SetRegisterValue register RegisterOperation.Delete value

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Format the 'count' lines in the buffer
    member x.FormatLines count =
        let range = SnapshotLineRangeUtil.CreateForLineAndMaxCount x.CaretLine count
        _operations.FormatLines range
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Format the selected lines
    member x.FormatLinesVisual (visualSpan: VisualSpan) =

        // Use a transaction so the formats occur as a single operation
        x.EditWithUndoTransaciton "Format" (fun () ->
            visualSpan.Spans
            |> Seq.map SnapshotLineRangeUtil.CreateForSpan
            |> Seq.iter _operations.FormatLines)

        CommandResult.Completed ModeSwitch.SwitchPreviousMode

    /// Format the lines in the Motion 
    member x.FormatMotion (result : MotionResult) = 
        _operations.FormatLines result.LineRange
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Run the specified action with a wrapped undo transaction.  This is often necessary when
    /// an edit command manipulates the caret
    member x.EditWithUndoTransaciton name action = _operations.WrapEditInUndoTransaction name action

    /// Join 'count' lines in the buffer
    member x.JoinLines kind count = 

        // An oddity of the join command is that the count 1 and 2 have the same effect.  Easiest
        // to treat both values as 2 since the math works out for all other values above 2
        let count = if count = 1 then 2 else count

        match SnapshotLineRangeUtil.CreateForLineAndCount x.CaretLine count with
        | None -> 
            // If the count exceeds the length of the buffer then the operation should not 
            // complete and a beep should be issued
            _operations.Beep()
        | Some range -> 
            // The caret should be moved after the original line.  It should have it's original
            // position during an undo though so don't move the caret until inside the transaciton
            let position = x.CaretLine.End.Position
            x.EditWithUndoTransaciton "Join" (fun () -> 
                _operations.Join range kind
                TextViewUtil.MoveCaretToPosition _textView position)

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Jump to the specified mark
    member x.JumpToMark c =
        match _operations.JumpToMark c _markMap with
        | Modes.Result.Failed msg ->
            _statusUtil.OnError msg
            CommandResult.Error
        | Modes.Result.Succeeded ->
            CommandResult.Completed ModeSwitch.NoSwitch

    /// Switch to Insert mode with the specified count
    member x.Insert count =
        CommandResult.Completed (ModeSwitch.SwitchModeWithArgument (ModeKind.Insert, ModeArgument.InsertWithCount count))

    /// Begin insert mode on the first non-blank character of the line.  Pass the count onto
    /// insert mode so it can duplicate the input
    member x.InsertAtFirstNonBlank count =
        let point = 
            x.CaretLine
            |> SnapshotLineUtil.GetPoints
            |> Seq.skipWhile SnapshotPointUtil.IsWhiteSpace
            |> SeqUtil.tryHeadOnly
            |> OptionUtil.getOrDefault x.CaretLine.End
        TextViewUtil.MoveCaretToPoint _textView point

        let switch = ModeSwitch.SwitchModeWithArgument (ModeKind.Insert, ModeArgument.InsertWithCount count)
        CommandResult.Completed switch

    /// Move the caret to the result of the motion
    member x.MoveCaretToMotion motion count = 
        let argument = { MotionContext = MotionContext.Movement; OperatorCount = None; MotionCount = count}
        match _motionUtil.GetMotion motion argument with
        | None -> _operations.Beep()
        | Some result -> _operations.MoveCaretToMotionResult result
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Run the Ping command
    member x.Ping (pingData : PingData) data = 
        pingData.Function data
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Put the contents of the specified register after the cursor.  Used for the
    /// normal 'p' and 'gp' command
    member x.PutAfterCaret (register : Register) count moveCaretAfterText =
        let point = SnapshotPointUtil.AddOneOrCurrent x.CaretPoint
        let stringData = register.StringData.ApplyCount count

        // PutAtCaret is one of the few ICommonOperations which takes care of
        // positioning the caret so no need to do it here
        _operations.PutAtCaret stringData register.Value.OperationKind PutKind.After moveCaretAfterText
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Put the contents of the specified register before the cursor.  Used for the
    /// normal / visual 'P' command
    member x.PutBeforeCaret (register : Register) count moveCaretAfterText =
        let point = SnapshotPointUtil.AddOneOrCurrent x.CaretPoint
        let stringData = register.StringData.ApplyCount count

        // PutAtCaret is one of the few ICommonOperations which takes care of
        // positioning the caret so no need to do it here
        _operations.PutAtCaret stringData register.Value.OperationKind PutKind.Before moveCaretAfterText
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Put the contents of the specified register after the selection.  Used for the
    /// the visual 'p' and 'gp' command
    member x.PutOverSelection (register : Register) count visualSpan moveCaretAfterText = 
        let stringData = register.StringData.ApplyCount count
        match visualSpan with
        | VisualSpan.Character span ->
            // Cursor needs to be at the start of the span during undo and at the end
            // of the pasted span after redo so move to the start before the undo transaction
            TextViewUtil.MoveCaretToPoint _textView span.Start
            x.EditWithUndoTransaciton "Put" (fun () ->
                _textBuffer.Replace(span.Span, stringData.String) |> ignore

                let position = span.Start.Position + stringData.String.Length - 1
                let position = if moveCaretAfterText then position + 1 else position
                TextViewUtil.MoveCaretToPosition _textView position

                // Need to respect the virtualedit setting
                _operations.MoveCaretForVirtualEdit())

        | VisualSpan.Line range ->
            // Cursor needs to be positioned at the start of the range for both undo so
            // move the caret now 
            TextViewUtil.MoveCaretToPoint _textView range.Start
            x.EditWithUndoTransaciton "Put" (fun () ->
                let snapshot = _textBuffer.Replace(range.Extent.Span, stringData.String)

                if moveCaretAfterText then
                    // Move to the line after the line which was the start of the replace.  If this
                    // occured at the end of the buffer then pretend we didn't have 'moveCaretAfterText'
                    match SnapshotUtil.TryGetLine snapshot (range.StartLineNumber + 1) with
                    | None -> TextViewUtil.MoveCaretToPosition _textView range.Start.Position
                    | Some line -> TextViewUtil.MoveCaretToPoint _textView line.Start
                else
                    // Need to be positioned at the start of the original range
                    TextViewUtil.MoveCaretToPosition _textView range.Start.Position)

        | VisualSpan.Block col ->
            // Cursor needs to be positioned at the start of the range for undo so
            // move the caret now
            match NormalizedSnapshotSpanCollectionUtil.TryGetFirst col with
            | None -> 
                ()
            | Some span -> 
                TextViewUtil.MoveCaretToPoint _textView span.Start
                x.EditWithUndoTransaciton "Put" (fun () ->
                    use edit = _textBuffer.CreateEdit()

                    // First delete everything but the first span 
                    col
                    |> SeqUtil.skipMax 1
                    |> Seq.iter (fun span -> edit.Delete(span.Span) |> ignore)

                    // Now replace the first span with the contents of the register 
                    edit.Replace(span.Span, stringData.String) |> ignore
                    edit.Apply() |> ignore

                    let position = span.Start.Position + stringData.String.Length - 1
                    let position = if moveCaretAfterText then position + 1 else position
                    TextViewUtil.MoveCaretToPosition _textView position

                    // Need to respect the virtualedit setting
                    _operations.MoveCaretForVirtualEdit())

        CommandResult.Completed ModeSwitch.SwitchPreviousMode

    /// Repeat the last executed command against the current buffer
    member x.RepeatLastCommand (repeatData : CommandData) = 

        // Function to actually repeat the last change 
        let rec repeat (command : StoredCommand) (repeatData : CommandData option) = 

            // Calculate the new CommandData based on the original and repeat CommandData 
            // values.  The repeat CommandData will be None in nested command repeats 
            // for linked commands which means the original should just be used
            let getCommandData (original : CommandData) = 
                match repeatData with
                | None -> 
                    original
                | Some repeatData -> 
                    match repeatData.Count with
                    | Some count -> { original with Count = repeatData.Count }
                    | None -> original

            // Repeat a text change.  
            let repeatTextChange change = 

                // Calculate the count of the repeat
                let count = 
                    match repeatData with
                    | Some repeatData -> repeatData.CountOrDefault
                    | None -> 1

                match change with 
                | TextChange.Insert text -> 
                    _operations.InsertText text count
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
                match repeat command1 repeatData with
                | CommandResult.Error -> CommandResult.Error
                | CommandResult.Completed _ -> repeat command2 None
            | StoredCommand.LegacyCommand (keyInputSet, _) -> 

                // Don't support repeat of Legacy Commands.  They're the reason we moved to this
                // new system
                _statusUtil.OnError (Resources.Common_CannotRepeatLegacy (keyInputSet.ToString()))
                CommandResult.Error

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
                    repeat command (Some repeatData)
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
    member x.ReplaceSelection keyInput (visualSpan : VisualSpan) = 

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
        | Command.LegacyCommand data -> data.Function()

    /// Run a NormalCommand against the buffer
    member x.RunNormalCommand command (data : CommandData) =
        let register = _registerMap.GetRegister data.RegisterNameOrDefault
        let count = data.CountOrDefault
        match command with
        | NormalCommand.ChangeMotion motion -> x.RunWithMotion motion (x.ChangeMotion register)
        | NormalCommand.ChangeCaseCaretLine kind -> x.ChangeCaseCaretLine kind
        | NormalCommand.ChangeCaseCaretPoint kind -> x.ChangeCaseCaretPoint kind count
        | NormalCommand.ChangeCaseMotion (kind, motion) -> x.RunWithMotion motion (x.ChangeCaseMotion kind)
        | NormalCommand.ChangeLines -> x.ChangeLines count register
        | NormalCommand.ChangeTillEndOfLine -> x.ChangeTillEndOfLine count register
        | NormalCommand.DeleteCharacterAtCaret -> x.DeleteCharacterAtCaret count register
        | NormalCommand.DeleteCharacterBeforeCaret -> x.DeleteCharacterBeforeCaret count register
        | NormalCommand.DeleteLines -> x.DeleteLines count register
        | NormalCommand.DeleteMotion motion -> x.RunWithMotion motion (x.DeleteMotion register)
        | NormalCommand.DeleteTillEndOfLine -> x.DeleteTillEndOfLine count register
        | NormalCommand.FormatLines -> x.FormatLines count
        | NormalCommand.FormatMotion motion -> x.RunWithMotion motion x.FormatMotion 
        | NormalCommand.Insert -> x.Insert count
        | NormalCommand.InsertAtFirstNonBlank -> x.InsertAtFirstNonBlank count
        | NormalCommand.JoinLines kind -> x.JoinLines kind count
        | NormalCommand.JumpToMark c -> x.JumpToMark c
        | NormalCommand.MoveCaretToMotion motion -> x.MoveCaretToMotion motion data.Count
        | NormalCommand.Ping pingData -> x.Ping pingData data
        | NormalCommand.PutAfterCaret moveCaretAfterText -> x.PutAfterCaret register count moveCaretAfterText
        | NormalCommand.PutBeforeCaret moveCaretBeforeText -> x.PutBeforeCaret register count moveCaretBeforeText
        | NormalCommand.SetMarkToCaret c -> x.SetMarkToCaret c
        | NormalCommand.SubstituteCharacterAtCaret -> x.SubstituteCharacterAtCaret count register
        | NormalCommand.ShiftLinesLeft -> x.ShiftLinesLeft count
        | NormalCommand.ShiftLinesRight -> x.ShiftLinesRight count
        | NormalCommand.ShiftMotionLinesLeft motion -> x.RunWithMotion motion x.ShiftMotionLinesLeft
        | NormalCommand.ShiftMotionLinesRight motion -> x.RunWithMotion motion x.ShiftMotionLinesRight
        | NormalCommand.RepeatLastCommand -> x.RepeatLastCommand data
        | NormalCommand.ReplaceChar keyInput -> x.ReplaceChar keyInput data.CountOrDefault
        | NormalCommand.Yank motion -> x.RunWithMotion motion (x.YankMotion register)

    /// Run a VisualCommand against the buffer
    member x.RunVisualCommand command (data : CommandData) (visualSpan : VisualSpan) = 
        let register = _registerMap.GetRegister data.RegisterNameOrDefault
        let count = data.CountOrDefault
        match command with
        | VisualCommand.ChangeCase kind -> x.ChangeCaseVisual kind visualSpan
        | VisualCommand.DeleteSelectedText -> x.DeleteSelectedText register visualSpan
        | VisualCommand.FormatLines -> x.FormatLinesVisual visualSpan
        | VisualCommand.PutOverSelection moveCaretAfterText -> x.PutOverSelection register count visualSpan moveCaretAfterText
        | VisualCommand.ReplaceSelection keyInput -> x.ReplaceSelection keyInput visualSpan
        | VisualCommand.ShiftLinesLeft -> x.ShiftLinesLeftVisual count visualSpan
        | VisualCommand.ShiftLinesRight -> x.ShiftLinesRightVisual count visualSpan

    /// Get the MotionResult value for the provided MotionData and pass it
    /// if found to the provided function
    member x.RunWithMotion (motion : MotionData) func = 
        match _motionUtil.GetMotion motion.Motion motion.MotionArgument with
        | None ->  
            _statusUtil.OnError Resources.MotionCapture_InvalidMotion
            CommandResult.Error
        | Some data -> 
            func data

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

    /// Shift the given line range left by the specified value.  The caret will be 
    /// placed at the first character on the first line of the shifted text
    member x.ShiftLinesLeftCore range multiplier =

        // Use a transaction so the caret will be properly moved for undo / redo
        x.EditWithUndoTransaciton "ShiftLeft" (fun () ->
            _operations.ShiftLineRangeLeft range multiplier

            // Now move the caret to the first non-whitespace character on the first
            // line 
            let line = SnapshotUtil.GetLine x.CurrentSnapshot range.StartLineNumber
            let point = 
                match TssUtil.TryFindFirstNonWhiteSpaceCharacter line with
                | None -> SnapshotLineUtil.GetLastIncludedPoint line |> OptionUtil.getOrDefault line.Start
                | Some point -> point
            TextViewUtil.MoveCaretToPoint _textView point)

    /// Shift the given line range left by the specified value.  The caret will be 
    /// placed at the first character on the first line of the shifted text
    member x.ShiftLinesRightCore range multiplier =

        // Use a transaction so the caret will be properly moved for undo / redo
        x.EditWithUndoTransaciton "ShiftRight" (fun () ->
            _operations.ShiftLineRangeRight range multiplier

            // Now move the caret to the first non-whitespace character on the first
            // line 
            let line = SnapshotUtil.GetLine x.CurrentSnapshot range.StartLineNumber
            let point = 
                match TssUtil.TryFindFirstNonWhiteSpaceCharacter line with
                | None -> SnapshotLineUtil.GetLastIncludedPoint line |> OptionUtil.getOrDefault line.Start
                | Some point -> point
            TextViewUtil.MoveCaretToPoint _textView point)

    /// Shift 'count' lines to the left 
    member x.ShiftLinesLeft count =
        let range = SnapshotLineRangeUtil.CreateForLineAndMaxCount x.CaretLine count
        x.ShiftLinesLeftCore range 1
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Shift 'motion' lines to the left by 'count' shiftwidth.
    member x.ShiftLinesLeftVisual count visualSpan = 

        // Both Character and Line spans operate like most shifts
        match visualSpan with
        | VisualSpan.Character span ->
            let range = SnapshotLineRangeUtil.CreateForSpan span
            x.ShiftLinesLeftCore range count
        | VisualSpan.Line range ->
            x.ShiftLinesLeftCore range count
        | VisualSpan.Block col ->
            // Shifting a block span is trickier because it doesn't shift at column
            // 0 but rather shifts at the start column of every span.  It also treats
            // the caret much more different by keeping it at the start of the first
            // span vs. the start of the shift

            let targetCaretPosition = 
                visualSpan.Start 
                |> OptionUtil.getOrDefault x.CaretPoint 
                |> SnapshotPointUtil.GetPosition

            // Use a transaction to preserve the caret.  But move the caret first since
            // it needs to be undone to this location
            TextViewUtil.MoveCaretToPosition _textView targetCaretPosition
            x.EditWithUndoTransaciton "ShiftLeft" (fun () -> 
                _operations.ShiftLineBlockRight col count
                TextViewUtil.MoveCaretToPosition _textView targetCaretPosition)

        CommandResult.Completed ModeSwitch.SwitchPreviousMode

    /// Shift 'count' lines to the right 
    member x.ShiftLinesRight count =
        let range = SnapshotLineRangeUtil.CreateForLineAndMaxCount x.CaretLine count
        x.ShiftLinesRightCore range 1
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Shift 'motion' lines to the right by 'count' shiftwidth
    member x.ShiftLinesRightVisual count visualSpan = 

        // Both Character and Line spans operate like most shifts
        match visualSpan with
        | VisualSpan.Character span ->
            let range = SnapshotLineRangeUtil.CreateForSpan span
            x.ShiftLinesRightCore range count
        | VisualSpan.Line range ->
            x.ShiftLinesRightCore range count
        | VisualSpan.Block col ->
            // Shifting a block span is trickier because it doesn't shift at column
            // 0 but rather shifts at the start column of every span.  It also treats
            // the caret much more different by keeping it at the start of the first
            // span vs. the start of the shift
            let targetCaretPosition = 
                visualSpan.Start 
                |> OptionUtil.getOrDefault x.CaretPoint 
                |> SnapshotPointUtil.GetPosition

            // Use a transaction to preserve the caret.  But move the caret first since
            // it needs to be undone to this location
            TextViewUtil.MoveCaretToPosition _textView targetCaretPosition
            x.EditWithUndoTransaciton "ShiftLeft" (fun () -> 
                _operations.ShiftLineBlockRight col count

                TextViewUtil.MoveCaretToPosition _textView targetCaretPosition)

        CommandResult.Completed ModeSwitch.SwitchPreviousMode

    /// Shift 'motion' lines to the left
    member x.ShiftMotionLinesLeft (result : MotionResult) = 
        x.ShiftLinesLeftCore result.OperationLineRange 1
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Shift 'motion' lines to the right
    member x.ShiftMotionLinesRight (result : MotionResult) = 
        x.ShiftLinesRightCore result.OperationLineRange 1
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Substitute 'count' characters at the cursor on the current line.  Very similar to
    /// DeleteCharacterAtCaret.  Main exception is the behavior when the caret is on
    /// or after the last character in the line
    /// should be after the span for Substitute even if 've='.  
    member x.SubstituteCharacterAtCaret count register =

        if x.CaretPoint.Position >= x.CaretLine.End.Position then
            // When we are past the end of the line just move the caret
            // to the end of the line and complete the command.  Nothing should be deleted
            TextViewUtil.MoveCaretToPoint _textView x.CaretLine.End
        else
            let endPoint = SnapshotLineUtil.GetOffsetOrEnd x.CaretLine (x.CaretColumn + count)
            let span = SnapshotSpan(x.CaretPoint, endPoint)

            // Use a transaction so we can guarantee the caret is in the correct
            // position on undo / redo
            x.EditWithUndoTransaciton "DeleteChar" (fun () -> 
                let position = x.CaretPoint.Position
                let snapshot = _textBuffer.Delete(span.Span)
                TextViewUtil.MoveCaretToPoint _textView (SnapshotPoint(snapshot, position)))

            // Put the deleted text into the specified register
            let value = { Value = StringData.OfSpan span; OperationKind = OperationKind.CharacterWise }
            _registerMap.SetRegisterValue register RegisterOperation.Delete value

        CommandResult.Completed (ModeSwitch.SwitchMode ModeKind.Insert)

    /// Yank the contents of the motion into the specified register
    member x.YankMotion register (result: MotionResult) = 
        let value = { Value = StringData.OfSpan result.OperationSpan; OperationKind = result.OperationKind }
        _registerMap.SetRegisterValue register RegisterOperation.Yank value
        CommandResult.Completed ModeSwitch.NoSwitch

    interface ICommandUtil with
        member x.RunNormalCommand command data = x.RunNormalCommand command data
        member x.RunVisualCommand command data visualSpan = x.RunVisualCommand command data visualSpan 
        member x.RunCommand command = x.RunCommand command

