#light
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Tagging
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Language.Intellisense
open Microsoft.VisualStudio.Text.Classification
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition
open System.Collections.Generic

type internal DisplayWindowBroker 
    ( 
        _textView : ITextView,
        _completionBroker : ICompletionBroker,
        _signatureBroker : ISignatureHelpBroker,
        _smartTagBroker : ISmartTagBroker ) = 
    interface IDisplayWindowBroker with
        member x.TextView = _textView
        member x.IsCompletionWindowActive = 
            _completionBroker.IsCompletionActive(_textView) || _signatureBroker.IsSignatureHelpActive(_textView)
        member x.IsSmartTagWindowActive = _smartTagBroker.IsSmartTagActive(_textView)
        member x.DismissCompletionWindow() = 
            if _completionBroker.IsCompletionActive(_textView) then
                _completionBroker.DismissAllSessions(_textView)
            if _signatureBroker.IsSignatureHelpActive(_textView) then
                _signatureBroker.DismissAllSessions(_textView)

[<Export(typeof<IDisplayWindowBrokerFactoryService>)>]
type internal DisplayWindowBrokerFactoryService
    [<ImportingConstructor>]
    (
        _completionBroker : ICompletionBroker,
        _signatureBroker : ISignatureHelpBroker,
        _smartTagBroker : ISmartTagBroker ) = 

    interface IDisplayWindowBrokerFactoryService with
        member x.CreateDisplayWindowBroker textView = 
            let broker = DisplayWindowBroker(textView, _completionBroker, _signatureBroker, _smartTagBroker)
            broker :> IDisplayWindowBroker


type internal TrackingLineColumn 
    ( 
        _textBuffer : ITextBuffer,
        _column : int,
        _onClose : TrackingLineColumn -> unit ) =

    /// This is the SnapshotSpan of the line that we are tracking.  It is None in the
    /// case of a deleted line
    let mutable _line : ITextSnapshotLine option  = None

    /// When the line this TrackingLineColumn is deleted, this will record the version 
    /// number of the last valid version containing the line.  That way if we undo this 
    /// can become valid again
    let mutable _lastValidVersion : (int * int) option  = None

    member x.TextBuffer = _textBuffer

    member x.Line 
        with get() = _line
        and set value = _line <- value

    member x.Column = _column

    member private x.VirtualSnapshotPoint = 
        match _line with
        | None -> None
        | Some(line) -> Some (VirtualSnapshotPoint(line, _column))

    /// Update the internal tracking information based on the new ITextSnapshot
    member x.UpdateForChange (e:TextContentChangedEventArgs) =
        let newSnapshot = e.After
        let changes = e.Changes

        // Before this change this was tracking an active line.  Make the appropriate
        // update here
        let updateValidLine (oldLine:ITextSnapshotLine) = 
            let span = oldLine.ExtentIncludingLineBreak.Span
            let makeInvalid () = 
                _line <- None
                _lastValidVersion <- Some (oldLine.Snapshot.Version.VersionNumber, oldLine.LineNumber)

            let deleted =  changes |> Seq.filter (fun c -> c.LineCountDelta <> 0 && c.OldSpan.Contains(span)) 
            if not (deleted |> Seq.isEmpty) then makeInvalid()
            else
                // The line wasn't deleted so go calculate the diff and update our state
                let lineDiff = 
                    changes 
                    |> Seq.filter (fun c -> c.OldPosition <= oldLine.Start.Position)
                    |> Seq.map (fun c -> c.LineCountDelta) 
                    |> Seq.sum
                let lineNumber = oldLine.LineNumber + lineDiff
                if lineNumber >= newSnapshot.LineCount then makeInvalid()
                else  _line <- Some (newSnapshot.GetLineFromLineNumber(lineNumber))

        // This line was deleted at some point in the past and hence we're invalid.  If the 
        // current change is an Undo back to the last version where we were valid then we
        // become valid again
        let checkUndo lastVersion lastLineNumber = 
            let newVersion = e.AfterVersion
            if newVersion.ReiteratedVersionNumber = lastVersion && lastLineNumber <= newSnapshot.LineCount then 
                _line <- Some (newSnapshot.GetLineFromLineNumber(lastLineNumber))
                _lastValidVersion <- None

        match _line,_lastValidVersion with
        | Some(line),_ -> updateValidLine line
        | None,Some(version,lineNumber) -> checkUndo version lineNumber
        | _ -> ()

    override x.ToString() =
        match x.VirtualSnapshotPoint with
        | Some(point) ->
            let line,_ = TssUtil.GetLineColumn point.Position
            sprintf "%d,%d - %s" line _column (point.ToString())
        | None -> "Invalid"

    interface ITrackingLineColumn with
        member x.TextBuffer = _textBuffer
        member x.VirtualPoint = x.VirtualSnapshotPoint
        member x.Point = 
            match x.VirtualSnapshotPoint with
            | None -> None
            | Some(point) -> 
                if point.IsInVirtualSpace then None
                else Some point.Position
        member x.PointTruncating =
            match x.VirtualSnapshotPoint with
            | None -> None
            | Some(point) -> Some point.Position
        member x.Close () =
            _onClose x
            _line <- None
            _lastValidVersion <- None

type internal TrackedData = {
    List : WeakReference<TrackingLineColumn> list
    Observer : System.IDisposable 
}

[<Export(typeof<ITrackingLineColumnService>)>]
type internal TrackingLineColumnService() = 
    
    let _map = new Dictionary<ITextBuffer, TrackedData>()

    /// Gets the data for the passed in buffer.  This method takes care of removing all 
    /// collected WeakReference items and updating the internal map 
    member private x.GetData textBuffer foundFunc notFoundFunc =
        let found,data = _map.TryGetValue(textBuffer)
        if not found then notFoundFunc()
        else
            let tlcs = 
                data.List 
                |> Seq.ofList 
                |> Seq.choose (fun weakRef -> weakRef.Target)
            if tlcs |> Seq.isEmpty then
                data.Observer.Dispose()
                _map.Remove(textBuffer) |> ignore
                notFoundFunc()
            else
                foundFunc data.Observer tlcs data.List

    /// Remove the TrackingLineColumn from the map.  If it is the only remaining 
    /// TrackingLineColumn assigned to the ITextBuffer, remove it from the map
    /// and unsubscribe from the Changed event
    member private x.Remove (tlc:TrackingLineColumn) = 
        let found (data:System.IDisposable) items rawList = 
            let items = items |> Seq.filter (fun cur -> cur <> tlc)
            if items |> Seq.isEmpty then 
                data.Dispose()
                _map.Remove(tlc.TextBuffer) |> ignore
            else
                let items = [Utils.CreateWeakReference tlc] @ rawList
                _map.Item(tlc.TextBuffer) <- { Observer = data; List = items }
        x.GetData tlc.TextBuffer found (fun () -> ())

    /// Add the TrackingLineColumn to the map.  If this is the first item in the
    /// map then subscribe to the Changed event on the buffer
    member private x.Add (tlc:TrackingLineColumn) =
        let textBuffer = tlc.TextBuffer
        let found data _ list =
            let list = [Utils.CreateWeakReference tlc] @ list
            _map.Item(textBuffer) <- { Observer = data; List = list }
        let notFound () =
            let observer = textBuffer.Changed |> Observable.subscribe x.OnBufferChanged
            let data = { List = [Utils.CreateWeakReference tlc]; Observer = observer }
            _map.Add(textBuffer,data)
        x.GetData textBuffer found notFound

    member private x.OnBufferChanged (e:TextContentChangedEventArgs) = 
        let found _ (items: TrackingLineColumn seq) _ =
            items |> Seq.iter (fun tlc -> tlc.UpdateForChange e)
        x.GetData e.Before.TextBuffer found (fun () -> ())

    member x.Create (textBuffer:ITextBuffer) lineNumber column = 
        let tlc = TrackingLineColumn(textBuffer, column, x.Remove)
        let tss = textBuffer.CurrentSnapshot
        let line = tss.GetLineFromLineNumber(lineNumber)
        tlc.Line <-  Some line
        x.Add tlc
        tlc

    member x.CreateDisconnected textBuffer = 
        { new ITrackingLineColumn with 
            member x.TextBuffer = textBuffer
            member x.Point = None
            member x.PointTruncating = None
            member x.VirtualPoint = None
            member x.Close() = () }

    interface ITrackingLineColumnService with
        member x.Create textBuffer lineNumber column = x.Create textBuffer lineNumber column :> ITrackingLineColumn
        member x.CreateForPoint (point:SnapshotPoint) =
            let buffer = point.Snapshot.TextBuffer
            if point.Snapshot <> buffer.CurrentSnapshot then 
                x.CreateDisconnected buffer
            else
                let line,column = TssUtil.GetLineColumn point
                (x.Create buffer line column) :> ITrackingLineColumn
        member x.CreateDisconnected textBuffer = x.CreateDisconnected textBuffer
            
        member x.CloseAll() =
            let values = _map.Values |> List.ofSeq
            values 
            |> Seq.ofList
            |> Seq.map (fun data -> data.List)
            |> Seq.concat
            |> Seq.choose (fun item -> item.Target)
            |> Seq.map (fun tlc -> tlc :> ITrackingLineColumn)
            |> Seq.iter (fun tlc -> tlc.Close() )

