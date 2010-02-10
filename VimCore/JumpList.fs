#light

namespace Vim
open Microsoft.VisualStudio.Text
open System.Collections.Generic

/// Implementation of the IJumpList.  Maintains a jumpable list of points for the current
/// window
type internal JumpList 
    ( 
        _tlcService : ITrackingLineColumnService,
        _limit : int  ) =  

    let _toKeepOnTruncate =     
        if _limit > 10 then _limit - 10
        else _limit 

    /// This is the LinkedList containing the jump list.  To keep the terminology consistent
    /// we add new items to the end of the LinkedList and start our iteration at that point. To
    /// do otherwise forces us to use the Next values on linked list nodes to implement
    /// MovePrevious and that simply produces confusion
    let _list = new LinkedList<ITrackingLineColumn>()

    /// This is the pointer to the current node in the list
    let mutable _current : LinkedListNode<ITrackingLineColumn> = null

    new(tlcService) = JumpList(tlcService, 100)

    member x.Current =
        if _current = null then None
        else _current.Value.Point

    member x.AllJumps = _list |> Seq.map (fun tlc -> tlc.Point) |> List.ofSeq

    member x.Add point = 
        let tlc = _tlcService.CreateForPoint point 
        _list.AddLast(tlc) |> ignore
        _current <- null
        x.MaybeTruncate()

    /// Move to the previous node in the jump list.  
    member x.MovePrevious() = 
        if _current = null then
            if _list.Count = 0 then false
            else    
                _current <- _list.Last
                true
        elif _current.Previous <> null then
            _current <- _current.Previous
            true
        else
            false

    /// Move to the next node in the jump list
    member x.MoveNext() =
        if _current = null then false
        else
            _current <- _current.Next
            true

    member private x.MaybeTruncate() =
        if _list.Count > _limit then 
            while _list.Count > _toKeepOnTruncate do
                _list.First.Value.Close()
                _list.RemoveFirst()

    interface IJumpList with
        member x.Current = x.Current
        member x.AllJumps = x.AllJumps |> Seq.ofList
        member x.MovePrevious() = x.MovePrevious() 
        member x.MoveNext() = x.MoveNext()
        member x.Add point = x.Add point

