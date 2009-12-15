#light

namespace Vim
open Microsoft.VisualStudio.Text
open System.Collections.Generic

type MarkIdentifier(buffer: ITextBuffer, identifier : char, isGlobal : bool) =
    static let mutable s_id = 0
    let _id = 
        s_id <- s_id + 1
        s_id

    member x.Id = _id    
    member x.TextBuffer = buffer
    member x.Identifier = identifier

    /// Does this represent a global mark
    member x.IsGlobal = isGlobal

    member private x.CompareTo (other:MarkIdentifier) =
        if x.Identifier = other.Identifier then
            if x.IsGlobal then 0
            else if x.TextBuffer = other.TextBuffer then 0
            else x.Id - other.Id
        else
            (int32 x.Identifier) - (int32 other.Identifier)

    override x.GetHashCode() = 
        if x.IsGlobal then
            int32 identifier
        else
            buffer.GetHashCode() + (int32 identifier)

    override x.Equals(obj) =
        match obj with
        | :? MarkIdentifier as other -> 0 = x.CompareTo other
        | _ -> false
   
    interface System.IComparable with
        member x.CompareTo yObj =
            match yObj with
            | :? MarkIdentifier as y -> x.CompareTo y
            | _ -> invalidArg "yObj" "Cannot compare values of different types"  
    

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

    let mutable _map: Map<MarkIdentifier, Mark> = Map.empty
    let mutable _tracking: HashSet<ITextBuffer> = new HashSet<ITextBuffer>()

    /// Is this mark local to a buffer
    static member IsLocalMark c =
        if System.Char.IsDigit(c) then false
        else if System.Char.IsLetter(c) && System.Char.IsUpper(c) then false
        else true

    static member CreateMarkIdentifier buffer ident = 
        let isGlobal = not (MarkMap.IsLocalMark ident)
        MarkIdentifier(buffer, ident, isGlobal)

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

    static member MarkToPoint (tss:ITextSnapshot) (m:Mark) =
        match m with
        | Valid(md) -> 
            match MarkMap.MarkDataToPoint tss md with 
            | Some (point) -> Some(md,point)
            | None -> None
        | Invalid(_) -> None

    /// Update all of the marks in the specified buffer to deal with the changes.  
    member x.UpdateMarks (oldSnapshot:ITextSnapshot) (changes:INormalizedTextChangeCollection) =
        let buffer = oldSnapshot.TextBuffer

        // Is this line deleted with this change?
        let isLineDeleted lineNumber = 
            let line = oldSnapshot.GetLineFromLineNumber(lineNumber)
            let span = line.ExtentIncludingLineBreak.Span
            let deleted =  changes |> Seq.filter (fun c -> c.LineCountDelta <> 0 && c.OldSpan.Contains(span))
            not (deleted |> Seq.isEmpty)

        // Update a single mark.  Make sure to check for deletion of the containing line
        let updateSingleMark (mi:MarkIdentifier,data:MarkData,point:VirtualSnapshotPoint) : unit =
            if isLineDeleted data.Line then
                let invalid = Invalid (data, oldSnapshot.Version.VersionNumber)
                _map <- _map.Add(mi,invalid)
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
                    _map <- _map.Add(mi,(Valid(data)))

        let tryMap (mi:MarkIdentifier) (m:Mark) =
            match MarkMap.MarkToPoint oldSnapshot m with
            | None -> None
            | Some(md,point) -> Some(mi,md,point)
        _map 
            |> Seq.filter (fun p -> p.Key.TextBuffer = buffer) 
            |> Seq.choose (fun p -> tryMap p.Key p.Value)
            |> List.ofSeq
            |> List.iter updateSingleMark


    /// Tracks all changes to the buffer and updates the corresponding marks as 
    /// appropriate
    member x.OnBufferChanged (sender:obj) (e:TextContentChangedEventArgs) =

        // Line changes are the only action that can affect a mark so don't bother processing 
        // if there hasn't been a line change
        let anyLineDiff = not (e.Changes |> Seq.filter (fun c -> c.LineCountDelta <> 0) |> Seq.isEmpty)
        if anyLineDiff then x.UpdateMarks e.Before e.Changes

    member x.EnsureTracking (buffer:ITextBuffer) =
        if _tracking.Add(buffer) then
            let handler = new System.EventHandler<TextContentChangedEventArgs>(x.OnBufferChanged)
            buffer.Changed.AddHandler handler

    member x.EnsureNotTracking (buffer:ITextBuffer) =
        if _tracking.Remove(buffer) then
            let handler = new System.EventHandler<TextContentChangedEventArgs>(x.OnBufferChanged)
            buffer.Changed.RemoveHandler handler

    /// Get the mark relative to the specified buffer
    member x.GetMark (buffer:ITextBuffer) (c:char) =
        let ident = MarkMap.CreateMarkIdentifier buffer c
        match Map.tryFind ident _map with
        | None -> None
        | Some(mark) ->
            match mark with
            | Valid(data) -> MarkMap.MarkDataToPoint buffer.CurrentSnapshot data
            | Invalid(_) -> None

    /// Try and get the local mark for the specified char <param name="c"/>.  If it does not
    /// exist then the value None will be returned.  The mark is always returned for the 
    /// current ITextSnapshot of the <param name="buffer"> 
    member x.GetLocalMark (buffer:ITextBuffer) (c:char) =
        let ident = MarkMap.CreateMarkIdentifier buffer c
        if ident.IsGlobal then None
        else
            x.GetMark buffer c
    
    /// Set the mark for the <param name="point"/> inside the corresponding buffer.  
    member x.SetMark (point:SnapshotPoint) (c:char) = 
        let ident = MarkMap.CreateMarkIdentifier (point.Snapshot.TextBuffer) c
        let md = MarkMap.PointToMarkData point
        _map <- _map.Add(ident, Valid(md))
        x.EnsureTracking point.Snapshot.TextBuffer

    /// Delete the specified local mark.  
    /// <returns>True if the mark was deleted.  False if no action was taken </returns>
    member x.DeleteMark (buffer:ITextBuffer) (c:char) =
        let ident =  MarkMap.CreateMarkIdentifier buffer c
        let isDelete = _map.ContainsKey ident
        _map <- _map.Remove(ident)
        let allMarksGoneInBuffer =
            _map
                |> Seq.filter (fun p -> p.Key.TextBuffer = buffer)
                |> Seq.isEmpty
        if allMarksGoneInBuffer then
            x.EnsureNotTracking buffer
        isDelete

    /// Delete all of the marks being tracked
    member x.DeleteAllMarks () = 
        _map |> Seq.iter (fun p -> x.EnsureNotTracking(p.Key.TextBuffer))
        _map <- Map.empty
    
    /// Delete all of the marks for the specified buffer
    member x.DeleteAllMarksForBuffer buffer =
        x.EnsureNotTracking buffer
        _map <- 
            _map 
            |> Seq.filter (fun pair -> pair.Key.TextBuffer <> buffer)
            |> Seq.map (fun pair -> (pair.Key,pair.Value))
            |> Map.ofSeq
