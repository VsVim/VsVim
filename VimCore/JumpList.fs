#light

namespace Vim
open Microsoft.VisualStudio.Text
open System.Collections.Generic

/// Implementation of the IJumpList.  Maintains a jumpable list of points for the current
/// window
type internal JumpList 
    ( 
        _trackingLineColumnService : ITrackingLineColumnService
    ) =  

    /// The limit of items in the jump list is 100.  See ':help jumplist'
    let _limit = 100

    /// This is the LinkedList containing the jump list.  To keep the terminology consistent
    /// we add new items to the end of the LinkedList and start our iteration at that point. To
    /// do otherwise forces us to use the Next values on linked list nodes to implement
    /// MovePrevious and that simply produces confusion
    let _list = new LinkedList<ITrackingLineColumn>()

    /// Index into the linked list of values
    let mutable _current : (LinkedListNode<ITrackingLineColumn> * int) option = None

    /// Return the current Point
    member x.Current = 
        match _current with
        | None -> None
        | Some (current, _) -> current.Value.Point

    /// Return the current index
    member x.CurrentIndex =
        match _current with
        | None -> None
        | Some (_, index) -> Some index

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

    /// Add a SnapshotPoint to the list.  
    member x.Add point = 
        let line, column = SnapshotPointUtil.GetLineColumn point
        let trackingLineColumn = 
            let textBuffer = point.Snapshot.TextBuffer

            // If there is an existing ITrackingLineColumn which tracks this buffer and
            // line we should re-use that
            let node = 
                let rec inner (current : LinkedListNode<ITrackingLineColumn>) = 
                    let trackingLineColumn = current.Value
                    let matches = 
                        if trackingLineColumn.TextBuffer <> textBuffer then
                            false
                        else
                            match trackingLineColumn.Point with
                            | None -> false
                            | Some point -> line = SnapshotPointUtil.GetLineNumber point
                    if matches then Some current
                    elif current.Next = null then None
                    else inner current.Next

                if _list.Count = 0 then None
                else inner _list.First

            match node with
            | None -> 
                _trackingLineColumnService.Create textBuffer line column LineColumnTrackingMode.SurviveDeletes
            | Some node ->
                _list.Remove(node) |> ignore
                node.Value

        // We should now point to the front of the list
        let head = _list.AddFirst(trackingLineColumn)
        _current <- Some (head, 0)

        // Truncate the end of the list
        while _list.Count > _limit do
            _list.Last.Value.Close()
            _list.RemoveLast()

    /// Move to the newer node in the jump list.  
    member x.MoveNewer count = 
        match _current with
        | None ->
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

    /// Move to the older node in the jump list
    member x.MoveOlder count =
        match _current with
        | None ->
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

    interface IJumpList with
        member x.Current = x.Current
        member x.CurrentIndex = x.CurrentIndex
        member x.Jumps = x.Jumps
        member x.MoveNewer count = x.MoveNewer count
        member x.MoveOlder count = x.MoveOlder count
        member x.Add point = x.Add point

