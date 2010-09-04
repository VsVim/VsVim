#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Vim

/// Responsible for tracking and updating the selection while we are in visual mode
type internal SelectionTracker
    (
        _textView : ITextView,
        _kind : VisualKind ) as this =

    let mutable _anchorPoint = VirtualSnapshotPoint(_textView.TextSnapshot, 0) 

    /// Whether or not we are currently running
    let mutable _running = false

    let mutable _textChangedHandler = ToggleHandler.Empty
    do 
        _textChangedHandler <- ToggleHandler.Create (_textView.TextBuffer.Changed) (fun (args:TextContentChangedEventArgs) -> this.OnTextChanged(args))

    /// Anchor point being tracked by the selection tracker
    member x.AnchorPoint = _anchorPoint

    member x.IsRunning = _running

    /// Call when selection tracking should begin.  
    member x.Start() = 
        if _running then invalidOp Vim.Resources.SelectionTracker_AlreadyRunning
        _running <- true
        _textChangedHandler.Add()

        let selection = _textView.Selection
        if selection.IsEmpty then
            // Do the initial selection update
            let caretPoint = TextViewUtil.GetCaretPoint _textView 
            _anchorPoint <- VirtualSnapshotPoint(caretPoint)
            x.UpdateSelection()
        else 
            _anchorPoint <- selection.AnchorPoint

    /// Called when selection should no longer be tracked.  Must be paired with Start calls or
    /// we will stay attached to certain event handlers
    member x.Stop() =
        if not _running then invalidOp Resources.SelectionTracker_NotRunning
        _textChangedHandler.Remove()

        // On teardown we will get calls to Stop when the view is closed.  It's invalid to access 
        // the selection at that point
        if not _textView.IsClosed then
            _textView.Selection.Clear()
            _textView.Selection.Mode <- TextSelectionMode.Stream

        _running <- false

    /// Update the selection based on the current state of the view
    member x.UpdateSelection() = 
        let desiredMode = 
            match _kind with
            | VisualKind.Character -> TextSelectionMode.Stream
            | VisualKind.Line -> TextSelectionMode.Stream
            | VisualKind.Block -> TextSelectionMode.Box
        if _textView.Selection.Mode <> desiredMode then _textView.Selection.Mode <- desiredMode

        let selectStandard() = 
            let first = _anchorPoint
            let last = _textView.Caret.Position.VirtualBufferPosition
            let last = 
                if first = last then
                    if last.IsInVirtualSpace then VirtualSnapshotPoint(last.Position,last.VirtualSpaces+1)
                    elif last.Position.GetContainingLine().End = last.Position then VirtualSnapshotPoint(last.Position,1)
                    else VirtualSnapshotPoint(last.Position.Add(1))
                else last
            _textView.Selection.Select(first,last)

        match _kind with
        | VisualKind.Character -> selectStandard()
        | VisualKind.Line ->
            let caret = _textView.Caret.Position.VirtualBufferPosition
            let first,last = 
                if VirtualSnapshotPointUtil.GetPosition _anchorPoint <= VirtualSnapshotPointUtil.GetPosition caret then (_anchorPoint,caret)
                else (caret,_anchorPoint)
            let first = 
                first
                |> VirtualSnapshotPointUtil.GetContainingLine 
                |> SnapshotLineUtil.GetStart
                |> VirtualSnapshotPointUtil.OfPoint
            let last =
                if last.IsInVirtualSpace then last
                else 
                    last 
                    |> VirtualSnapshotPointUtil.GetContainingLine
                    |> SnapshotLineUtil.GetEndIncludingLineBreak
                    |> VirtualSnapshotPointUtil.OfPoint
            _textView.Selection.Select(first, last)
        | VisualKind.Block -> selectStandard()

    /// When the text is changed it invalidates the anchor point.  It needs to be forwarded to
    /// the next version of the buffer.  If it's not present then just go to point 0
    member private x.OnTextChanged (args:TextContentChangedEventArgs) =
        let point = _anchorPoint.Position
        let trackingPoint = point.Snapshot.CreateTrackingPoint(point.Position, PointTrackingMode.Negative)
        _anchorPoint <- 
            match TrackingPointUtil.GetPoint args.After trackingPoint with
            | Some(point) -> VirtualSnapshotPoint(point)
            | None -> VirtualSnapshotPoint(args.After, 0)

    member private x.ResetCaret() =
        let point =
            let caretPoint = TextViewUtil.GetCaretPoint _textView
            if _anchorPoint.Position.Position < caretPoint.Position then _anchorPoint
            else VirtualSnapshotPoint(caretPoint)
        TextViewUtil.MoveCaretToVirtualPoint _textView point
        TextViewUtil.EnsureCaretOnScreen _textView

    interface ISelectionTracker with 
        member x.VisualKind = _kind
        member x.IsRunning = x.IsRunning
        member x.Start () = x.Start()
        member x.Stop () = x.Stop()
        member x.ResetCaret() = x.ResetCaret()
        member x.UpdateSelection () = x.UpdateSelection()
