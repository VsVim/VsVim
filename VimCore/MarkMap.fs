#light

namespace Vim
open Microsoft.VisualStudio.Text
open System.Collections.Generic

type MarkMap( _tlcService : ITrackingLineColumnService ) =

    let mutable _localMap = new Dictionary<ITextBuffer,(char*ITrackingLineColumn) list>();
    let mutable _globalList : (char*ITrackingLineColumn) seq = Seq.empty

    /// Is this mark local to a buffer
    static member IsLocalMark c =
        if System.Char.IsDigit(c) then false
        else if System.Char.IsLetter(c) && System.Char.IsUpper(c) then false
        else true

    member private x.GetLocalList (buffer:ITextBuffer) =
        let empty : (char*ITrackingLineColumn) list = List.empty
        let mutable list = empty 
        if _localMap.TryGetValue(buffer, &list) then
            list
        else
            empty

    /// Try and get the local mark for the specified char <param name="c"/>.  If it does not
    /// exist then the value None will be returned.  The mark is always returned for the 
    /// current ITextSnapshot of the <param name="buffer"> 
    member x.GetLocalMark buffer (ident:char) =
        let list = x.GetLocalList buffer
        match list |> Seq.tryFind (fun (c,m) -> c = ident) with
        | Some(_,tlc) -> tlc.VirtualPoint 
        | None -> None

    /// Get the mark relative to the specified buffer
    member x.GetMark buffer (ident:char) =
        if MarkMap.IsLocalMark ident then
            x.GetLocalMark buffer ident
        else
            let found = 
                _globalList 
                |> Seq.tryFind (fun (c,tlc) -> tlc.TextBuffer = buffer && c = ident)
            match found with 
            | Some(_,tlc) -> tlc.VirtualPoint
            | None -> None

    member x.SetLocalMark point (ident:char) = 
        if not (MarkMap.IsLocalMark ident) then failwith "Invalid"
        let tlc = _tlcService.CreateForPoint point
        let buffer = tlc.TextBuffer
        let list = x.GetLocalList buffer

        let prev = list |> List.tryFind (fun (c,_) -> c = ident)
        let list = 
            match prev with 
            | Some(_,oldTlc) -> 
                // Close out the previous ITrackingLineColumn for the mark since we're about to remove
                // it
                oldTlc.Close()
                list |> Seq.ofList |> Seq.filter (fun (c,_) -> c <> ident) |> List.ofSeq
            | None -> list
        let list = [(ident,tlc)] @ list
        _localMap.Item(buffer) <- list

    /// Set the mark for the <param name="point"/> inside the corresponding buffer.  
    member x.SetMark (point:SnapshotPoint) (ident:char) = 
        let buffer = point.Snapshot.TextBuffer
        if MarkMap.IsLocalMark ident then x.SetLocalMark point ident
        else    
            let tlc = _tlcService.CreateForPoint point

            // Close out the old one
            _globalList 
                |> Seq.filter (fun (c,_) -> c = ident)
                |> Seq.iter (fun (_,oldTlc) -> oldTlc.Close())

            _globalList <-
                _globalList
                |> Seq.filter (fun (c,_) -> c <> ident)
                |> Seq.append (Seq.singleton (ident,tlc))

    member x.GetGlobalMarkOwner ident = 
        let res = 
            _globalList 
                |> Seq.filter (fun (c,_) -> c = ident)
                |> Seq.map (fun (_,tlc) -> tlc.TextBuffer)
        if Seq.isEmpty res then None else Some (Seq.head res)

    member x.GetGlobalMark ident = 
        match x.GetGlobalMarkOwner ident with 
        | Some (buf) ->  x.GetMark buf ident 
        | None -> None

    /// Delete the specified local mark.  
    /// <returns>True if the mark was deleted.  False if no action was taken </returns>
    member x.DeleteLocalMark buffer (ident:char) =
        if not (MarkMap.IsLocalMark ident) then failwith "Invalid"
        let isDelete = x.GetLocalMark buffer ident |> Option.isSome
        let list = 
            x.GetLocalList buffer
            |> List.filter (fun (c,_) -> c <> ident)
        if list |> List.isEmpty then
            _localMap.Remove(buffer) |> ignore
        else
            _localMap.Item(buffer) <- list
        isDelete

    /// Delete all of the marks being tracked
    member x.DeleteAllMarks () = 
        _localMap.Values 
        |> Seq.concat 
        |> Seq.iter (fun (_,tlc) -> tlc.Close())
        _globalList |> Seq.iter (fun (_,tlc) -> tlc.Close())
        _localMap.Clear()
        _globalList <- Seq.empty
    
    /// Delete all of the marks for the specified buffer
    member x.DeleteAllMarksForBuffer buffer =
        let found,list = _localMap.TryGetValue(buffer)
        if found then 
            list |> List.iter (fun (_,tlc) -> tlc.Close())
            _localMap.Remove(buffer) |> ignore

        _globalList
        |> Seq.filter (fun (_,tlc) -> tlc.TextBuffer = buffer)
        |> Seq.iter (fun (_,tlc) -> tlc.Close())
        _globalList <- _globalList |> Seq.filter (fun (_,tlc) -> tlc.TextBuffer <> buffer)

    member x.GetLocalMarks (buffer:ITextBuffer) = 
        let tss = buffer.CurrentSnapshot
        buffer
        |> x.GetLocalList
        |> Seq.map (fun (c,tlc) -> (c,tlc.VirtualPoint))
        |> Seq.choose (fun (c,opt) -> if Option.isSome opt then Some (c, Option.get opt) else None)

    member x.GetGlobalMarks () = 
        _globalList 
        |> Seq.map (fun (c,tlc) -> (c,tlc.VirtualPoint))
        |> Seq.choose (fun (c,opt) -> if Option.isSome opt then Some (c, Option.get opt) else None)

    interface IMarkMap with
        member x.IsLocalMark c = MarkMap.IsLocalMark c
        member x.GetLocalMark buf c = x.GetLocalMark buf c
        member x.GetGlobalMarkOwner c = x.GetGlobalMarkOwner c
        member x.GetMark buf c = x.GetMark buf c 
        member x.GetGlobalMark c = x.GetGlobalMark c
        member x.SetMark point c = x.SetMark point c 
        member x.SetLocalMark point c = x.SetLocalMark point c
        member x.GetLocalMarks buffer = x.GetLocalMarks buffer
        member x.GetGlobalMarks () = x.GetGlobalMarks()
        member x.DeleteLocalMark buf c = x.DeleteLocalMark buf c 
        member x.DeleteAllMarks () = x.DeleteAllMarks()

    interface IVimBufferCreationListener with
        member x.VimBufferCreated buffer = 
            buffer.Closed |> Event.add (fun _ -> x.DeleteAllMarksForBuffer buffer.TextBuffer)
