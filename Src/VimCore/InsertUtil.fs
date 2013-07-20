﻿
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining

/// This type houses the functionality for many of the insert mode commands
type internal InsertUtil
    (
        _vimBufferData : IVimBufferData,
        _operations : ICommonOperations
    ) =

    let _textView = _vimBufferData.TextView
    let _textBuffer = _textView.TextBuffer
    let _localSettings = _vimBufferData.LocalSettings
    let _globalSettings = _localSettings.GlobalSettings
    let _undoRedoOperations = _vimBufferData.UndoRedoOperations
    let _editorOperations = _operations.EditorOperations
    let _editorOptions = _textView.Options
    let _wordUtil = _vimBufferData.WordUtil
    let _vimHost = _vimBufferData.Vim.VimHost

    /// The column of the caret
    member x.CaretColumn = SnapshotPointUtil.GetColumn x.CaretPoint

    /// The SnapshotPoint for the caret
    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    /// The VirtualSnapshotPoint for the caret
    member x.CaretVirtualPoint = TextViewUtil.GetCaretVirtualPoint _textView

    /// The ITextSnapshotLine for the caret
    member x.CaretLine = TextViewUtil.GetCaretLine _textView

    /// The line number for the caret
    member x.CaretLineNumber = x.CaretLine.LineNumber

    /// The SnapshotLineRange for the caret line
    member x.CaretLineRange = x.CaretLine |> SnapshotLineRangeUtil.CreateForLine

    /// The SnapshotPoint and ITextSnapshotLine for the caret
    member x.CaretPointAndLine = TextViewUtil.GetCaretPointAndLine _textView

    /// The current ITextSnapshot instance for the ITextBuffer
    member x.CurrentSnapshot = _textBuffer.CurrentSnapshot

    /// Run the specified action with a wrapped undo transaction.  This is often necessary when
    /// an edit command manipulates the caret
    member x.EditWithUndoTransaction<'T> (name : string) (action : unit -> 'T) : 'T = 
        _undoRedoOperations.EditWithUndoTransaction name action

    /// Used for the several commands which make an edit here and need the edit to be linked
    /// with the next insert mode change.  
    member x.EditWithLinkedChange name action =
        let transaction = _undoRedoOperations.CreateLinkedUndoTransaction()

        try
            x.EditWithUndoTransaction name action
        with
            | _ ->
                // If the above throws we can't leave the transaction open else it will
                // break undo / redo in the ITextBuffer.  Close it here and
                // re-raise the exception
                transaction.Dispose()
                reraise()

    /// Apply the TextChange to the given ITextEdit at the specified position.  This will
    /// return the position of the edit after the operation completes.  None is returned
    /// if the edit cannot be completed 
    member x.ApplyTextChangeCore (textEdit : ITextEdit) (position : int) (bounds : Span) (textChange : TextChange) addNewLines =

        let textChange = textChange.Reduce

        // Note that when doing edits in this method that all delete and insert need to be 
        // calculated against the original layout.  For example if you have insert "dog" 
        // followed by delete right 3, the delete right should occur from the original 
        // start position.  This is just how ITextEdit works 
        //
        // After a TextChange structure is reduced we should be able to break it down into
        // the 3 components that we care about: new text, delete left, delete right 
        // and perform those actions independently
        let insertText = ref ""
        let deleteLeft = ref 0
        let deleteRight = ref 0 
        let caretPosition = ref position

        let rec build textChange = 
            match textChange with
            | TextChange.Insert text ->
                let text = 
                    if addNewLines then
                        let newLine = _operations.GetNewLineText x.CaretPoint
                        newLine + text
                    else 
                        text
                insertText.Value <- insertText.Value + text
                caretPosition.Value <- caretPosition.Value + text.Length
            | TextChange.DeleteRight count -> 
                deleteRight.Value <- deleteRight.Value + count
            | TextChange.DeleteLeft count -> 
                deleteLeft.Value <- deleteLeft.Value + count
                caretPosition.Value <- caretPosition.Value - count
            | TextChange.Combination (left, right) ->
                build left
                build right
        build textChange

        let mutable error = false

        if not (StringUtil.isNullOrEmpty insertText.Value) then
            if not (textEdit.Insert(position, insertText.Value)) then
                error <- true

        if deleteRight.Value > 0 then

            // Delete 'count * deleteCount' more characters.  Make sure that we don't
            // go off the end of the specified bounds
            let maxCount = bounds.End - position
            let realCount = min maxCount deleteRight.Value
            if not (textEdit.Delete(position, realCount)) then
                error <- true


        if deleteLeft.Value > 0 then
            // Delete 'count' characters to the left of the caret.  Make sure that
            // we don't delete outside the specified bounds
            let start, count = 
                let diff = position - deleteLeft.Value
                if diff < bounds.Start then
                    bounds.Start, position - bounds.Start
                else
                    diff, deleteLeft.Value

            if not (textEdit.Delete(start, count)) then
                error <- true

        if error then
            None
        else
            Some caretPosition.Value

    /// Apply the TextChange to the ITextBuffer 'count' times as a single operation.
    member x.ApplyTextChange textChange addNewLines =

        use textEdit = _textBuffer.CreateEdit()
        let bounds = Span(0, x.CurrentSnapshot.Length)
        match x.ApplyTextChangeCore textEdit x.CaretPoint.Position bounds textChange addNewLines with
        | Some position -> 
            let snapshot = textEdit.Apply()
            TextViewUtil.MoveCaretToPosition _textView position
        | None -> textEdit.Cancel()

    /// Apply the given TextChange to the specified BlockSpan 
    member x.ApplyBlockInsert (text : string) startLineNumber spaces height = 

        // Don't edit past the end of the ITextBuffer 
        let height = 
            let maxHeight = x.CurrentSnapshot.LineCount - startLineNumber
            min maxHeight height

        // It is possible that a repeat of a block edit will begin part of the way through
        // a wide character (think a 4 space tab).  If any edits have this behavior the first
        // pass will break them up into the appropriate number of spaces
        let fixOverlapEdits () = 
            use textEdit = _textBuffer.CreateEdit()

            let currentSnapshot = x.CurrentSnapshot
            for i = 0 to (height - 1) do 

                let lineNumber = startLineNumber + i
                let currentLine = SnapshotUtil.GetLine currentSnapshot lineNumber
                let point = SnapshotLineUtil.GetSpaceWithOverlapOrEnd currentLine spaces _localSettings.TabStop
                if point.SpacesBefore > 0 then
                    let text = StringUtil.repeatChar point.Width ' '
                    let span = Span(point.Point.Position, 1)
                    textEdit.Replace(span, text) |> ignore

            if textEdit.HasEffectiveChanges then 
                textEdit.Apply() |> ignore
            else
                textEdit.Cancel()

        // Actually apply the edit to the lines in the block selection
        let doApply () = 
            use textEdit = _textBuffer.CreateEdit()
            let mutable abortChange = false

            let currentSnapshot = x.CurrentSnapshot
            for i = 0 to (height - 1) do

                let lineNumber = startLineNumber + i
                let currentLine = SnapshotUtil.GetLine currentSnapshot lineNumber

                // Only apply the edit to lines which were included in the original selection
                let point = SnapshotLineUtil.GetSpaceOrEnd currentLine spaces _localSettings.TabStop
                if not (SnapshotPointUtil.IsInsideLineBreak point) then
                    let position = point.Position
                    if not (textEdit.Insert(position, text)) then
                        abortChange <- true

            if abortChange then
                textEdit.Cancel()
            else
                textEdit.Apply() |> ignore

        fixOverlapEdits ()
        doApply ()

    /// Delete the character before the cursor
    ///
    /// TODO: This needs to respect the 'backspace' option
    member x.Back () = 
        _editorOperations.Backspace() |> ignore
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Block insert the specified text at the caret point over the specified number of lines
    member x.BlockInsert text height = 
        let line, column = SnapshotPointUtil.GetLineColumn x.CaretPoint
        let spaces = SnapshotLineUtil.GetSpacesToColumn x.CaretLine column _localSettings.TabStop
        x.ApplyBlockInsert text line spaces height 
        CommandResult.Completed ModeSwitch.NoSwitch

    member x.Combined left right =
        x.RunInsertCommand left |> ignore
        x.RunInsertCommand right

    /// Complete the insert mode session.
    member x.CompleteMode moveCaretLeft = 

        // If the caret is in virtual space we move regardless of the flag.
        let virtualPoint = TextViewUtil.GetCaretVirtualPoint _textView
        if virtualPoint.IsInVirtualSpace then 
            _operations.MoveCaretToPoint virtualPoint.Position
            CommandResult.Completed ModeSwitch.NoSwitch
        elif moveCaretLeft then 
            x.MoveCaret Direction.Left
        else
            CommandResult.Completed ModeSwitch.NoSwitch

    /// Delete the character under the cursor
    member x.Delete () = 
        _editorOperations.Delete() |> ignore
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Delete count characters to the left of the caret
    member x.DeleteLeft count = 
        let span = 
            let diff = x.CaretPoint.Position - count
            if diff < 0 then
                let count = diff + count
                Span(0, count)
            else
                Span(diff, count)

        _textBuffer.Delete(span) |> ignore
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Delete count characters to the right of the caret
    member x.DeleteRight count = 
        let start = x.CaretPoint.Position
        let maxCount = x.CurrentSnapshot.Length - start
        let realCount = min maxCount count
        let span = Span(start, realCount)
        _textBuffer.Delete(span) |> ignore
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Delete all of the indentation on the current line.  This should not affect caret
    /// position
    member x.DeleteAllIndent () =

        let indentSpan = 
            let endPoint = SnapshotLineUtil.GetFirstNonBlankOrEnd x.CaretLine
            SnapshotSpan(x.CaretLine.Start, endPoint)

        _textBuffer.Delete(indentSpan.Span) |> ignore

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Delete the word before the cursor
    ///
    /// TODO: This needs to respect the IsBackspaceStart sub option. 
    member x.DeleteWordBeforeCursor () =

        // Called when the caret is positioned at the start of the line.  The line break 
        // should be deleted and the caret positioned at the end of the previous line
        let deleteLineBreak () = 
            if x.CaretLineNumber = 0 || not _globalSettings.IsBackspaceEol then
                _operations.Beep()
            else
                x.EditWithUndoTransaction "Delete Word Before Cursor" (fun () ->
                    let line = SnapshotUtil.GetLine x.CurrentSnapshot (x.CaretLineNumber - 1)
                    let span = Span.FromBounds(line.End.Position, line.EndIncludingLineBreak.Position)
                    _textBuffer.Delete span |> ignore
                    TextViewUtil.MoveCaretToPosition _textView line.End.Position)

        // To delete the word we need to first categorize the type of delete we are doing. This
        // is judged by the content of the caret
        if x.CaretPoint = x.CaretLine.Start then
            deleteLineBreak()
        else
            // Need to find the start of the previous word and delete from that to the original 
            // caret point
            let start = 

                // Jump past any blanks before the caret
                let searchEndPoint = 
                    SnapshotSpan(x.CaretLine.Start, x.CaretPoint)
                    |> SnapshotSpanUtil.GetPoints Path.Backward
                    |> Seq.skipWhile SnapshotPointUtil.IsBlank
                    |> SeqUtil.headOrDefault x.CaretLine.Start

                match _wordUtil.GetFullWordSpan WordKind.NormalWord searchEndPoint with
                | None -> searchEndPoint
                | Some span -> span.Start

            // Delete the span and position the caret at it's original start
            x.EditWithUndoTransaction "Delete Word Before Cursor" (fun () ->
                let span = SnapshotSpan(start, x.CaretPoint)
                _textBuffer.Delete span.Span |> ignore
                TextViewUtil.MoveCaretToPosition _textView span.Start.Position)

        CommandResult.Completed ModeSwitch.NoSwitch

    member x.DirectInsert (c : char) = 
        let text = c.ToString()
        if _editorOperations.InsertText(text) then
            CommandResult.Completed ModeSwitch.NoSwitch
        else
            CommandResult.Error

    /// Do a replacement under the caret of the specified char
    member x.DirectReplace (c : char) =
        // Typically we have the overwrite option set for all of replace mode so this
        // is a redundant check.  But during repeat we only see the commands and not
        // the mode changes so we need to check here
        let oldValue = EditorOptionsUtil.GetOptionValueOrDefault _editorOptions DefaultTextViewOptions.OverwriteModeId true
        if not oldValue then
            EditorOptionsUtil.SetOptionValue _editorOptions DefaultTextViewOptions.OverwriteModeId true

        try
            x.DirectInsert c
        finally 
            if not oldValue then
                EditorOptionsUtil.SetOptionValue _editorOptions DefaultTextViewOptions.OverwriteModeId false

    member x.InsertCharacterCore msg lineNumber =
        match SnapshotUtil.TryGetPointInLine _textBuffer.CurrentSnapshot lineNumber x.CaretColumn with
        | None -> 
            _operations.Beep()
            CommandResult.Error
        | Some point -> 
            let text = SnapshotPointUtil.GetChar point |> StringUtil.ofChar
            x.EditWithUndoTransaction "Insert Character Above" (fun () ->
                let position = x.CaretPoint.Position
                _textBuffer.Insert(position, text) |> ignore
                TextViewUtil.MoveCaretToPosition _textView (position + 1))
            CommandResult.Completed ModeSwitch.NoSwitch

    /// Insert the character immediately above the caret
    member x.InsertCharacterAboveCaret() = 
        x.InsertCharacterCore "Insert Character Above" (x.CaretLineNumber - 1)

    /// Insert the character immediately below the caret
    member x.InsertCharacterBelowCaret() = 
        x.InsertCharacterCore "Insert Character Below" (x.CaretLineNumber + 1)

    /// Insert a new line into the ITextBuffer.  Make sure to indent the text
    member x.InsertNewLine() =
        let newLineText = _operations.GetNewLineText x.CaretPoint
        x.EditWithUndoTransaction "New Line" (fun () -> 
            let contextLine = x.CaretLine
            _textBuffer.Insert(x.CaretPoint.Position, newLineText) |> ignore

            // Now we need to position the caret within the new line
            let newLine = SnapshotUtil.GetLine x.CurrentSnapshot (contextLine.LineNumber + 1)
            match _operations.GetNewLineIndent contextLine newLine with
            | None -> ()
            | Some indent ->
                // Calling GetNewLineIndent can cause a buffer edit.  Need to rebind all of the
                // snapshot related items
                match SnapshotUtil.TryGetLine x.CurrentSnapshot newLine.LineNumber with
                | None -> ()
                | Some newLine ->
                    // If there is actual text on this new line (enter in the middle of the
                    // line) then we need to insert white space.  Else we just put the caret
                    // into virtual space
                    let indentText = StringUtil.repeat indent " " |> _operations.NormalizeBlanks
                    if indentText.Length > 0 && newLine.Length > 0 then
                        _textBuffer.Insert(newLine.Start.Position, indentText) |> ignore
                        TextViewUtil.MoveCaretToPosition _textView (newLine.Start.Position + indentText.Length)
                    else
                        let virtualPoint = VirtualSnapshotPoint(newLine.Start, indent)
                        TextViewUtil.MoveCaretToVirtualPoint _textView virtualPoint)

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Insert a single tab into the ITextBuffer.  If 'expandtab' is enabled then insert
    /// the appropriate number of spaces
    member x.InsertTab () =

        x.EditWithUndoTransaction "Insert Tab" (fun () -> 

            let text = 
                if _localSettings.ExpandTab then
                    // When inserting spaces we need to consider the number of spaces to the caret.
                    // If it's a multiple of the tab stop then we insert a full tab.  Else we insert
                    // what it takes to get to the multiple
                    let count = 
                        let spaces = _operations.GetSpacesToPoint x.CaretPoint
                        let remainder = spaces % _localSettings.TabStop
                        if remainder = 0 then
                            _localSettings.TabStop
                        else
                            _localSettings.TabStop - remainder

                    StringUtil.repeatChar count ' '
                else
                    "\t"

            let position = x.CaretPoint.Position + text.Length
            _textBuffer.Insert(x.CaretPoint.Position, text) |> ignore

            // Move the caret to the end of the insertion
            let point = SnapshotPoint(x.CurrentSnapshot, position)
            _operations.MoveCaretToPoint point)

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Insert the specified text into the ITextBuffer at the current caret position and then move
    /// the cursor to the end of the insert
    member x.InsertText (text : string)=

        x.EditWithUndoTransaction "Insert Text" (fun () -> 

            let position = x.CaretPoint.Position + text.Length
            _textBuffer.Insert(x.CaretPoint.Position, text) |> ignore

            // Move the caret to the end of the insertion in the current ITextSnapshot
            let point = SnapshotPoint(x.CurrentSnapshot, position)
            _operations.MoveCaretToPoint point)

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Move the caret in the given direction
    member x.MoveCaret direction = 
        let caretMovement = CaretMovement.OfDirection direction
        if _operations.MoveCaret caretMovement then
            CommandResult.Completed ModeSwitch.NoSwitch
        else
            _operations.Beep()
            CommandResult.Error

    member x.MoveCaretByWord direction = 
        let moveLeft () = 
            if x.CaretPoint.Position > x.CaretLine.Start.Position then
                let point = 
                    let defaultIfNotFound = SnapshotUtil.GetStartPoint x.CurrentSnapshot
                    _wordUtil.GetWords WordKind.NormalWord Path.Backward x.CaretPoint
                    |> Seq.map SnapshotSpanUtil.GetStartPoint
                    |> SeqUtil.headOrDefault defaultIfNotFound
                _operations.MoveCaretToPointAndEnsureVisible point
                CommandResult.Completed ModeSwitch.NoSwitch
            else
                _operations.Beep()
                CommandResult.Error

        let moveRight () = 
            if x.CaretPoint.Position < x.CaretLine.End.Position then
                let point = 
                    let defaultIfNotFound = SnapshotUtil.GetStartPoint x.CurrentSnapshot
                    _wordUtil.GetWords WordKind.NormalWord Path.Forward x.CaretPoint
                    |> Seq.skip 1
                    |> Seq.map SnapshotSpanUtil.GetStartPoint
                    |> SeqUtil.headOrDefault defaultIfNotFound
                _operations.MoveCaretToPointAndEnsureVisible point
                CommandResult.Completed ModeSwitch.NoSwitch
            else
                _operations.Beep()
                CommandResult.Error

        match direction with
        | Direction.Left -> moveLeft()
        | Direction.Right -> moveRight()
        | _ -> CommandResult.Error

    /// Repeat the given edit InsertCommand.  This is used at the exit of insert mode to
    /// apply the edits again and again
    member x.RepeatEdit textChange addNewLines count = 

        // Create a transaction so the textChange is applied as a single edit and to 
        // maintain caret position 
        _undoRedoOperations.EditWithUndoTransaction "Repeat Edits" (fun () -> 

            for i = 1 to count do
                x.ApplyTextChange textChange addNewLines)

    /// Repeat the edits for the other lines in the block span.
    member x.RepeatBlock (insertCommand : InsertCommand) (blockSpan : BlockSpan) =

        // Unfortunately the ITextEdit implementation doesn't properly reduce the changes that are
        // applied to it.  For example if you add 2 characters, delete them and then insert text
        // at the original start point you will not end up with simply the new text.  
        //
        // To account for this we reduce the TextChange to the smallest possible TextChange value
        // and apply that change 
        //
        // Block edits don't apply if the user inserts a new line into the buffer.  Check for that
        // early on
        let isRepeatable = blockSpan.Snasphot.LineCount = x.CurrentSnapshot.LineCount && blockSpan.Height > 1 
        match isRepeatable, insertCommand.TextChange _editorOptions |> Option.map (fun textChange -> textChange.Reduce) with
        | true, Some textChange -> 
            match textChange with
            | TextChange.Insert text -> 
                x.EditWithUndoTransaction "Repeat Block Edit" (fun () ->
                    let startLineNumber = (SnapshotPointUtil.GetLineNumber blockSpan.Start) + 1
                    x.ApplyBlockInsert text startLineNumber blockSpan.ColumnSpaces (blockSpan.Height - 1)

                    // insertion point which is the start of the BlockSpan.
                    match TrackingPointUtil.GetPointInSnapshot blockSpan.Start PointTrackingMode.Negative x.CurrentSnapshot with
                    | None -> ()
                    | Some point -> TextViewUtil.MoveCaretToPoint _textView point
                    
                    Some text)
            | _ -> None
        | _ -> None

    member x.RunInsertCommandCore command addNewLines = 

        // Allow the host to custom process this message here.
        if _vimHost.TryCustomProcess _textView command then
            CommandResult.Completed ModeSwitch.NoSwitch
        else
            match command with
            | InsertCommand.Back -> x.Back()
            | InsertCommand.BlockInsert (text, count) -> x.BlockInsert text count
            | InsertCommand.Combined (left, right) -> x.Combined left right
            | InsertCommand.CompleteMode moveCaretLeft -> x.CompleteMode moveCaretLeft
            | InsertCommand.Delete -> x.Delete()
            | InsertCommand.DeleteLeft count -> x.DeleteLeft count
            | InsertCommand.DeleteRight count -> x.DeleteRight count
            | InsertCommand.DeleteAllIndent -> x.DeleteAllIndent() 
            | InsertCommand.DeleteWordBeforeCursor -> x.DeleteWordBeforeCursor()
            | InsertCommand.DirectInsert c -> x.DirectInsert c
            | InsertCommand.DirectReplace c -> x.DirectReplace c
            | InsertCommand.InsertCharacterAboveCaret -> x.InsertCharacterAboveCaret()
            | InsertCommand.InsertCharacterBelowCaret -> x.InsertCharacterBelowCaret()
            | InsertCommand.InsertNewLine -> x.InsertNewLine()
            | InsertCommand.InsertTab -> x.InsertTab()
            | InsertCommand.InsertText text -> x.InsertText text
            | InsertCommand.MoveCaret direction -> x.MoveCaret direction
            | InsertCommand.MoveCaretByWord direction -> x.MoveCaretByWord direction
            | InsertCommand.ShiftLineLeft -> x.ShiftLineLeft ()
            | InsertCommand.ShiftLineRight -> x.ShiftLineRight ()

    member x.RunInsertCommand command = 
        x.RunInsertCommandCore command false

    /// Shift the caret line one 'shiftwidth' to the left.  This is different than 
    /// both normal and visual mode shifts because it will round up the blanks to
    /// a 'shiftwidth' before indenting
    member x.ShiftLineLeft () =
        let indentSpan = SnapshotLineUtil.GetIndentSpan x.CaretLine

        if indentSpan.Length > 0 || x.CaretVirtualPoint.IsInVirtualSpace then
            let isBlankLine = SnapshotLineUtil.IsBlankOrEmpty x.CaretLine

            let spaces = 
                let spaces = _operations.NormalizeBlanksToSpaces (indentSpan.GetText())

                // Make sure to account for the caret being in virtual space.  This simply
                // adds extra spaces to the line equal to the number of virtual spaces
                if x.CaretVirtualPoint.IsInVirtualSpace then
                    let extra = StringUtil.repeatChar x.CaretVirtualPoint.VirtualSpaces ' '
                    spaces + extra
                else
                    spaces

            let trim = 
                let remainder = spaces.Length % _localSettings.ShiftWidth
                if remainder = 0 then
                    _localSettings.ShiftWidth
                else
                    remainder
            let indent = 
                let spaces = spaces.Substring(0, spaces.Length - trim)
                _operations.NormalizeBlanks spaces
            _textBuffer.Replace(indentSpan.Span, indent) |> ignore
            if isBlankLine then
                // the line is now all spaces, move it to the end
                TextViewUtil.MoveCaretToPoint _textView x.CaretLine.End

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Shift the carte line one 'shiftwidth' to the right
    member x.ShiftLineRight () =
        let range = TextViewUtil.GetCaretLineRange _textView 1
        _operations.ShiftLineRangeRight range 1
        CommandResult.Completed ModeSwitch.NoSwitch

    member x.ExtraTextChange textChange addNewLines = 
        x.ApplyTextChange textChange addNewLines 
        CommandResult.Completed ModeSwitch.NoSwitch

    interface IInsertUtil with

        member x.RunInsertCommand command = x.RunInsertCommand command
        member x.RepeatEdit textChange addNewLines count = x.RepeatEdit textChange addNewLines count
        member x.RepeatBlock command blockSpan = x.RepeatBlock command blockSpan

