#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input
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
        _mode : SelectionMode ) =

    let mutable _anchorPoint = SnapshotPoint(_textView.TextSnapshot, 0) 

    /// TextSelectionMode of the Editor before Start was called
    let mutable _originalSelectionMode = _textView.Selection.Mode

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
        _textView.TextBuffer.Changed.AddHandler(System.EventHandler<TextContentChangedEventArgs>(x.OnTextChanged))
        _textView.Caret.PositionChanged.AddHandler(System.EventHandler<CaretPositionChangedEventArgs>(x.OnCaretChanged))
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
        _textView.Caret.PositionChanged.RemoveHandler(System.EventHandler<CaretPositionChangedEventArgs>(x.OnCaretChanged))
        _textView.TextBuffer.Changed.RemoveHandler(System.EventHandler<TextContentChangedEventArgs>(x.OnTextChanged))
        _textView.Selection.Clear()
        _textView.Selection.Mode <- _originalSelectionMode

    /// Update the selection based on the current state of the view
    member x.UpdateSelection() = 
        let selectStandard() = 
            let first = VirtualSnapshotPoint(_anchorPoint)
            let last = _textView.Caret.Position.VirtualBufferPosition
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

    /// When the text is changed it invalidates the anchor point.  It needs to be forwarded to
    /// the next version of the buffer
    member private x.OnTextChanged _ (args:TextContentChangedEventArgs) =
        let point = _anchorPoint.Snapshot.CreateTrackingPoint(_anchorPoint.Position, PointTrackingMode.Positive)
        try
            _anchorPoint <- point.GetPoint(args.After)
        with
            | :? System.ArgumentOutOfRangeException -> 
                // If it's not present in the new version of the buffer then just go to point 0.
                _anchorPoint <- SnapshotPoint(args.After, 0)
            | e -> reraise()
        x.UpdateSelection()

    member private x.OnCaretChanged _ _ = x.UpdateSelection()
