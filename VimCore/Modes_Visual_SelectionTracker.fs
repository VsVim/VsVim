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

    /// Tracks the count of explicit moves we are seeing.  Normally an explicit character
    /// move causes the selection to be removed.  Updating this counter is a way of our 
    /// consumers to tell us the caret move is legal
    let mutable _explicitMoveCount = 0

    /// Anchor point being tracked by the selection tracker
    member x.AnchorPoint = _anchorPoint

    /// Call when selection tracking should begin.  Optionally passes in an anchor point for
    /// the selection.  If it's not specified the caret will be used
    member x.Start(anchor : SnapshotPoint option) =
        _textView.TextBuffer.Changed.AddHandler(System.EventHandler<TextContentChangedEventArgs>(x.OnTextChanged))
        _anchorPoint <- 
            match anchor with
            | Some(point) -> point
            | None -> ViewUtil.GetCaretPoint _textView

        // Update the selection based on our current information
        _originalSelectionMode = _textView.Selection.Mode
        _textView.Selection.Select(new Span(_anchorPoint.Position, 1), false)
        _textView.Selection.Mode = 
            match _mode with
            | Character -> TextSelectionMode.Stream
            | Line -> TextSelectionMode.Stream
            | Box -> TextSelectionMode.Box

    /// Called when selection should no longer be tracked.  Must be paired with Start calls or
    /// we will stay attached to certain event handlers
    member x.Stop() =
        _textView.TextBuffer.Changed.RemoveHandler(System.EventHandler<TextContentChangedEventArgs>(x.OnTextChanged))
        _textView.Selection.Clear()
        _textView.Selection.Mode = _originalSelectionMode

    member x.InExplicitMove = _explicitMoveCount > 0
    member x.BeginExplicitMove() = _explicitMoveCount <- _explicitMoveCount + 1
    member x.EndExplicitMove() = _explicitMoveCount <- _explicitMoveCount - 1

    /// Update the selection based on the current state of the view
    member x.UpdateSelection() = 
        match _mode with
        | Character ->
            let first = VirtualSnapshotPoint(_anchorPoint)
            let last = _textView.Caret.Position.VirtualBufferPosition
            _textView.Selection.Select(first,last)
        | Line ->
            let caret = ViewUtil.GetCaretPoint _textView
        | Block -> 
            ()


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
