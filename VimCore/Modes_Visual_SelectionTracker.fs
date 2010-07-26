#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
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

    let mutable _anchorPoint = VirtualSnapshotPoint(_textView.TextSnapshot, 0) 

    /// TextSelectionMode of the Editor before Start was called
    let mutable _originalSelectionMode = _textView.Selection.Mode

    /// Whether or not we are currently running
    let mutable _running = false

    let mutable _textChangedHandler = ToggleHandler.Empty
    let mutable _caretChangedHandler = ToggleHandler.Empty

    do 
        _textChangedHandler <- ToggleHandler.Create (_textView.TextBuffer.Changed) (fun (args:TextContentChangedEventArgs) -> this.OnTextChanged(args))
        _caretChangedHandler <- ToggleHandler.Create (_textView.Caret.PositionChanged) (fun _ -> this.OnCaretChanged())

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

    member x.SelectedLines = 
        let spans = _textView.Selection.SelectedSpans 
        match spans |> Seq.isEmpty with
        | true -> SnapshotSpan(_textView.TextSnapshot,0,0)
        | false -> 
            let head = spans |> Seq.head
            let startLine = head.Start.GetContainingLine()
            let endLine = head.End.GetContainingLine()
            SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak)

    member x.IsRunning = _running

    /// Call when selection tracking should begin.  
    member x.Start() = 
        if _running then invalidOp Vim.Resources.SelectionTracker_AlreadyRunning
        _running <- true
        _textChangedHandler.Add()
        _caretChangedHandler.Add()

        let selection = _textView.Selection
        _originalSelectionMode <- selection.Mode
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
        _caretChangedHandler.Remove()

        // On teardown we will get calls to Stop when the view is closed.  It's invalid to access 
        // the selection at that point
        if not _textView.IsClosed then
            _textView.Selection.Clear()
            _textView.Selection.Mode <- _originalSelectionMode

        _running <- false

    /// Update the selection based on the current state of the view
    member private x.UpdateSelectionCore() = 
        let desiredMode = 
            match _mode with
            | SelectionMode.Character -> TextSelectionMode.Stream
            | SelectionMode.Line -> TextSelectionMode.Stream
            | SelectionMode.Block -> TextSelectionMode.Box
            | _ -> failwith "Invaild enum value"
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

        match _mode with
        | SelectionMode.Character -> selectStandard()
        | SelectionMode.Line ->
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
        | SelectionMode.Block -> selectStandard()
        | _ -> failwith "Invalid enum value"

    /// Update the selection.  Because certain types of movements can themselves reset the selection
    /// we do our reset on the background.  Ensure we are still running to avoid a race condition in 
    /// this process
    member private x.UpdateSelection() = 
        let func _ = 
            if _running then
                x.UpdateSelectionCore()
        System.Threading.SynchronizationContext.Current.Post(
            new System.Threading.SendOrPostCallback(func),
            null)

    /// When the text is changed it invalidates the anchor point.  It needs to be forwarded to
    /// the next version of the buffer
    member private x.OnTextChanged (args:TextContentChangedEventArgs) =
        let point = _anchorPoint.Position
        let trackingPoint = point.Snapshot.CreateTrackingPoint(point.Position, PointTrackingMode.Negative)
        try
            let point = trackingPoint.GetPoint(args.After)
            _anchorPoint <- VirtualSnapshotPoint(point)
        with
            | :? System.ArgumentOutOfRangeException -> 
                // If it's not present in the new version of the buffer then just go to point 0.
                _anchorPoint <- VirtualSnapshotPoint(args.After, 0)
            | e -> reraise()
        x.UpdateSelection()

    member private x.OnCaretChanged()  = x.UpdateSelection()

    interface ISelectionTracker with 
        member x.IsRunning = x.IsRunning
        member x.SelectedText = x.SelectedText
        member x.SelectedLines = x.SelectedLines
        member x.Start () = x.Start()
        member x.Stop () = x.Stop()
