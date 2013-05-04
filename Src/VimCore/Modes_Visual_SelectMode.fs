namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Vim
open Vim.Modes

type internal SelectMode
    (
        _vimBufferData : IVimBufferData,
        _visualKind : VisualKind,
        _commonOperations : ICommonOperations,
        _undoRedoOperations : IUndoRedoOperations,
        _selectionTracker : ISelectionTracker
    ) = 

    let _globalSettings = _vimBufferData.LocalSettings.GlobalSettings
    let _vimTextBuffer = _vimBufferData.VimTextBuffer
    let _textBuffer = _vimBufferData.TextBuffer
    let _textView = _vimBufferData.TextView
    let _modeKind = 
        match _visualKind with
        | VisualKind.Character -> ModeKind.SelectCharacter
        | VisualKind.Line -> ModeKind.SelectLine
        | VisualKind.Block -> ModeKind.SelectBlock

    /// A 'special key' is defined in :help keymodel as any of the following keys.  Depending
    /// on the value of the keymodel setting they can affect the selection
    static let GetCaretMovement (keyInput : KeyInput) =
        match keyInput.Key with
        | VimKey.Up -> Some CaretMovement.Up
        | VimKey.Right -> Some CaretMovement.Right
        | VimKey.Down -> Some CaretMovement.Down
        | VimKey.Left -> Some CaretMovement.Left
        | VimKey.Home -> Some CaretMovement.Home
        | VimKey.End -> Some CaretMovement.End
        | VimKey.PageUp -> Some CaretMovement.PageUp
        | VimKey.PageDown -> Some CaretMovement.PageDown
        | _ -> None

    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    member x.CaretVirtualPoint = TextViewUtil.GetCaretVirtualPoint _textView

    member x.CaretLine = TextViewUtil.GetCaretLine _textView

    member x.CurrentSnapshot = _textView.TextSnapshot

    member x.ProcessCaretMovement caretMovement (keyInput : KeyInput) = 
        _commonOperations.MoveCaret caretMovement |> ignore

        let hasShift = Util.IsFlagSet keyInput.KeyModifiers KeyModifiers.Shift
        if not hasShift && Util.IsFlagSet _globalSettings.KeyModelOptions KeyModelOptions.StopSelection then
            ProcessResult.Handled ModeSwitch.SwitchPreviousMode
        else
            // The caret moved so we need to update the selection 
            _selectionTracker.UpdateSelection()
            ProcessResult.Handled ModeSwitch.NoSwitch

    /// The user hit an input key.  Need to replace the current selection with the given 
    /// text and put the caret just after the insert.  This needs to be a single undo 
    /// transaction 
    member x.ProcessInput text = 

        // During undo we don't want the currently selected text to be reselected as that
        // would put the editor back into select mode.  Clear the selection now so that
        // it's not recorderd in the undo transaction and move the caret to the selection
        // start
        let span = _textView.Selection.StreamSelectionSpan.SnapshotSpan
        _textView.Selection.Clear()
        TextViewUtil.MoveCaretToPoint _textView span.Start

        _undoRedoOperations.EditWithUndoTransaction "Replace" (fun () -> 

            use edit = _textBuffer.CreateEdit()

            // First step is to replace the deleted text with the new one
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
            else
                match GetCaretMovement keyInput with
                | Some caretMovement -> x.ProcessCaretMovement caretMovement keyInput
                | None -> 
                    if Option.isSome keyInput.RawChar then
                        x.ProcessInput (StringUtil.ofChar keyInput.Char)
                    else
                        ProcessResult.Handled ModeSwitch.NoSwitch

        if processResult.IsAnySwitch then
            _textView.Selection.Clear()
            _textView.Selection.Mode <- TextSelectionMode.Stream

        processResult

    member x.OnEnter modeArgument = 
        match modeArgument with
        | ModeArgument.InitialVisualSelection (visualSelection, caretPoint) ->

            if visualSelection.VisualKind = _visualKind then
                visualSelection.Select _textView
                let visualCaretPoint = visualSelection.GetCaretPoint _globalSettings.SelectionKind
                TextViewUtil.MoveCaretToPointRaw _textView visualCaretPoint MoveCaretFlags.EnsureOnScreen
        | _ -> ()

        _selectionTracker.Start()
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
        member x.ModeKind = _modeKind
        member x.CanProcess _ = true
        member x.Process keyInput =  x.Process keyInput
        member x.OnEnter modeArgument = x.OnEnter modeArgument
        member x.OnLeave () = x.OnLeave()
        member x.OnClose() = x.OnClose()

