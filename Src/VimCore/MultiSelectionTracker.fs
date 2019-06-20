#light

namespace Vim
open System.ComponentModel.Composition
open Microsoft.VisualStudio.Text

type internal MultiSelectionTracker
    ( 
        _vimBuffer: IVimBuffer,
        _commonOperations: ICommonOperations,
        _mouseDevice: IMouseDevice
    ) as this =

    let _globalSettings = _vimBuffer.GlobalSettings
    let _textView = _vimBuffer.TextView
    let _bag = DisposableBag()

    let mutable _oldSelectedSpans: SelectedSpan array = [||]

    do
        _commonOperations.SelectedSpansSet
        |> Observable.subscribe (fun _ -> this.OnSelectedSpansSet())
        |> _bag.Add

        _vimBuffer.KeyInputStart
        |> Observable.subscribe (fun _ -> this.OnKeyInputStart())
        |> _bag.Add

        _vimBuffer.KeyInputProcessed
        |> Observable.subscribe (fun _ -> this.OnKeyInputProcessed())
        |> _bag.Add

        _vimBuffer.SwitchedMode
        |> Observable.subscribe (fun args -> this.OnSwitchedMode args)
        |> _bag.Add

        _vimBuffer.Closed
        |> Observable.subscribe (fun _ -> this.OnBufferClosed())
        |> _bag.Add

   /// The caret points at the start of the most recent key input 
    member x.OldSelectedSpans 
        with get () =  _oldSelectedSpans
        and set value = _oldSelectedSpans <- value

    /// Raised when the selected spans are set
    member x.OnSelectedSpansSet () = 
        x.OldSelectedSpans <- x.GetSelectedSpans()

    /// Raised when the buffer starts processing input
    member x.OnKeyInputStart () = 
        x.OldSelectedSpans <- x.GetSelectedSpans()

    /// Raised when the caret position changes
    member x.OnKeyInputProcessed args = 
        if
            _vimBuffer.ModeKind <> ModeKind.Disabled
            && _vimBuffer.ModeKind <> ModeKind.ExternalEdit
            && not (VimExtensions.IsAnySelect _vimBuffer.ModeKind)
            && not (VimExtensions.IsAnyVisual _vimBuffer.ModeKind)
            && not _textView.IsClosed
        then
            x.RestoreSelectedSpans()

    /// Raised when the vim buffer switches modes
    member x.OnSwitchedMode args =
        match args.ModeArgument with
        | ModeArgument.CancelOperation ->
            let newSelectedSpans =
                _commonOperations.SelectedSpans
                |> Seq.take 1
                |> Seq.toArray
            _commonOperations.SetSelectedSpans newSelectedSpans
            x.OldSelectedSpans <- newSelectedSpans
        | _ ->
            ()

    /// Raised when the vim buffer is closed
    member x.OnBufferClosed() = 
        _bag.DisposeAll()

    /// Get all the caret points
    member x.GetSelectedSpans () =
        _commonOperations.SelectedSpans |> Seq.toArray

    /// Restore selected spans present at the start of key processing
    member x.RestoreSelectedSpans () =
        let snapshot = _textView.TextBuffer.CurrentSnapshot
        let oldSelectedSpans = x.OldSelectedSpans
        let newSelectedSpans = x.GetSelectedSpans()
        if oldSelectedSpans.Length > 1 then
            let oldPosition =
                oldSelectedSpans.[0].CaretPoint.Position
                |> _commonOperations.MapPointNegativeToCurrentSnapshot
            let newPosition = newSelectedSpans.[0].CaretPoint.Position
            let delta = newPosition - oldPosition
            seq {

                // Return the first selected span as is.
                yield newSelectedSpans.[0]

                // Adjust the second and subsequent old carets to the relative
                // offset of the first caret.
                for caretIndex = 1 to oldSelectedSpans.Length - 1 do
                    let oldPoint =
                        oldSelectedSpans.[caretIndex].CaretPoint.Position
                        |> _commonOperations.MapPointNegativeToCurrentSnapshot
                    let oldPosition = oldPoint.Position
                    let newPosition = oldPosition + delta
                    let newCaretPoint =
                        if newPosition >= 0 && newPosition <= snapshot.Length then
                            VirtualSnapshotPoint(snapshot, newPosition)
                        else
                            oldPoint
                            |> VirtualSnapshotPointUtil.OfPoint

                    yield SelectedSpan(newCaretPoint)

                // Return any remaining new selected spans.
                for caretIndex = oldSelectedSpans.Length to newSelectedSpans.Length - 1 do
                    yield newSelectedSpans.[caretIndex]
            }
            |> _commonOperations.SetSelectedSpans

[<Export(typeof<IVimBufferCreationListener>)>]
type internal MultiSelectionTrackerFactory
    [<ImportingConstructor>]
    (
        _mouseDevice: IMouseDevice,
        _commonOperationsFactory: ICommonOperationsFactory
    ) =

    interface IVimBufferCreationListener with
        member x.VimBufferCreated vimBuffer = 

            // It's OK to just ignore this after creation.  It subscribes to
            // several event handlers which will keep it alive for the duration
            // of the IVimBuffer.
            let commonOperations = _commonOperationsFactory.GetCommonOperations vimBuffer.VimBufferData
            let multiCaretTracker = MultiSelectionTracker(vimBuffer, commonOperations, _mouseDevice)
            ()
