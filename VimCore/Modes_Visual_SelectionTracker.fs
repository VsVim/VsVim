#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
open System.Windows.Threading
open Vim

/// Mode on the selection
type internal SelectionMode =
    | Character = 0
    | Line = 1
    | Block = 2

/// Responsible for tracking and updating the selection while we are in visual mode
type internal SelectionTracker
    (
        _textView : ITextView,
        _mode : SelectionMode ) as this =

    let mutable _anchorPoint = SnapshotPoint(_textView.TextSnapshot, 0) 

    /// TextSelectionMode of the Editor before Start was called
    let mutable _originalSelectionMode = _textView.Selection.Mode

    /// Whether or not we are currently running
    let mutable _running = false

    let mutable _textChangedHandler = Util.CreateToggleHandler (_textView.TextBuffer.Changed)
    let mutable _caretChangedHandler = Util.CreateToggleHandler (_textView.Caret.PositionChanged)

    do 
        _textChangedHandler.Reset((fun _ args -> this.OnTextChanged(args)))
        _caretChangedHandler.Reset((fun _ _ -> this.OnCaretChanged()))

    /// Anchor point being tracked by the selection tracker
    member x.AnchorPoint = _anchorPoint

    member x.SelectedText = 
        let spans = _textView.Selection.SelectedSpans
        if _mode = SelectionMode.Block then
            let builder = System.Text.StringBuilder()
            spans
                |> Seq.map (fun x -> x.GetText())
                |> Seq.reduce (fun acc cur -> acc + System.Environment.NewLine + cur)
        else 
            
            match spans |> Seq.isEmpty with
            | true -> System.String.Empty
            | false -> 
                let head = spans |> Seq.head
                head.GetText()

    /// Call when selection tracking should begin.  Optionally passes in an anchor point for
    /// the selection.  If it's not specified the caret will be used
    member x.Start(anchor : SnapshotPoint option) =
        if _running then invalidOp Vim.Resources.SelectionTracker_AlreadyRunning
        _running <- true
        _textChangedHandler.Add()
        _caretChangedHandler.Add()
        _anchorPoint <- 
            match anchor with
            | Some(point) -> point
            | None -> ViewUtil.GetCaretPoint _textView

        // Update the selection based on our current information
        _originalSelectionMode <- _textView.Selection.Mode
        _textView.Selection.Select(new SnapshotSpan(_anchorPoint, 1), false)
        _textView.Selection.Mode <- 
            match _mode with
            | SelectionMode.Character -> TextSelectionMode.Stream
            | SelectionMode.Line -> TextSelectionMode.Stream
            | SelectionMode.Block -> TextSelectionMode.Box
            | _ -> failwith "Invaild enum value"

    /// Called when selection should no longer be tracked.  Must be paired with Start calls or
    /// we will stay attached to certain event handlers
    member x.Stop() =
        if not _running then invalidOp Resources.SelectionTracker_NotRunning
        _textChangedHandler.Remove()
        _caretChangedHandler.Remove()

        // On teardown we will get calls to Stop when the view is closed.  It's invalid to access 
        // the selection at that point
        if not _textView.IsClosed then
            _textView.Selection.Clear()
            _textView.Selection.Mode <- _originalSelectionMode

        _running <- false

    /// Update the selection based on the current state of the view
    member private x.UpdateSelectionCore() = 
        let selectStandard() = 
            let first = VirtualSnapshotPoint(_anchorPoint)
            let last = _textView.Caret.Position.VirtualBufferPosition
            let last = 
                if first.Position.Position = last.Position.Position then
                    VirtualSnapshotPoint(last.Position.Add(1),last.VirtualSpaces)
                else last
            _textView.Selection.Select(first,last)
        match _mode with
        | SelectionMode.Character -> selectStandard()
        | SelectionMode.Line ->
            let caret = ViewUtil.GetCaretPoint _textView
            if _anchorPoint.Position < caret.Position then 
                let first = _anchorPoint.GetContainingLine().Start
                let last = caret.GetContainingLine().End
                let span = SnapshotSpan(first,last)
                _textView.Selection.Select(span, false)
            else 
                let first = caret.GetContainingLine().Start
                let last = _anchorPoint.GetContainingLine().End
                let span = SnapshotSpan(first,last)
                _textView.Selection.Select(span, false)
        | SelectionMode.Block -> selectStandard()
        | _ -> failwith "Invalid enum value"

    member private x.UpdateSelection() = 
        let func () = 
            if _running then
                x.UpdateSelectionCore()
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new System.Action(func)) |> ignore

    /// When the text is changed it invalidates the anchor point.  It needs to be forwarded to
    /// the next version of the buffer
    member private x.OnTextChanged (args:TextContentChangedEventArgs) =
        let point = _anchorPoint.Snapshot.CreateTrackingPoint(_anchorPoint.Position, PointTrackingMode.Negative)
        try
            _anchorPoint <- point.GetPoint(args.After)
        with
            | :? System.ArgumentOutOfRangeException -> 
                // If it's not present in the new version of the buffer then just go to point 0.
                _anchorPoint <- SnapshotPoint(args.After, 0)
            | e -> reraise()
        x.UpdateSelection()

    member private x.OnCaretChanged()  = x.UpdateSelection()
