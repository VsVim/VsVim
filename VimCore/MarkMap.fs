#light

namespace Vim
open Microsoft.VisualStudio.Text
open System.Collections.Generic

type MarkData =  {
    Line : int;
    Column : int;
}

/// Internal representation of a Mark.  All marks are tracked the same
type Mark = 
    | Valid of MarkData

    /// For an invalid mark, the last int in the tuple represents the version number at which
    /// this mark was last valid.  
    | Invalid of MarkData * int 

[<Sealed>]
type MarkMap() =

    let mutable _localMap = new Dictionary<ITextBuffer,(char*Mark) list>();
    let mutable _globalList : (ITextBuffer*char*Mark) seq = Seq.empty
    let mutable _tracking: HashSet<ITextBuffer> = new HashSet<ITextBuffer>()

    /// Is this mark local to a buffer
    static member IsLocalMark c =
        if System.Char.IsDigit(c) then false
        else if System.Char.IsLetter(c) && System.Char.IsUpper(c) then false
        else true

    static member PointToMarkData (point:SnapshotPoint) =
        let line = point.GetContainingLine()
        let column = line.Start.Difference(point)
        { Line=line.LineNumber; Column=column }

    static member MarkDataToPoint (tss:ITextSnapshot) (m:MarkData) =
        if m.Line >= tss.LineCount then None
        else
            let line = tss.GetLineFromLineNumber(m.Line)
            let point = VirtualSnapshotPoint(line, m.Column)
            Some point

    static member MarkToPointAndMarkData (tss:ITextSnapshot) (m:Mark) =
        match m with
        | Valid(md) -> 
            match MarkMap.MarkDataToPoint tss md with 
            | Some (point) -> Some(md,point)
            | None -> None
        | Invalid(_) -> None

    static member MarkToPoint (tss:ITextSnapshot) (m:Mark) =
        match MarkMap.MarkToPointAndMarkData tss m with
        | Some (_,point) -> Some(point)
        | None -> None

    member private x.GetLocalList (buffer:ITextBuffer) =
        let empty : (char*Mark) list = List.empty
        let mutable list = empty 
        if _localMap.TryGetValue(buffer, &list) then
            list
        else
            empty

    /// Update all of the marks in the specified buffer to deal with the changes.  
    member private x.UpdateMarks (oldSnapshot:ITextSnapshot) (changes:INormalizedTextChangeCollection) =
        let buffer = oldSnapshot.TextBuffer

        // Is this line deleted with this change?
        let isLineDeleted lineNumber = 
            let line = oldSnapshot.GetLineFromLineNumber(lineNumber)
            let span = line.ExtentIncludingLineBreak.Span
            let deleted =  changes |> Seq.filter (fun c -> c.LineCountDelta <> 0 && c.OldSpan.Contains(span))
            not (deleted |> Seq.isEmpty)

        // Update a single mark.  Make sure to check for deletion of the containing line
        let updateSingleMarkData (data:MarkData) (point:VirtualSnapshotPoint) =
            if isLineDeleted data.Line then
                Invalid (data, oldSnapshot.Version.VersionNumber)
            else
                let position = point.Position.Position
                let lineDiff = 
                    changes
                        |> Seq.filter (fun c -> c.OldPosition < position )
                        |> Seq.map (fun c -> c.LineCountDelta)
                        |> Seq.sum
                if lineDiff <> 0 then
                    let newLineNumber = data.Line + lineDiff
                    let data = {data with Line=newLineNumber}
                    Valid(data)
                else
                    Valid(data)

        let updateSingleMark (m:Mark) =
            match MarkMap.MarkToPointAndMarkData oldSnapshot m with
            | None -> m
            | Some(md,point) -> updateSingleMarkData md point

        // First update the local marks
        let list =
            x.GetLocalList buffer            
            |> Seq.ofList
            |> Seq.map (fun (c,m) -> (c,updateSingleMark m))
            |> List.ofSeq
        _localMap.Item(buffer) <- list

        // Now update the global marks.  Don't rebuild the list unless there is at least
        // one mark in the list for this buffer
        if not (_globalList |> Seq.filter ( fun (buf,_,_) -> buf = buffer) |> Seq.isEmpty) then
            _globalList <- _globalList |> Seq.map (fun (b,c,m) -> (b,c,updateSingleMark m))


    /// Tracks all changes to the buffer and updates the corresponding marks as 
    /// appropriate
    member private x.OnBufferChanged (sender:obj) (e:TextContentChangedEventArgs) =

        // Line changes are the only action that can affect a mark so don't bother processing 
        // if there hasn't been a line change
        let anyLineDiff = not (e.Changes |> Seq.filter (fun c -> c.LineCountDelta <> 0) |> Seq.isEmpty)
        if anyLineDiff then x.UpdateMarks e.Before e.Changes

    member private x.EnsureTracking (buffer:ITextBuffer) =
        if _tracking.Add(buffer) then
            let handler = new System.EventHandler<TextContentChangedEventArgs>(x.OnBufferChanged)
            buffer.Changed.AddHandler handler

    member private x.EnsureNotTracking (buffer:ITextBuffer) =
        if _tracking.Remove(buffer) then
            let handler = new System.EventHandler<TextContentChangedEventArgs>(x.OnBufferChanged)
            buffer.Changed.RemoveHandler handler

    member x.TrackedBuffers = _tracking :> ITextBuffer seq

    /// Try and get the local mark for the specified char <param name="c"/>.  If it does not
    /// exist then the value None will be returned.  The mark is always returned for the 
    /// current ITextSnapshot of the <param name="buffer"> 
    member x.GetLocalMark (buffer:ITextBuffer) (ident:char) =
        let list = x.GetLocalList buffer
        match list |> Seq.tryFind (fun (c,m) -> c = ident) with
        | Some(_,mark) -> MarkMap.MarkToPoint buffer.CurrentSnapshot mark
        | None -> None

    /// Get the mark relative to the specified buffer
    member x.GetMark (buffer:ITextBuffer) (ident:char) =
        if MarkMap.IsLocalMark ident then
            x.GetLocalMark buffer ident
        else
            let found = 
                _globalList 
                |> Seq.tryFind (fun (b,c,m) -> b = buffer && c = ident)
            match found with 
            | Some(_,_,m) -> MarkMap.MarkToPoint buffer.CurrentSnapshot m
            | None -> None

    /// Set the mark for the <param name="point"/> inside the corresponding buffer.  
    member x.SetMark (point:SnapshotPoint) (ident:char) = 
        let buffer = point.Snapshot.TextBuffer
        let mark = Valid(MarkMap.PointToMarkData point)
        if MarkMap.IsLocalMark ident then
            let list = x.GetLocalList buffer
            let list = list |> List.filter (fun (c,md) -> c <> ident)
            let list = (ident,mark) :: list
            _localMap.Item(buffer) <- list
        else    
            _globalList <-
                _globalList
                |> Seq.filter (fun (b,c,m) -> c <> ident)
                |> Seq.append (Seq.singleton (buffer,ident,mark))
        x.EnsureTracking buffer

    /// Delete the specified local mark.  
    /// <returns>True if the mark was deleted.  False if no action was taken </returns>
    member x.DeleteMark (buffer:ITextBuffer) (ident:char) =
        let isDelete = x.GetMark buffer ident |> Option.isSome
        if MarkMap.IsLocalMark ident then
            let list = 
                x.GetLocalList buffer
                |> List.filter (fun (c,_) -> c <> ident)
            if list |> List.isEmpty then
                _localMap.Remove(buffer) |> ignore
            else
                _localMap.Item(buffer) <- list
        else
            _globalList <-
                _globalList
                |> Seq.filter (fun(b,c,m) -> b <> buffer && c <> ident)
        
        let allMarksGoneInBuffer =
            not (_localMap.ContainsKey(buffer))
            && (_globalList |> Seq.filter (fun (b,_,_) -> b = buffer) |> Seq.isEmpty)
        if allMarksGoneInBuffer then
            x.EnsureNotTracking buffer
        isDelete

    /// Delete all of the marks being tracked
    member x.DeleteAllMarks () = 
        _tracking 
            |> List.ofSeq
            |> List.iter (fun b -> x.EnsureNotTracking(b))
        _localMap.Clear()
        _globalList <- Seq.empty
    
    /// Delete all of the marks for the specified buffer
    member x.DeleteAllMarksForBuffer buffer =
        x.EnsureNotTracking buffer
        _localMap.Remove(buffer) |> ignore
        _globalList <- _globalList |> Seq.filter (fun (b,_,_) -> b <> buffer)
