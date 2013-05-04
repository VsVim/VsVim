#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Collections.Generic

/// Implementation of the IJumpList.  Maintains a jumpable list of points for the current
/// window
type internal JumpList 
    ( 
        _textView : ITextView,
        _bufferTrackingService : IBufferTrackingService
    ) =

    let _textBuffer = _textView.TextBuffer

    /// The limit of items in the jump list is 100.  See ':help jumplist'
    let _limit = 100

    /// This is the LinkedList containing the jump list.  To keep the terminology consistent
    /// we add new items to the end of the LinkedList and start our iteration at that point. To
    /// do otherwise forces us to use the Next values on linked list nodes to implement
    /// MovePrevious and that simply produces confusion
    let _list = new LinkedList<ITrackingLineColumn>()

    /// Index into the linked list of values.  Contains a value if we are actively traversing
    let mutable _current : (LinkedListNode<ITrackingLineColumn> * int) option = None

    /// Last jump from location
    let mutable _lastJumpLocation : ITrackingLineColumn option = None

    /// Return the point for the caret before the most recent jump
    member x.LastJumpLocation = _lastJumpLocation |> OptionUtil.map2 (fun trackingLineColumn -> trackingLineColumn.VirtualPoint)

    /// Return the information for the current location in the list
    member x.Current = 
        match _current with
        | None -> None
        | Some (current, _) -> current.Value.Point

    /// Return the current index
    member x.CurrentIndex =
        match _current with
        | None -> None
        | Some (_, index) -> Some index

    member x.IsTraversing = Option.isSome _current

    /// Returns all of the Jumps.  It's technically possible for an ITrackingLineColumn to 
    /// no longer track due to an error in the tracking.  If that's the case just represent
    /// it as point 0 in the ITextSnapshot
    member x.Jumps = 
        _list
        |> Seq.map (fun tlc -> 
            match tlc.VirtualPoint with
            | Some point -> point
            | None -> VirtualSnapshotPoint(tlc.TextBuffer.CurrentSnapshot, 0))
        |> List.ofSeq

    /// Find the LinkedListNode tracking the provided line number.  Returns None if 
    /// one doesn't exist
    member x.FindNodeTrackingLine lineNumber = 
        let rec inner (current : LinkedListNode<ITrackingLineColumn>) =

            let currentLineNumber = current.Value.Point |> Option.map SnapshotPointUtil.GetLineNumber
            let matches = 
                match currentLineNumber with
                | None -> false
                | Some currentLineNumber -> currentLineNumber = lineNumber
            if matches then
                Some current
            elif current.Next = null then
                None
            else
                inner current.Next

        if _list.Count = 0 then
            None
        else
            inner _list.First

    /// Add the SnapshotPoint into the jump list and return the LinkedListNode which
    /// it occupies
    member x.AddCore point = 

        // First complete the existing traversal.  Adding a new jump point ends the 
        // traversal
        _current <- None

        let line, column = SnapshotPointUtil.GetLineColumn point

        let trackingLineColumn = 
            match x.FindNodeTrackingLine line with
            | None -> 
                // No node currently tracking that line.  Create a new one
                _bufferTrackingService.CreateLineColumn _textBuffer line column LineColumnTrackingMode.SurviveDeletes
            | Some node ->
                // Existing node.  Re-use the ITrackingLineColumn and remove the node from
                // the list
                _list.Remove(node) |> ignore
                node.Value

        let node = _list.AddFirst(trackingLineColumn)

        // Truncate the end of the list
        while _list.Count > _limit do
            _list.Last.Value.Close()
            _list.RemoveLast()

        node

    /// Add the SnapshotPoint into the jump list.  This will end the current traversal and 
    /// update the LastJumpLocation to that point
    member x.Add point = 

        // First complete the existing traversal.  Adding a new jump point ends the 
        // traversal
        _current <- None

        let line, column = SnapshotPointUtil.GetLineColumn point
        x.SetLastJumpLocation line column

        x.AddCore point |> ignore

    /// Clear out all of the tracking data 
    member x.Clear() = 
        _current <- None

        // Clear out the list of jump locations
        _list
        |> Seq.iter (fun trackingLineColumn -> trackingLineColumn.Close())
        _list.Clear()

        // Clear out the last jump location
        match _lastJumpLocation with
        | None -> ()
        | Some trackingLineColumn -> trackingLineColumn.Close()
        _lastJumpLocation <- None

    /// Move to the newer node in the jump list.  
    member x.MoveNewer count = 
        match _current with
        | None ->
            // Not traversing
            false
        | Some (current, index) ->
            let rec inner (current : LinkedListNode<ITrackingLineColumn>) count = 
                if count = 0 then current, true
                elif current.Previous = null then current, false
                else inner current.Previous (count - 1)

            let current, success = inner current count
            if success then
                _current <- Some (current, index - count)
            success

    /// Move to the older node in the jump list.  This will start a traversal if 
    /// we are not yet traversing the list
    member x.MoveOlder count =
        match _current with
        | None ->
            // Not traversing
            false
        | Some (current, index) ->
            let rec inner (current : LinkedListNode<ITrackingLineColumn>) count = 
                if count = 0 then current, true
                elif current.Next = null then current, false
                else inner current.Next (count - 1)
            let current, success = inner current count
            if success then
                _current <- Some (current, index + count)
            success

    /// Set the last jump location to the provided line and column in the ITextView
    member x.SetLastJumpLocation line column = 
        match _lastJumpLocation with
        | None -> ()
        | Some trackingLineColumn -> trackingLineColumn.Close()

        _lastJumpLocation <- Some (_bufferTrackingService.CreateLineColumn _textView.TextBuffer line column LineColumnTrackingMode.Default)

    /// Start a traversal of the jump list
    member x.StartTraversal() = 
        let caretPoint = TextViewUtil.GetCaretPoint _textView
        let node = x.AddCore caretPoint
        _current <- Some (node, 0)

    interface IJumpList with
        member x.TextView = _textView
        member x.Current = x.Current
        member x.CurrentIndex = x.CurrentIndex
        member x.IsTraversing = x.IsTraversing
        member x.Jumps = x.Jumps
        member x.LastJumpLocation = x.LastJumpLocation
        member x.Add point = x.Add point
        member x.Clear() = x.Clear()
        member x.MoveNewer count = x.MoveNewer count
        member x.MoveOlder count = x.MoveOlder count
        member x.SetLastJumpLocation line column = x.SetLastJumpLocation line column
        member x.StartTraversal() = x.StartTraversal()

