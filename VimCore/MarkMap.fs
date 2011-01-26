#light

namespace Vim
open Microsoft.VisualStudio.Text
open System.Collections.Generic

type BufferMarkData =  {
    TextBuffer : ITextBuffer;
    Marks : (char*ITrackingLineColumn) list;
    LastSelection : ITrackingSpan option
}

type MarkMap( _tlcService : ITrackingLineColumnService ) =

    /// Set of char's for all possible local marks
    static let _localMarkSet = 
        CharUtil.LettersLower
        |> Seq.append ['>';'<']
        |> Set.ofSeq

    /// Set of char's for all updatable local marks
    static let _updatableLocalMarkSet = CharUtil.Letters |> Set.ofSeq

    let mutable _localMap = new Dictionary<ITextBuffer,BufferMarkData>()
    let mutable _globalList : (char*ITrackingLineColumn) list = List.empty

    /// Is this mark local to a buffer
    static member IsLocalMark c = Set.contains c _localMarkSet

    member private x.GetLastSelection (data : BufferMarkData) =
        match data.LastSelection with
        | None -> None
        | Some(selection) ->
            let snapshot = data.TextBuffer.CurrentSnapshot
            TrackingSpanUtil.GetSpan snapshot selection 

    /// Get or create the BufferMarkData for the given ITextBuffer. 
    member private x.GetOrCreateBufferMarkData buffer = 
        let ret,data = _localMap.TryGetValue(buffer)
        if ret then data
        else 
            let data = { TextBuffer=buffer;Marks=List.empty; LastSelection=None }
            _localMap.Item(buffer) <- data
            data

    member private x.GetSelectionStartMark data =
        match x.GetLastSelection data with
        | None -> None
        | Some(span) -> span.Start |> VirtualSnapshotPointUtil.OfPoint |> Some

    member private x.GetSelectionEndMark data =
        match x.GetLastSelection data with
        | None -> None
        | Some(span) -> 
            match SnapshotSpanUtil.GetLastIncludedPoint span with
            | None -> None
            | Some(point) -> point |> VirtualSnapshotPointUtil.OfPoint |> Some

    /// Try and get the local mark for the specified char <param name="c"/>.  If it does not
    /// exist then the value None will be returned.  The mark is always returned for the 
    /// current ITextSnapshot of the <param name="buffer"> 
    member x.GetLocalMark buffer ident =
        let ret,data = _localMap.TryGetValue(buffer) 
        if ret then 
            if CharUtil.IsLetter ident then 
                let list = data.Marks
                match list |> Seq.tryFind (fun (c,m) -> c = ident) with
                | Some(_,tlc) -> tlc.VirtualPoint 
                | None -> None
            else
                match ident with
                | '<' -> x.GetSelectionStartMark data
                | '>' -> x.GetSelectionEndMark data
                | _ -> None
                       
        else None

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
        let data = x.GetOrCreateBufferMarkData buffer
        let list = data.Marks

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
        _localMap.Item(buffer) <- { data with Marks = list }

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
                |> List.ofSeq

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
        let data = x.GetOrCreateBufferMarkData buffer
        let list = data.Marks |> List.filter (fun (c,_) -> c <> ident)
        _localMap.Item(buffer) <- {data with Marks = list }
        isDelete

    /// Delete all of the marks being tracked
    member x.DeleteAllMarks () = 
        _localMap.Values 
        |> Seq.map (fun d -> d.Marks )
        |> Seq.concat
        |> Seq.iter (fun (_,tlc) -> tlc.Close())
        _globalList |> Seq.iter (fun (_,tlc) -> tlc.Close())
        _localMap.Clear()
        _globalList <- List.empty
    
    /// Delete all of the marks for the specified buffer
    member x.DeleteAllMarksForBuffer buffer =
        let found,data = _localMap.TryGetValue(buffer)
        if found then 
            data.Marks |> List.iter (fun (_,tlc) -> tlc.Close())
            _localMap.Remove(buffer) |> ignore

        _globalList
        |> Seq.filter (fun (_,tlc) -> tlc.TextBuffer = buffer)
        |> Seq.iter (fun (_,tlc) -> tlc.Close())
        _globalList <- _globalList |> Seq.filter (fun (_,tlc) -> tlc.TextBuffer <> buffer) |> List.ofSeq
        
    member x.GetLocalMarks buffer = 
        _localMarkSet
        |> Set.toSeq
        |> Seq.map (fun c -> OptionUtil.combineRev c (x.GetLocalMark buffer c))
        |> SeqUtil.filterToSome

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

            let updateLastSelection () =
                let selection = buffer.TextView.Selection
                if selection.IsEmpty then ()
                else 
                    match TextSelectionUtil.GetOverarchingSelectedSpan selection with
                    | None -> ()
                    | Some(span) -> 
                        let lastSelected = span.Snapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeExclusive) |> Some
                        let data = x.GetOrCreateBufferMarkData buffer.TextBuffer
                        let data = {data with LastSelection=lastSelected }
                        _localMap.Item(buffer.TextBuffer) <- data 
            updateLastSelection()
    
            buffer.TextView.Selection.SelectionChanged 
            |> Event.add (fun _ -> updateLastSelection() )

            buffer.Closed |> Event.add (fun _ -> x.DeleteAllMarksForBuffer buffer.TextBuffer)
