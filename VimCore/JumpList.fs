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

    /// This is the head of the jump list.  
    let _list = new LinkedList<ITrackingLineColumn>()

    /// This is the pointer to the current node in the list
    let mutable _current = _list.First

    new(tlcService) = JumpList(tlcService, 100)

    member x.Current =
        if _current = null then None
        else _current.Value.Point

    member x.AllJumps = _list |> Seq.map (fun tlc -> tlc.Point) |> List.ofSeq

    member x.AddRaw buffer line column = 
        let tlc = _tlcService.Create buffer line column
        _current <- _list.AddFirst(tlc)
        x.MaybeTruncate()

    /// Add the SnapshotPoint to the jumplist.  Only works if the SnapshotPiont 
    member x.Add (point:SnapshotPoint) =
        let buffer = point.Snapshot.TextBuffer
        if buffer.CurrentSnapshot <> point.Snapshot then false
        else
            let line,column = TssUtil.GetLineColumn point
            x.AddRaw buffer line column
            true

    member private x.MaybeTruncate() =
        if _list.Count > _limit then 
            while _list.Count > _toKeepOnTruncate do
                _list.Last.Value.Close()
                _list.RemoveLast()
        
    interface IJumpList with
        member x.Current = x.Current
        member x.AllJumps = x.AllJumps |> Seq.ofList
        member x.MovePrevious() = 
            if _current <> null && _current.Previous <> null then 
                _current <- _current.Previous 
                true
            else
                false
        member x.MoveNext() = 
            if _current <> null && _current.Next <> null then
                _current <- _current.Next
                true
            else
                false
        member x.Add point = x.Add point

