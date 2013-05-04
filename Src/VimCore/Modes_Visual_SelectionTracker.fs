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
        _globalSettings : IVimGlobalSettings, 
        _incrementalSearch : IIncrementalSearch,
        _visualKind : VisualKind
    ) as this =

    /// The anchor point we are currently tracking 
    let mutable _anchorPoint : SnapshotPoint option = None

    /// When we are in the middle of an incremental search this will 
    /// track the most recent search result
    let mutable _lastIncrementalSearchResult : SearchResult option = None

    let mutable _textChangedHandler = ToggleHandler.Empty
    do 
        _textChangedHandler <- ToggleHandler.Create (_textView.TextBuffer.Changed) (fun (args:TextContentChangedEventArgs) -> this.OnTextChanged(args))

        _incrementalSearch.CurrentSearchUpdated
        |> Observable.add (fun args -> _lastIncrementalSearchResult <- Some args.SearchResult)
        
        _incrementalSearch.CurrentSearchCancelled
        |> Observable.add (fun _ -> _lastIncrementalSearchResult <- None)

        _incrementalSearch.CurrentSearchCompleted 
        |> Observable.add (fun _ -> _lastIncrementalSearchResult <- None)

    member x.AnchorPoint = Option.get _anchorPoint

    member x.IsRunning = Option.isSome _anchorPoint

    /// Call when selection tracking should begin.  
    member x.Start() = 
        if x.IsRunning then invalidOp Vim.Resources.SelectionTracker_AlreadyRunning
        _textChangedHandler.Add()

        _anchorPoint <- 
            let selection = _textView.Selection
            if selection.IsEmpty then

                // Set the selection.  If this is line mode we need to select the entire line 
                // here
                let caretPoint = TextViewUtil.GetCaretPoint _textView
                let visualSelection = VisualSelection.CreateInitial _visualKind caretPoint
                visualSelection.VisualSpan.Select _textView Path.Forward

                Some caretPoint
            else 
                _textView.Selection.Mode <- _visualKind.TextSelectionMode
                Some selection.AnchorPoint.Position

    /// Called when selection should no longer be tracked.  Must be paired with Start calls or
    /// we will stay attached to certain event handlers
    member x.Stop() =
        if not x.IsRunning then invalidOp Resources.SelectionTracker_NotRunning
        _textChangedHandler.Remove()
        _anchorPoint <- None

    /// Update the selection based on the current state of the ITextView
    member x.UpdateSelection() = 

        match _anchorPoint with
        | None -> ()
        | Some anchorPoint ->
            let simulatedCaretPoint = 
                let caretPoint = TextViewUtil.GetCaretPoint _textView 
                if _incrementalSearch.InSearch then
                    match _lastIncrementalSearchResult with
                    | None -> caretPoint
                    | Some searchResult ->
                        match searchResult with
                        | SearchResult.NotFound _ -> caretPoint
                        | SearchResult.Found (_, span, _) -> span.Start
                else
                    caretPoint

            // Update the selection only.  Don't move the caret here.  It's either properly positioned
            // or we're simulating the selection based on incremental search
            let visualSelection = VisualSelection.CreateForPoints _visualKind anchorPoint simulatedCaretPoint
            let visualSelection = visualSelection.AdjustForSelectionKind _globalSettings.SelectionKind
            visualSelection.Select _textView

    /// When the text is changed it invalidates the anchor point.  It needs to be forwarded to
    /// the next version of the buffer.  If it's not present then just go to point 0
    member x.OnTextChanged (args : TextContentChangedEventArgs) =
        match _anchorPoint with
        | None -> ()
        | Some anchorPoint ->

            _anchorPoint <- 
                match TrackingPointUtil.GetPointInSnapshot anchorPoint PointTrackingMode.Negative args.After with
                | None -> SnapshotPoint(args.After, 0) |> Some
                | Some anchorPoint -> Some anchorPoint

    interface ISelectionTracker with 
        member x.VisualKind = _visualKind
        member x.IsRunning = x.IsRunning
        member x.Start () = x.Start()
        member x.Stop () = x.Stop()
        member x.UpdateSelection() = x.UpdateSelection()
