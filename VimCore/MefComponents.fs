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

[<Export(typeof<IBlockCaretFactoryService>)>]
type internal BlockCaretFactoryService [<ImportingConstructor>] ( _formatMapService : IEditorFormatMapService ) =
    
    let _blockCaretAdornmentLayerName = "BlockCaretAdornmentLayer"

    [<Export(typeof<AdornmentLayerDefinition>)>]
    [<Name("BlockCaretAdornmentLayer")>]
    [<Order(After = PredefinedAdornmentLayers.Selection)>]
    let mutable _blockCaretAdornmentLayerDefinition : AdornmentLayerDefinition = null

    /// This method is a hack.  Unless a let binding is explicitly used the F# compiler 
    /// will remove it from the final metadata definition.  This will prevent the MEF
    /// import from ever being resolved and hence cause us to not define the adornment
    /// layer.  Hacky member method that is never called to fake assign and prevent this
    /// problem
    member private x.Hack() =
        _blockCaretAdornmentLayerDefinition = AdornmentLayerDefinition()

    interface IBlockCaretFactoryService with
        member x.CreateBlockCaret textView = 
            let formatMap = _formatMapService.GetEditorFormatMap(textView)
            BlockCaret(textView, _blockCaretAdornmentLayerName, formatMap) :> IBlockCaret

[<Export(typeof<IVimFactoryService>)>]
type internal VimFactoryService
    [<ImportingConstructor>]
    ( _vim : IVim ) =

    interface IVimFactoryService with
        member x.Vim = _vim
        member x.CreateKeyProcessor buffer = Vim.KeyProcessor(buffer) :> Microsoft.VisualStudio.Text.Editor.KeyProcessor
        member x.CreateMouseProcessor buffer = Vim.MouseProcessor(buffer, MouseDeviceImpl() :> IMouseDevice) :> Microsoft.VisualStudio.Text.Editor.IMouseProcessor

type internal CompletionWindowBroker 
    ( 
        _textView : ITextView,
        _completionBroker : ICompletionBroker,
        _signatureBroker : ISignatureHelpBroker ) = 
    interface ICompletionWindowBroker with
        member x.TextView = _textView
        member x.IsCompletionWindowActive = 
            _completionBroker.IsCompletionActive(_textView) || _signatureBroker.IsSignatureHelpActive(_textView)
        member x.DismissCompletionWindow() = 
            if _completionBroker.IsCompletionActive(_textView) then
                _completionBroker.DismissAllSessions(_textView)
            if _signatureBroker.IsSignatureHelpActive(_textView) then
                _signatureBroker.DismissAllSessions(_textView)

[<Export(typeof<ICompletionWindowBrokerFactoryService>)>]
type internal CompletionWindowBrokerFactoryService
    [<ImportingConstructor>]
    (
        _completionBroker : ICompletionBroker,
        _signatureBroker : ISignatureHelpBroker ) = 

    interface ICompletionWindowBrokerFactoryService with
        member x.CreateCompletionWindowBroker textView = 
            let broker = CompletionWindowBroker(textView, _completionBroker, _signatureBroker)
            broker :> ICompletionWindowBroker


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

    /// Update based on the new snapshot.  
    member x.UpdateForChange (e:TextContentChangedEventArgs) =
        let newSnapshot = e.After
        let changes = e.Changes

        let updateValidLine (oldLine:ITextSnapshotLine) = 
            let span = oldLine.ExtentIncludingLineBreak.Span
            let makeInvalid () = 
                _line <- None
                _lastValidVersion <- Some (oldLine.Snapshot.Version.VersionNumber, oldLine.LineNumber)

            let deleted =  changes |> Seq.filter (fun c -> c.LineCountDelta <> 0 && c.OldSpan.Contains(span)) 
            if not (deleted |> Seq.isEmpty) then makeInvalid()
            else
                let lineDiff = 
                    changes 
                    |> Seq.filter (fun c -> c.OldPosition <= oldLine.Start.Position)
                    |> Seq.map (fun c -> c.LineCountDelta) 
                    |> Seq.sum
                let lineNumber = oldLine.LineNumber + lineDiff
                if lineNumber >= newSnapshot.LineCount then makeInvalid()
                else  _line <- Some (newSnapshot.GetLineFromLineNumber(lineNumber))

        let checkUndo lastVersion lastLineNumber = 
            let newVersion = e.AfterVersion
            if newVersion.ReiteratedVersionNumber = lastVersion && lastLineNumber <= newSnapshot.LineCount then 
                _line <- Some (newSnapshot.GetLineFromLineNumber(lastLineNumber))
                _lastValidVersion <- None

        match _line,_lastValidVersion with
        | Some(line),_ -> updateValidLine line
        | None,Some(version,lineNumber) -> checkUndo version lineNumber
        | _ -> ()


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
    List : TrackingLineColumn list
    Observer : System.IDisposable 
}

[<Export(typeof<ITrackingLineColumnService>)>]
type internal TrackingLineColumnService() = 
    
    let _map = new Dictionary<ITextBuffer, TrackedData>()

    /// Remove the TrackingLineColumn from the map.  If it is the only remaining 
    /// TrackingLineColumn assigned to the ITextBuffer, remove it from the map
    /// and unsubscribe from the Changed event
    member private x.Remove (tlc:TrackingLineColumn) = 
        let textBuffer = tlc.TextBuffer
        let found,data = _map.TryGetValue(textBuffer)
        if not found then ()
        else
            let l = data.List |> List.filter (fun item -> item <> tlc)
            if l |> List.isEmpty then 
                data.Observer.Dispose()
                _map.Remove(textBuffer) |> ignore
            else
                _map.Item(textBuffer) <- { data with List = l} 

    /// Add the TrackingLineColumn to the map.  If this is the first item in the
    /// map then subscribe to the Changed event on the buffer
    member private x.Add (tlc:TrackingLineColumn) =
        let textBuffer = tlc.TextBuffer 
        let found,data= _map.TryGetValue(textBuffer)
        if found then 
            let data = { data with List = [tlc] @ data.List }
            _map.Item(textBuffer) <- data
        else 
            let observer = textBuffer.Changed |> Observable.subscribe x.OnBufferChanged
            let data = { List = [tlc]; Observer = observer }
            _map.Add(textBuffer,data)

    member private x.OnBufferChanged (e:TextContentChangedEventArgs) = 
        let data = _map.Item(e.After.TextBuffer)
        data.List |> List.iter (fun tlc -> tlc.UpdateForChange e)

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
            |> Seq.map (fun tlc -> tlc :> ITrackingLineColumn)
            |> Seq.iter (fun tlc -> tlc.Close() )

