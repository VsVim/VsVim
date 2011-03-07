

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
        _localSettings : IVimLocalSettings,
        _undoRedoOperations : IUndoRedoOperations,
        _smartIndentationService : ISmartIndentationService,
        _foldManager : IFoldManager,
        _vimHost : IVimHost
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
                |> NonEmptyCollectionUtil.OfSeq
                |> Option.get
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
        let point = visualSpan.Start
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
            x.MoveCaretToDeletedLineStart range.StartLine)

        // Update the register now that the operation is complete.  Register value is odd here
        // because we really didn't delete linewise but it's required to be a linewise 
        // operation.  
        let value = range.Extent.GetText() + System.Environment.NewLine
        let value = { Value = StringData.Simple value; OperationKind = OperationKind.LineWise }
        _registerMap.SetRegisterValue register RegisterOperation.Delete value

        CommandResult.Completed (ModeSwitch.SwitchMode ModeKind.Insert)

    /// Delete the selected lines and begin insert mode (implements the 'S', 'C' and 'R' visual
    /// mode commands.  This is very similar to DeleteLineSelection except that block deletion
    /// can be special cased depending on the command it's used in
    member x.ChangeLineSelection register (visualSpan : VisualSpan) specialCaseBlock =

        // The majority of cases simply delete a SnapshotLineRange directly.  Handle that here
        let deleteRange (range : SnapshotLineRange) = 

            // In an undo the caret position has 2 cases.
            //  - Single line range: Start of the first line
            //  - Multiline range: Start of the second line.
            let point = 
                if range.Count = 1 then 
                    range.StartLine.Start
                else 
                    let next = SnapshotUtil.GetLine range.Snapshot (range.StartLineNumber + 1)
                    next.Start
            TextViewUtil.MoveCaretToPoint _textView point

            x.EditWithUndoTransaciton "ChangeLines" (fun () -> 
                _textBuffer.Delete(range.Extent.Span) |> ignore
                x.MoveCaretToDeletedLineStart range.StartLine)

            EditSpan.Single range.Extent

        // The special casing of block deletion is handled here
        let deleteBlock (col : NonEmptyCollection<SnapshotSpan>) = 

            // First step is to change the SnapshotSpan instances to extent from the start to the
            // end of the current line 
            let col = col |> NonEmptyCollectionUtil.Map (fun span -> 
                let line = SnapshotPointUtil.GetContainingLine span.Start
                SnapshotSpan(span.Start, line.End))

            // Caret should be positioned at the start of the span for undo
            TextViewUtil.MoveCaretToPoint _textView col.Head.Start

            x.EditWithUndoTransaciton "ChangeLines" (fun () -> 
                let edit = _textBuffer.CreateEdit()
                col |> Seq.iter (fun span -> edit.Delete(span.Span) |> ignore)
                edit.Apply() |> ignore

                TextViewUtil.MoveCaretToPosition _textView col.Head.Start.Position)

            EditSpan.Block col

        // Dispatch to the appropriate type of edit
        let editSpan = 
            match visualSpan with 
            | VisualSpan.Character span -> 
                span |> SnapshotLineRangeUtil.CreateForSpan |> deleteRange
            | VisualSpan.Line range -> 
                deleteRange range
            | VisualSpan.Block col -> 
                if specialCaseBlock then deleteBlock col 
                else visualSpan.EditSpan.OverarchingSpan |> SnapshotLineRangeUtil.CreateForSpan |> deleteRange

        let value = { Value = StringData.OfEditSpan editSpan; OperationKind = OperationKind.LineWise }
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

    /// Delete the selected text from the buffer and put it into the specified 
    /// register. 
    member x.DeleteLineSelection register (visualSpan : VisualSpan) =

        // For each of the 3 cases the caret should begin at the start of the 
        // VisualSpan during undo so move the caret now. 
        TextViewUtil.MoveCaretToPoint _textView visualSpan.Start

        // Start a transaction so we can manipulate the caret position during 
        // an undo / redo
        let editSpan = 
            x.EditWithUndoTransaciton "Delete" (fun () -> 
    
                use edit = _textBuffer.CreateEdit()
                let editSpan = 
                    match visualSpan with
                    | VisualSpan.Character span ->
                        // Just extend the SnapshotSpan to the encompasing SnapshotLineRange 
                        let range = SnapshotLineRangeUtil.CreateForSpan span
                        let span = range.ExtentIncludingLineBreak
                        edit.Delete(span.Span) |> ignore
                        EditSpan.Single span
                    | VisualSpan.Line range ->
                        // Easiest case.  It's just the range
                        edit.Delete(range.ExtentIncludingLineBreak.Span) |> ignore
                        EditSpan.Single range.ExtentIncludingLineBreak
                    | VisualSpan.Block col -> 
                        col
                        |> Seq.iter (fun span -> 
                            // Delete from the start of the span until the end of the containing
                            // line
                            let span = 
                                let line = SnapshotPointUtil.GetContainingLine span.Start
                                SnapshotSpan(span.Start, line.End)
                            edit.Delete(span.Span) |> ignore)
                        EditSpan.Block col
    
                edit.Apply() |> ignore
    
                // Now position the cursor back at the start of the VisualSpan
                TextViewUtil.MoveCaretToPosition _textView visualSpan.Start.Position
    
                // Possible for a block mode to deletion to cause the start to now be in the line 
                // break so we need to acount for the 'virtualedit' setting
                _operations.MoveCaretForVirtualEdit()

                editSpan)

        let value = { Value = StringData.OfEditSpan editSpan; OperationKind = OperationKind.LineWise }
        _registerMap.SetRegisterValue register RegisterOperation.Delete value

        CommandResult.Completed ModeSwitch.SwitchPreviousMode

    /// Delete the highlighted text from the buffer and put it into the specified 
    /// register.  The caret should be positioned at the begining of the text for
    /// undo / redo
    member x.DeleteSelection register (visualSpan : VisualSpan) modeSwitch = 
        let startPoint = visualSpan.Start

        // Use a transaction to guarantee caret position.  Caret should be at the start
        // during undo and redo so move it before the edit
        TextViewUtil.MoveCaretToPoint _textView startPoint
        x.EditWithUndoTransaciton "DeleteSelection" (fun () ->
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

        let operationKind = 
            match visualSpan with
            | VisualSpan.Character _ -> OperationKind.CharacterWise
            | VisualSpan.Line _ -> OperationKind.LineWise
            | VisualSpan.Block _ -> OperationKind.CharacterWise
        let value = { Value = StringData.OfEditSpan visualSpan.EditSpan; OperationKind = operationKind }
        _registerMap.SetRegisterValue register RegisterOperation.Delete value

        CommandResult.Completed modeSwitch

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

    /// Run the specified action with a wrapped undo transaction.  This is often necessary when
    /// an edit command manipulates the caret
    member x.EditWithUndoTransaciton<'T> (name : string) (action : unit -> 'T) : 'T = 
        _undoRedoOperations.EditWithUndoTransaction name action

    /// Create a fold for the given MotionResult
    member x.FoldMotion (result : MotionResult) =
        _foldManager.CreateFold result.LineRange

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Fold the specified selection 
    member x.FoldSelection (visualSpan : VisualSpan) = 
        _foldManager.CreateFold visualSpan.LineRange

        CommandResult.Completed ModeSwitch.SwitchPreviousMode

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

    /// GoTo the file name under the cursor and possiby use a new window
    member x.GoToFileUnderCaret useNewWindow =
        if useNewWindow then _operations.GoToFileInNewWindow()
        else _operations.GoToFile()

        CommandResult.Completed ModeSwitch.NoSwitch

    /// GoTo the ITextView in the specified direction
    member x.GoToView direction = 
        match direction with
        | Direction.Up -> _vimHost.MoveViewUp _textView
        | Direction.Down -> _vimHost.MoveViewDown _textView
        | Direction.Left -> _vimHost.MoveViewLeft _textView
        | Direction.Right -> _vimHost.MoveViewRight _textView

        CommandResult.Completed ModeSwitch.NoSwitch

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
            // The caret should be positioned one after the second to last line in the 
            // join.  It should have it's original position during an undo so don't
            // move the caret until we're inside the transaction
            x.EditWithUndoTransaciton "Join" (fun () -> 
                _operations.Join range kind
                x.MoveCaretFollowingJoin range)

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Join the selection of lines in the buffer
    member x.JoinSelection kind (visualSpan : VisualSpan) = 
        let range = SnapshotLineRangeUtil.CreateForSpan visualSpan.EditSpan.OverarchingSpan 

        // Extend the range to at least 2 lines if possible
        let range = 
            if range.Count = 1 && range.EndLineNumber = SnapshotUtil.GetLastLineNumber range.Snapshot then
                // Can't extend
                range
            elif range.Count = 1 then
                // Extend it 1 line
                SnapshotLineRange(range.Snapshot, range.StartLineNumber, 2)
            else
                // Already at least 2 lines
                range

        if range.Count = 1 then
            // Can't join a single line
            _operations.Beep()

            CommandResult.Completed ModeSwitch.NoSwitch
        else 
            // The caret before the join should be positioned at the start of the VisualSpan
            TextViewUtil.MoveCaretToPoint _textView visualSpan.Start
            x.EditWithUndoTransaciton "Join" (fun () -> 
                _operations.Join range kind
                x.MoveCaretFollowingJoin range)

            CommandResult.Completed ModeSwitch.SwitchPreviousMode

    /// Jump to the specified mark
    member x.JumpToMark c =
        match _operations.JumpToMark c _markMap with
        | Modes.Result.Failed msg ->
            _statusUtil.OnError msg
            CommandResult.Error
        | Modes.Result.Succeeded ->
            CommandResult.Completed ModeSwitch.NoSwitch

    /// Switch to insert mode after the caret 
    member x.InsertAfterCaret count = 
        let point = x.CaretPoint
        if SnapshotPointUtil.IsInsideLineBreak point then 
            ()
        elif SnapshotPointUtil.IsEndPoint point then 
            ()
        else 
            let point = point.Add(1)
            TextViewUtil.MoveCaretToPoint _textView point

        CommandResult.Completed (ModeSwitch.SwitchModeWithArgument (ModeKind.Insert, ModeArgument.InsertWithCount count))

    /// Switch to Insert mode with the specified count
    member x.InsertBeforeCaret count =
        CommandResult.Completed (ModeSwitch.SwitchModeWithArgument (ModeKind.Insert, ModeArgument.InsertWithCount count))

    /// Switch to insert mode at the end of the line
    member x.InsertAtEndOfLine count =
        TextViewUtil.MoveCaretToPoint _textView x.CaretLine.End

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

    /// Switch to insert mode at the start of the line
    member x.InsertAtStartOfLine count =
        TextViewUtil.MoveCaretToPoint _textView x.CaretLine.Start

        CommandResult.Completed (ModeSwitch.SwitchModeWithArgument (ModeKind.Insert, ModeArgument.InsertWithCount count))

    /// Insert a line above the current caret line and begin insert mode at the start of that
    /// line
    member x.InsertLineAbove count = 
        let savedCaretLine = x.CaretLine

        // REPEAT TODO: Need to file a bug to get the caret position correct here for redo
        _undoRedoOperations.EditWithUndoTransaction "InsertLineAbove" (fun() -> 
            let line = x.CaretLine
            _textBuffer.Replace(new Span(line.Start.Position,0), System.Environment.NewLine) |> ignore)

        // Position the caret for the edit
        let line = SnapshotUtil.GetLine x.CurrentSnapshot savedCaretLine.LineNumber
        x.MoveCaretToNewLineIndent savedCaretLine line

        let switch = ModeSwitch.SwitchModeWithArgument (ModeKind.Insert, ModeArgument.InsertWithCountAndNewLine count)
        CommandResult.Completed switch

    /// Insert a line below the current caret line and begin insert mode at the start of that 
    /// line
    member x.InsertLineBelow count = 

        // The caret position here odd.  The caret during undo / redo should be in the original
        // caret position.  However the edit needs to occur with the caret indented on the newly
        // created line.  So there are actually 3 caret positions to consider here
        //
        //  1. Before Edit (Undo)
        //  2. After the Edit but in the Transaction (Redo)
        //  3. For the eventual user edit

        let savedCaretPoint = x.CaretPoint
        let savedCaretLine = x.CaretLine
        _undoRedoOperations.EditWithUndoTransaction  "InsertLineBelow" (fun () -> 
            let span = new SnapshotSpan(savedCaretLine.EndIncludingLineBreak, 0)
            _textBuffer.Replace(span.Span, System.Environment.NewLine) |> ignore

            TextViewUtil.MoveCaretToPosition _textView savedCaretPoint.Position)

        let newLine = SnapshotUtil.GetLine x.CurrentSnapshot (savedCaretLine.LineNumber + 1)
        x.MoveCaretToNewLineIndent savedCaretLine newLine

        let switch = ModeSwitch.SwitchModeWithArgument (ModeKind.Insert, ModeArgument.InsertWithCountAndNewLine count)
        CommandResult.Completed switch

    /// Move the caret to start of a line which is deleted.  Needs to preserve the original 
    /// indent if 'autoindent' is set.
    ///
    /// Be wary of using this function.  It has the implicit contract that the Start position
    /// of the line is still valid.  
    member x.MoveCaretToDeletedLineStart (deletedLine : ITextSnapshotLine) =
        Contract.Requires (deletedLine.Start.Position <= x.CurrentSnapshot.Length)

        if _localSettings.AutoIndent then
            // Caret needs to be positioned at the indentation point of the previous line.  Don't
            // create actual whitespace, put the caret instead into virtual space
            let column = 
                deletedLine.Start
                |> SnapshotPointUtil.GetContainingLine
                |> SnapshotLineUtil.GetIndent
                |> SnapshotPointUtil.GetColumn
            if column = 0 then 
                TextViewUtil.MoveCaretToPosition _textView deletedLine.Start.Position
            else
                let point = SnapshotUtil.GetPoint x.CurrentSnapshot deletedLine.Start.Position
                let virtualPoint = VirtualSnapshotPoint(point, column)
                TextViewUtil.MoveCaretToVirtualPoint _textView virtualPoint
        else
            // Put the caret at column 0
            TextViewUtil.MoveCaretToPosition _textView deletedLine.Start.Position

    /// Move the caret to the indentation point applicable for a new line in the ITextBuffer
    member x.MoveCaretToNewLineIndent oldLine (newLine : ITextSnapshotLine) =
        let doVimIndent() = 
            if _localSettings.AutoIndent then
                let indent = oldLine |> SnapshotLineUtil.GetIndent |> SnapshotPointUtil.GetColumn
                let point = new VirtualSnapshotPoint(newLine, indent)
                TextViewUtil.MoveCaretToVirtualPoint _textView point |> ignore 
            else
                TextViewUtil.MoveCaretToPoint _textView newLine.Start |> ignore

        if _localSettings.GlobalSettings.UseEditorIndent then
            let indent = _smartIndentationService.GetDesiredIndentation(_textView, newLine)
            if indent.HasValue then 
                let point = new VirtualSnapshotPoint(newLine, indent.Value)
                TextViewUtil.MoveCaretToVirtualPoint _textView point |> ignore
            else
               doVimIndent()
        else 
            doVimIndent()

    /// The Join commands (Visual and Normal) have identical cursor positioning behavior and 
    /// it's non-trivial so it's factored out to a function here.  In short the caret should be
    /// positioned 1 position after the last character in the second to last line of the join
    /// The caret should be positioned one after the second to last line in the 
    /// join.  It should have it's original position during an undo so don't
    /// move the caret until we're inside the transaction
    member x.MoveCaretFollowingJoin (range : SnapshotLineRange) =
        let point = 
            let number = range.StartLineNumber + range.Count - 2
            let line = SnapshotUtil.GetLine range.Snapshot number
            line |> SnapshotLineUtil.GetLastIncludedPoint |> OptionUtil.getOrDefault line.Start
        match TrackingPointUtil.GetPointInSnapshot point PointTrackingMode.Positive x.CurrentSnapshot with
        | None -> 
            ()
        | Some point -> 
            let point = SnapshotPointUtil.AddOneOrCurrent point
            TextViewUtil.MoveCaretToPoint _textView point

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
    /// 'p' and 'gp' command in normal mode
    member x.PutAfterCaret (register : Register) count moveCaretAfterText =
        let stringData = register.StringData.ApplyCount count
        let point = 
            match register.OperationKind with
            | OperationKind.CharacterWise -> SnapshotPointUtil.GetNextPointOnLine x.CaretPoint 1
            | OperationKind.LineWise -> x.CaretLine.EndIncludingLineBreak

        x.PutCore point stringData register.OperationKind moveCaretAfterText

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Put the contents of the specified register before the cursor.  Used for the
    /// 'P' and 'gP' commands in normal mode
    member x.PutBeforeCaret (register : Register) count moveCaretAfterText =
        let stringData = register.StringData.ApplyCount count
        let point = 
            match register.OperationKind with
            | OperationKind.CharacterWise -> x.CaretPoint
            | OperationKind.LineWise -> x.CaretLine.Start

        x.PutCore point stringData register.OperationKind moveCaretAfterText

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Put the contents of the specified register after the cursor.  Used for the
    /// normal 'p', 'gp', 'P' and 'gP' commands.  For linewise put operations the
    /// point must be at the start of a line
    member x.PutCore point stringData operationKind moveCaretAfterText =
        // Save the point incase this is a linewise insertion and we need to
        // move after the inserted lines
        let oldPoint = point

        // The caret should be positioned at the current position in undo so don't move
        // it before the transaction.
        x.EditWithUndoTransaciton "Put" (fun () -> 

            _operations.Put point stringData operationKind

            // Edit is complete.  Position the caret against the updated text.  First though
            // get the original insertion point in the new ITextSnapshot
            let point = SnapshotUtil.GetPoint x.CurrentSnapshot point.Position
            match operationKind with
            | OperationKind.CharacterWise -> 

                let point = 
                    match stringData with
                    | StringData.Simple _ ->
                        // For characterwise we just increment the length of the first string inserted
                        // and possibily one more if moving after
                        let point = 
                            let offset = stringData.FirstString.Length - 1
                            let offset = max 0 offset
                            SnapshotPointUtil.Add offset point
                        if moveCaretAfterText then SnapshotPointUtil.AddOneOrCurrent point else point
                    | StringData.Block col -> 
                        if moveCaretAfterText then
                            // Needs to be positioned after the last item in the collection
                            let line = 
                                let number = oldPoint |> SnapshotPointUtil.GetContainingLine |> SnapshotLineUtil.GetLineNumber
                                let number = number + (col.Count - 1)
                                SnapshotUtil.GetLine x.CurrentSnapshot number
                            let offset = (SnapshotPointUtil.GetColumn point) + col.Head.Length
                            SnapshotPointUtil.Add offset line.Start
                        else
                            // Position at the original insertion point
                            SnapshotUtil.GetPoint x.CurrentSnapshot oldPoint.Position

                TextViewUtil.MoveCaretToPoint _textView point
            | OperationKind.LineWise ->

                // Get the line on which we will be positioning the caret
                let line = 
                    if moveCaretAfterText then
                        // Move to the first line after the insertion.  Can be calculated with a line
                        // count offset
                        let offset = x.CurrentSnapshot.LineCount - oldPoint.Snapshot.LineCount
                        let number = oldPoint |> SnapshotPointUtil.GetContainingLine |> SnapshotLineUtil.GetLineNumber
                        SnapshotUtil.GetLine x.CurrentSnapshot (number + offset)
                    else
                        // The caret should be moved to the first line of the inserted text.
                        let number = 
                            let oldLineNumber = oldPoint |> SnapshotPointUtil.GetContainingLine |> SnapshotLineUtil.GetLineNumber
                            if SnapshotPointUtil.IsStartOfLine oldPoint then 
                                oldLineNumber
                            else
                                // Anything other than the start of the line will cause the Put to 
                                // occur one line below and we need to account for that
                                oldLineNumber + 1
                        SnapshotUtil.GetLine x.CurrentSnapshot number

                // Get the indent point of the line.  That's what the caret needs to be moved to
                let point = SnapshotLineUtil.GetIndent line
                TextViewUtil.MoveCaretToPoint _textView point

            _operations.MoveCaretForVirtualEdit())

    /// Put the contents of the specified register over the selection.  This is used for all
    /// visual mode put commands. 
    member x.PutOverSelection (register : Register) count moveCaretAfterText visualSpan = 

        // Build up the common variables
        let stringData = register.StringData.ApplyCount count
        let operationKind = register.OperationKind

        let deletedSpan, operationKind = 
            match visualSpan with
            | VisualSpan.Character span ->

                // Cursor needs to be at the start of the span during undo and at the end
                // of the pasted span after redo so move to the start before the undo transaction
                TextViewUtil.MoveCaretToPoint _textView span.Start
                x.EditWithUndoTransaciton "Put" (fun () ->
    
                    // Delete the span and move the caret back to the start of the 
                    // span in the new ITextSnapshot
                    _textBuffer.Delete(span.Span) |> ignore
                    TextViewUtil.MoveCaretToPosition _textView span.Start.Position

                    // Now do a standard put operation at the original start point in the current
                    // ITextSnapshot
                    let point = SnapshotUtil.GetPoint x.CurrentSnapshot span.Start.Position
                    x.PutCore point stringData operationKind moveCaretAfterText

                    EditSpan.Single span, OperationKind.CharacterWise)
            | VisualSpan.Line range ->

                // Cursor needs to be positioned at the start of the range for both undo so
                // move the caret now 
                TextViewUtil.MoveCaretToPoint _textView range.Start
                x.EditWithUndoTransaciton "Put" (fun () ->

                    // When putting over a linewise selection the put needs to be done
                    // in a linewise fashion.  This means in certain cases we have to adjust
                    // the StringData to have proper newline semantics
                    let stringData = 
                        match stringData with
                        | StringData.Simple str ->
                            let str = if EditUtil.EndsWithNewLine str then str else str + EditUtil.NewLine
                            StringData.Simple str
                        | StringData.Block _ -> 
                            stringData
                    let operationKind = OperationKind.LineWise

                    // Delete the span and move the caret back to the start
                    _textBuffer.Delete(range.ExtentIncludingLineBreak.Span) |> ignore
                    TextViewUtil.MoveCaretToPosition _textView range.Start.Position

                    // Now do a standard put operation at the start of the SnapshotLineRange
                    // in the current ITextSnapshot
                    let point = SnapshotUtil.GetPoint x.CurrentSnapshot range.Start.Position
                    x.PutCore point stringData operationKind moveCaretAfterText

                    EditSpan.Single range.ExtentIncludingLineBreak, OperationKind.LineWise)

            | VisualSpan.Block col ->

                // Cursor needs to be positioned at the start of the range for undo so
                // move the caret now
                let span = col.Head
                TextViewUtil.MoveCaretToPoint _textView span.Start
                x.EditWithUndoTransaciton "Put" (fun () ->

                    // Delete all of the items in the collection
                    use edit = _textBuffer.CreateEdit()
                    col |> Seq.iter (fun span -> edit.Delete(span.Span) |> ignore)
                    edit.Apply() |> ignore

                    // Now do a standard put operation.  The point of the put varies a bit 
                    // based on whether we're doing a linewise or characterwise insert
                    let point = 
                        match operationKind with
                        | OperationKind.CharacterWise -> 
                            // Put occurs at the start of the original span
                            SnapshotUtil.GetPoint x.CurrentSnapshot span.Start.Position
                        | OperationKind.LineWise -> 
                            // Put occurs on the line after the last edit
                            let lastSpan = col |> SeqUtil.last
                            let number = lastSpan.Start |> SnapshotPointUtil.GetContainingLine |> SnapshotLineUtil.GetLineNumber
                            SnapshotUtil.GetLine x.CurrentSnapshot number |> SnapshotLineUtil.GetEndIncludingLineBreak
                    x.PutCore point stringData operationKind moveCaretAfterText

                    EditSpan.Block col, OperationKind.CharacterWise)

        // Update the register with the deleted text
        let value = { Value = StringData.OfEditSpan deletedSpan; OperationKind = operationKind }
        _registerMap.SetRegisterValue register RegisterOperation.Delete value 

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
                    _textBuffer.Delete(span.Span) |> ignore
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
                // command succeeds.  We do want any actions performed in the linked commands
                // to remain linked so do this inside of an edit transaction
                x.EditWithUndoTransaciton "LinkedCommand" (fun () ->
                    match repeat command1 repeatData with
                    | CommandResult.Error -> CommandResult.Error
                    | CommandResult.Completed _ -> repeat command2 None)

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
                        if keyInput = KeyInputUtil.EnterKey then EditUtil.NewLine
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
            if keyInput = KeyInputUtil.EnterKey then EditUtil.NewLine
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
        | NormalCommand.FoldMotion motion -> x.RunWithMotion motion x.FoldMotion
        | NormalCommand.FormatLines -> x.FormatLines count
        | NormalCommand.FormatMotion motion -> x.RunWithMotion motion x.FormatMotion 
        | NormalCommand.GoToFileUnderCaret useNewWindow -> x.GoToFileUnderCaret useNewWindow
        | NormalCommand.GoToView direction -> x.GoToView direction
        | NormalCommand.InsertAfterCaret -> x.InsertAfterCaret count
        | NormalCommand.InsertBeforeCaret -> x.InsertBeforeCaret count
        | NormalCommand.InsertAtEndOfLine -> x.InsertAtEndOfLine count
        | NormalCommand.InsertAtFirstNonBlank -> x.InsertAtFirstNonBlank count
        | NormalCommand.InsertAtStartOfLine -> x.InsertAtStartOfLine count
        | NormalCommand.InsertLineAbove -> x.InsertLineAbove count
        | NormalCommand.InsertLineBelow -> x.InsertLineBelow count
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
        | NormalCommand.SplitViewHorizontally -> x.SplitViewHorizontally()
        | NormalCommand.SplitViewVertically -> x.SplitViewVertically()
        | NormalCommand.RepeatLastCommand -> x.RepeatLastCommand data
        | NormalCommand.ReplaceChar keyInput -> x.ReplaceChar keyInput data.CountOrDefault
        | NormalCommand.Yank motion -> x.RunWithMotion motion (x.YankMotion register)

    /// Run a VisualCommand against the buffer
    member x.RunVisualCommand command (data : CommandData) (visualSpan : VisualSpan) = 

        // Clear the selection before actually running any Visual Commands.  Selection is one 
        // of the items which is preserved along with caret position when we use an edit transaction
        // with the change primitives (EditWithUndoTransaction).  We don't want the selection to 
        // reappear during an undo hence clear it now so it's gone.
        _textView.Selection.Clear()

        let register = _registerMap.GetRegister data.RegisterNameOrDefault
        let count = data.CountOrDefault
        match command with
        | VisualCommand.ChangeCase kind -> x.ChangeCaseVisual kind visualSpan
        | VisualCommand.ChangeSelection -> x.DeleteSelection register visualSpan (ModeSwitch.SwitchMode ModeKind.Insert)
        | VisualCommand.ChangeLineSelection specialCaseBlock -> x.ChangeLineSelection register visualSpan specialCaseBlock
        | VisualCommand.DeleteSelection -> x.DeleteSelection register visualSpan ModeSwitch.SwitchPreviousMode
        | VisualCommand.DeleteLineSelection -> x.DeleteLineSelection register visualSpan
        | VisualCommand.FormatLines -> x.FormatLinesVisual visualSpan
        | VisualCommand.FoldSelection -> x.FoldSelection visualSpan
        | VisualCommand.JoinSelection kind -> x.JoinSelection kind visualSpan
        | VisualCommand.PutOverSelection moveCaretAfterText -> x.PutOverSelection register count moveCaretAfterText visualSpan 
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
            let targetCaretPosition = visualSpan.Start.Position

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
            let targetCaretPosition = visualSpan.Start.Position 

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

    /// Split the view horizontally
    member x.SplitViewHorizontally () = 
        match _vimHost.SplitViewHorizontally _textView with
        | HostResult.Success -> ()
        | HostResult.Error _ -> _operations.Beep()

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Split the view vertically
    member x.SplitViewVertically () =
        match _vimHost.SplitViewVertically _textView with
        | HostResult.Success -> ()
        | HostResult.Error _ -> _operations.Beep()

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

