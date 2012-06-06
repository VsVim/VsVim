namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Vim
open Vim.Modes

type internal SelectMode
    (
        _vimBufferData : IVimBufferData,
        _commonOperations : ICommonOperations,
        _undoRedoOperations : IUndoRedoOperations,
        _selectionTracker : ISelectionTracker
    ) = 

    let _vimTextBuffer = _vimBufferData.VimTextBuffer
    let _textBuffer = _vimBufferData.TextBuffer
    let _textView = _vimBufferData.TextView

    /// The user hit an input key.  Need to replace the current selection with the given 
    /// text and put the caret just after the insert.  This needs to be a single undo 
    /// transaction 
    member x.ProcessInput text = 
        _undoRedoOperations.EditWithUndoTransaction "Replace" (fun () -> 

            use edit = _textBuffer.CreateEdit()

            // First step is to replace the deleted text with the new one
            let span = _textView.Selection.StreamSelectionSpan.SnapshotSpan
            edit.Delete(span.Span) |> ignore
            edit.Insert(span.End.Position, text) |> ignore

            // Now move the caret past the insert point in the new snapshot. We don't need to 
            // add one here (or even the length of the insert text).  The insert occurred at
            // the exact point we are tracking and we chose PointTrackingMode.Positive so this 
            // will push the point past the insert
            let snapshot = edit.Apply()
            match TrackingPointUtil.GetPointInSnapshot span.End PointTrackingMode.Positive snapshot with
            | None -> ()
            | Some point -> TextViewUtil.MoveCaretToPoint _textView point)

        ProcessResult.Handled (ModeSwitch.SwitchMode ModeKind.Insert)

    member x.Process keyInput = 
        let processResult = 
            if keyInput = KeyInputUtil.EscapeKey then
                ProcessResult.Handled ModeSwitch.SwitchPreviousMode
            elif keyInput = KeyInputUtil.EnterKey then
                let caretPoint = TextViewUtil.GetCaretPoint _textView
                let text = _commonOperations.GetNewLineText caretPoint
                x.ProcessInput text
            elif keyInput.Key = VimKey.Delete || keyInput.Key = VimKey.Back then
                x.ProcessInput ""
            elif Option.isSome keyInput.RawChar then
                x.ProcessInput (StringUtil.ofChar keyInput.Char)
            else
                ProcessResult.Handled ModeSwitch.NoSwitch

        if processResult.IsAnySwitch then
            _textView.Selection.Clear()
            _textView.Selection.Mode <- TextSelectionMode.Stream

        processResult

    member x.OnEnter() = _selectionTracker.Start()
    member x.OnLeave() = _selectionTracker.Stop()
    member x.OnClose() = ()

    member x.SyncSelection() =
        if _selectionTracker.IsRunning then
            _selectionTracker.Stop()
            _selectionTracker.Start()

    interface ISelectMode with
        member x.SyncSelection() = x.SyncSelection()

    interface IMode with
        member x.VimTextBuffer = _vimTextBuffer
        member x.CommandNames = Seq.empty
        member x.ModeKind = ModeKind.Select
        member x.CanProcess _ = true
        member x.Process keyInput =  x.Process keyInput
        member x.OnEnter _ = x.OnEnter()
        member x.OnLeave () = x.OnLeave()
        member x.OnClose() = x.OnClose()

