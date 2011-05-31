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
        _settings : IVimGlobalSettings, 
        _incrementalSearch : IIncrementalSearch,
        _kind : VisualKind ) as this =

    let mutable _anchorPoint = VirtualSnapshotPoint(_textView.TextSnapshot, 0) 

    /// Whether or not we are currently running
    let mutable _running = false

    /// When we are in the middle of an incremental search this will 
    /// track the most recent search result
    let mutable _lastIncrementalSearchResult : SearchResult option = None

    let mutable _textChangedHandler = ToggleHandler.Empty
    do 
        _textChangedHandler <- ToggleHandler.Create (_textView.TextBuffer.Changed) (fun (args:TextContentChangedEventArgs) -> this.OnTextChanged(args))

        _incrementalSearch.CurrentSearchUpdated
        |> Observable.add (fun result -> _lastIncrementalSearchResult <- Some result)
        
        _incrementalSearch.CurrentSearchCancelled
        |> Observable.add (fun _ -> _lastIncrementalSearchResult <- None)

        _incrementalSearch.CurrentSearchCompleted 
        |> Observable.add (fun _ -> _lastIncrementalSearchResult <- None)

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

        _running <- false

    /// Update the selection based on the current state of the view
    member x.UpdateSelection() = 
        let desiredMode = 
            match _kind with
            | VisualKind.Character -> TextSelectionMode.Stream
            | VisualKind.Line -> TextSelectionMode.Stream
            | VisualKind.Block -> TextSelectionMode.Box
        if _textView.Selection.Mode <> desiredMode then _textView.Selection.Mode <- desiredMode

        // Get the end point of the desired selection.  Typically this is 
        // just the caret.  If an incremental search is active though and 
        // has a found result it will be the start of it
        let endPoint = 
            let caretPoint = _textView.Caret.Position.VirtualBufferPosition 
            if _incrementalSearch.InSearch then
                match _lastIncrementalSearchResult with
                | Some(result) ->
                    match result with
                    | SearchResult.Found (_, span, _) -> VirtualSnapshotPoint(span.Start)
                    | SearchResult.NotFound _ -> caretPoint
                | None -> 
                    caretPoint
            else
                caretPoint

        // Set the selection based on the anchor point and the caret or the 
        // current place of the incremental search
        let selectStandard() = 
            let first = _anchorPoint
            let last = endPoint 
            let first,last = VirtualSnapshotPointUtil.OrderAscending first last
            let last = 
                if _settings.IsSelectionInclusive then VirtualSnapshotPointUtil.AddOneOnSameLine last 
                else last
            _textView.Selection.Select(first,last)

        match _kind with
        | VisualKind.Character -> selectStandard()
        | VisualKind.Line ->
            let first, last = 
                if VirtualSnapshotPointUtil.GetPosition _anchorPoint <= VirtualSnapshotPointUtil.GetPosition endPoint then 
                    (_anchorPoint, endPoint)
                else 
                    (endPoint, _anchorPoint)
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
