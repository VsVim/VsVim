#light

namespace Vim.Modes.Visual
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor
open Vim

/// Responsible for tracking and updating the selection while we are in visual mode
type internal SelectionTracker
    (
        _vimBufferData: IVimBufferData,
        _incrementalSearch: IIncrementalSearch,
        _visualKind: VisualKind
    ) as this =

    let _textView = _vimBufferData.TextView
    let _textBuffer = _vimBufferData.TextBuffer
    let _localSettings = _vimBufferData.LocalSettings
    let _globalSettings = _localSettings.GlobalSettings

    /// The anchor point we are currently tracking.  This is always included in the selection which
    /// is created by this type 
    let mutable _anchorPoint: VirtualSnapshotPoint option = None

    /// Should the selection be extended into the line break 
    let mutable _extendIntoLineBreak: bool = false

    /// When we are in the middle of an incremental search this will 
    /// track the most recent search result
    let mutable _lastIncrementalSearchResult: SearchResult option = None

    let mutable _textChangedHandler = ToggleHandler.Empty
    do 
        _textChangedHandler <- ToggleHandler.Create (_textView.TextBuffer.Changed) (fun (args:TextContentChangedEventArgs) -> this.OnTextChanged(args))

        _incrementalSearch.SessionCreated
        |> Observable.add (fun args -> this.OnIncrementalSearchSessionCreated(args.Session))

    member x.AnchorPoint = Option.get _anchorPoint

    member x.IsRunning = Option.isSome _anchorPoint

    /// Call when selection tracking should begin.  
    member x.Start() = 
        if x.IsRunning then invalidOp Vim.Resources.SelectionTracker_AlreadyRunning
        _textChangedHandler.Add()

        let useVirtualSpace = _vimBufferData.VimTextBuffer.UseVirtualSpace
        let selection = _textView.Selection
        if selection.IsEmpty then

            // Set the selection.  If this is line mode we need to select the entire line 
            // here
            let caretPoint = TextViewUtil.GetCaretVirtualPoint _textView
            let visualSelection = VisualSelection.CreateInitial _visualKind caretPoint _localSettings.TabStop _globalSettings.SelectionKind useVirtualSpace
            visualSelection.VisualSpan.Select _textView SearchPath.Forward

            _anchorPoint <- Some caretPoint
            _extendIntoLineBreak <- false

            _vimBufferData.VimTextBuffer.LastVisualSelection <- Some visualSelection

        else 
            // The selection is already set and we need to track it.  The anchor point in
            // vim is always included in the selection but in the ITextSelection it is 
            // not when the selection is reversed.  We need to account for this when 
            // setting our anchor point unless the selection is exclusive
            _textView.Selection.Mode <- _visualKind.TextSelectionMode
            let anchorPoint = selection.AnchorPoint
            let isInclusive =_globalSettings.SelectionKind = SelectionKind.Inclusive 
            _anchorPoint <- 
                if selection.IsReversed && isInclusive then
                    VirtualSnapshotPointUtil.SubtractOneOrCurrent anchorPoint |> Some
                else
                    Some anchorPoint
            _extendIntoLineBreak <- _visualKind = VisualKind.Character && selection.AnchorPoint.IsInVirtualSpace

            let visualSelection = VisualSelection.CreateForVirtualSelection _textView _visualKind _globalSettings.SelectionKind _vimBufferData.LocalSettings.TabStop useVirtualSpace
            let visualSelection = visualSelection.AdjustForSelectionKind _globalSettings.SelectionKind
            _vimBufferData.VimTextBuffer.LastVisualSelection <- Some visualSelection

    /// Called when selection should no longer be tracked.  Must be paired with Start calls or
    /// we will stay attached to certain event handlers
    member x.Stop() =
        if not x.IsRunning then invalidOp Resources.SelectionTracker_NotRunning
        _textChangedHandler.Remove()
        _anchorPoint <- None

    member x.OnIncrementalSearchSessionCreated (session: IIncrementalSearchSession) =
        _lastIncrementalSearchResult <- None

        // Whenever a search completes we want to track the result in the selection. The one exception
        // is when the search is cancelled. That happens when the developer:
        // - types ctrl-c: the session complete event will clear the data
        // - types a new letter: just let the last result stay until a new result shows up
        session.SearchEnd 
        |> Observable.add (fun args -> 
            _lastIncrementalSearchResult <- 
                match args.SearchResult with
                | SearchResult.Cancelled _ -> _lastIncrementalSearchResult
                | _ -> Some args.SearchResult)

        session.SessionComplete
        |> Observable.add (fun _ -> _lastIncrementalSearchResult <- None)

    /// Update the selection based on the current state of the ITextView
    member x.UpdateSelection() = 

        match _anchorPoint with
        | None -> ()
        | Some anchorPoint ->
            let simulatedCaretPoint = 
                let caretPoint = TextViewUtil.GetCaretVirtualPoint _textView 
                match _lastIncrementalSearchResult with
                | None -> caretPoint
                | Some searchResult ->
                    match searchResult with
                    | SearchResult.NotFound _ -> caretPoint
                    | SearchResult.Error _ -> caretPoint
                    | SearchResult.Cancelled _ -> caretPoint
                    | SearchResult.Found (_, span, _, _) ->
                        VirtualSnapshotPointUtil.OfPoint span.Start

            // Update the selection only.  Don't move the caret here.  It's either properly positioned
            // or we're simulating the selection based on incremental search
            let useVirtualSpace = _vimBufferData.VimTextBuffer.UseVirtualSpace
            let visualSelection = VisualSelection.CreateForVirtualPoints _visualKind anchorPoint simulatedCaretPoint _localSettings.TabStop useVirtualSpace
            let visualSelection = visualSelection.AdjustForExtendIntoLineBreak _extendIntoLineBreak
            let visualSelection = visualSelection.AdjustForSelectionKind _globalSettings.SelectionKind
            visualSelection.Select _textView

            _vimBufferData.VimTextBuffer.LastVisualSelection <- Some visualSelection

    /// Update the selection based on the current state of the ITextView
    member x.UpdateSelectionWithAnchorPoint anchorPoint = 
        if not x.IsRunning then invalidOp Resources.SelectionTracker_NotRunning

        _anchorPoint <- Some anchorPoint
        x.UpdateSelection()

    /// Record the caret tracking point
    member x.RecordCaretTrackingPoint modeArgument =

        // If we are provided an InitialVisualSpan value here go ahead and use it.  Do this before we
        // begin selection tracking as it will properly update the resulting selection to the appropriate
        // mode
        let caretPoint =
            match modeArgument with
            | ModeArgument.InitialVisualSelection (visualSelection, caretPoint) ->

                if visualSelection.VisualKind = _visualKind then
                    visualSelection.Select _textView
                    let visualCaretPoint = visualSelection.GetCaretVirtualPoint _globalSettings.SelectionKind
                    TextViewUtil.MoveCaretToVirtualPointRaw _textView visualCaretPoint MoveCaretFlags.EnsureOnScreen
                    caretPoint
                else
                    None
            | _ ->
                None

        // Save the start point of the visual selection so we can potentially reset to it later
        let caretPosition =
            match caretPoint with
            | Some caretPoint -> caretPoint.Position
            | None ->
                // If there is an existing explicit selection then the anchor point is considered
                // the original start point.  Else just use the caret point
                if _textView.Selection.IsEmpty then
                    (TextViewUtil.GetCaretPoint _textView).Position
                else
                    _textView.Selection.AnchorPoint.Position.Position

        let caretTrackingPoint = _textBuffer.CurrentSnapshot.CreateTrackingPoint(caretPosition, PointTrackingMode.Negative) |> Some
        _vimBufferData.VisualCaretStartPoint <- caretTrackingPoint
        _vimBufferData.VisualAnchorPoint <- caretTrackingPoint

    /// When the text is changed it invalidates the anchor point.  It needs to be forwarded to
    /// the next version of the buffer.  If it's not present then just go to point 0
    member x.OnTextChanged (args: TextContentChangedEventArgs) =
        match _anchorPoint with
        | None -> ()
        | Some anchorPoint ->

            _anchorPoint <- 
                match TrackingPointUtil.GetVirtualPointInSnapshot anchorPoint PointTrackingMode.Negative args.After with
                | None -> VirtualSnapshotPoint(args.After, 0) |> Some
                | Some anchorPoint -> Some anchorPoint

    interface ISelectionTracker with 
        member x.VisualKind = _visualKind
        member x.IsRunning = x.IsRunning
        member x.Start () = x.Start()
        member x.Stop () = x.Stop()
        member x.UpdateSelection() = x.UpdateSelection()
        member x.UpdateSelectionWithAnchorPoint anchorPoint = x.UpdateSelectionWithAnchorPoint anchorPoint
        member x.RecordCaretTrackingPoint modeArgument = x.RecordCaretTrackingPoint modeArgument
