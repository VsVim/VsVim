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

    member x.Line 
        with get() = _line
        and set value = _line <- value

    member x.Column = _column

    member private x.VirtualSnapshotPoint = 
        match _line with
        | None -> None
        | Some(line) -> Some (VirtualSnapshotPoint(line, _column))

    /// Update based on the new snapshot.  
    member x.UpdateForNewSnapshot (e:TextContentChangedEventArgs) =
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
                if lineNumber < newSnapshot.LineCount then makeInvalid()
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




