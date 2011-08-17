
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Outlining

/// This type houses the functionality for many of the insert mode commands
type internal InsertUtil
    (
        _bufferData : VimBufferData,
        _operations : ICommonOperations
    ) =

    let _textView = _bufferData.TextView
    let _textBuffer = _textView.TextBuffer
    let _localSettings = _bufferData.LocalSettings
    let _globalSettings = _localSettings.GlobalSettings
    let _undoRedoOperations = _bufferData.UndoRedoOperations
    let _editorOperations = _operations.EditorOperations

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
    member x.EditWithUndoTransaciton<'T> (name : string) (action : unit -> 'T) : 'T = 
        _undoRedoOperations.EditWithUndoTransaction name action

    /// Delete the character before the cursor
    member x.Back () = 
        _editorOperations.Backspace() |> ignore
        CommandResult.Completed ModeSwitch.NoSwitch

    member x.Combined left right =
        x.RunInsertCommand left |> ignore
        x.RunInsertCommand right

    /// Delete the character under the cursor
    member x.Delete () = 
        _editorOperations.Delete() |> ignore
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Delete all of the indentation on the current line.  This should not affect caret
    /// position
    member x.DeleteAllIndent () =

        let indentSpan = 
            let endPoint = SnapshotLineUtil.GetFirstNonBlankOrEnd x.CaretLine
            SnapshotSpan(x.CaretLine.Start, endPoint)

        _textBuffer.Delete(indentSpan.Span) |> ignore

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Insert a new line into the ITextBuffer
    member x.InsertNewLine() =
        _textBuffer.Insert(x.CaretPoint.Position, System.Environment.NewLine) |> ignore
        CommandResult.Completed ModeSwitch.NoSwitch

    /// Insert a single tab into the ITextBuffer.  If 'expandtab' is enabled then insert
    /// the appropriate number of spaces
    member x.InsertTab () =

        x.EditWithUndoTransaciton "Insert Tab" (fun () -> 

            let text = 
                if _localSettings.ExpandTab then
                    StringUtil.repeatChar _localSettings.TabStop ' '
                else
                    "\t"

            let position = x.CaretPoint.Position + text.Length
            _textBuffer.Insert(x.CaretPoint.Position, text) |> ignore

            // Move the caret to the end of the insertion
            let point = SnapshotPoint(x.CurrentSnapshot, position)
            _operations.MoveCaretToPoint point)

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Move the caret in the given direction
    member x.MoveCaret direction = 

        /// Move the caret up
        let moveUp () =
            match SnapshotUtil.TryGetLine x.CurrentSnapshot (x.CaretLine.LineNumber - 1) with
            | None ->
                _operations.Beep()
                CommandResult.Error
            | Some line ->
                _editorOperations.MoveLineUp(false);
                CommandResult.Completed ModeSwitch.NoSwitch

        /// Move the caret down
        let moveDown () =
            match SnapshotUtil.TryGetLine x.CurrentSnapshot (x.CaretLine.LineNumber + 1) with
            | None ->
                _operations.Beep()
                CommandResult.Error
            | Some line ->
                _editorOperations.MoveLineDown(false);
                CommandResult.Completed ModeSwitch.NoSwitch
    
        /// Move the caret left.  Don't go past the start of the line 
        let moveLeft () = 
            if x.CaretLine.Start.Position < x.CaretPoint.Position then
                let point = SnapshotPointUtil.SubtractOne x.CaretPoint
                _operations.MoveCaretToPointAndEnsureVisible point
                CommandResult.Completed ModeSwitch.NoSwitch
            else
                _operations.Beep()
                CommandResult.Error

        /// Move the caret right.  Don't go off the end of the line
        let moveRight () =
            if x.CaretPoint.Position < x.CaretLine.End.Position then
                let point = SnapshotPointUtil.AddOne x.CaretPoint
                _operations.MoveCaretToPointAndEnsureVisible point
                CommandResult.Completed ModeSwitch.NoSwitch
            else
                _operations.Beep()
                CommandResult.Error

        match direction with
        | Direction.Up -> moveUp()
        | Direction.Down -> moveDown()
        | Direction.Left -> moveLeft()
        | Direction.Right -> moveRight()

    /// Repeat the given edit InsertCommand.  This is used at the exit of insert mode to
    /// apply the edits again and again
    member x.RepeatEdit command addNewLines count = 

        // Create a transaction so the textChange is applied as a single edit and to 
        // maintain caret position 
        _undoRedoOperations.EditWithUndoTransaction "Repeat Edits" (fun () -> 

            for i = 1 to count do
                x.RunInsertCommandCore command addNewLines |> ignore)

    member x.RunInsertCommandCore command addNewLines = 
        match command with
        | InsertCommand.Back -> x.Back()
        | InsertCommand.Combined (left, right) -> x.Combined left right
        | InsertCommand.Delete -> x.Delete()
        | InsertCommand.DeleteAllIndent -> x.DeleteAllIndent() 
        | InsertCommand.InsertNewLine -> x.InsertNewLine()
        | InsertCommand.InsertTab -> x.InsertTab()
        | InsertCommand.MoveCaret direction -> x.MoveCaret direction
        | InsertCommand.ShiftLineLeft -> x.ShiftLineLeft ()
        | InsertCommand.ShiftLineRight -> x.ShiftLineRight ()
        | InsertCommand.TextChange textChange -> x.TextChange textChange addNewLines

    member x.RunInsertCommand command = 
        x.RunInsertCommandCore command false

    /// Shift the caret line one 'shiftwidth' to the left.  This is different than 
    /// both normal and visual mode shifts because it will round up the blanks to
    /// a 'shiftwidth' before indenting
    member x.ShiftLineLeft () =
        let indentSpan = 
            let endPoint = SnapshotLineUtil.GetFirstNonBlankOrEnd x.CaretLine
            SnapshotSpan(x.CaretLine.Start, endPoint)

        if indentSpan.Length > 0 || x.CaretVirtualPoint.IsInVirtualSpace then
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
                let remainder = spaces.Length % _globalSettings.ShiftWidth
                if remainder = 0 then
                    _globalSettings.ShiftWidth
                else
                    remainder
            let indent = 
                let spaces = spaces.Substring(0, spaces.Length - trim)
                _operations.NormalizeBlanks spaces
            _textBuffer.Replace(indentSpan.Span, indent) |> ignore

        CommandResult.Completed ModeSwitch.NoSwitch

    /// Shift the carte line one 'shiftwidth' to the right
    member x.ShiftLineRight () =
        CommandResult.Error

    member x.TextChange textChange addNewLines = 
        _operations.ApplyTextChange textChange addNewLines 1
        CommandResult.Completed ModeSwitch.NoSwitch

    interface IInsertUtil with

        member x.RunInsertCommand command = x.RunInsertCommand command
        member x.RepeatEdit command addNewLines count = x.RepeatEdit command addNewLines count

