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
    let _vimData = _vimBuffer.Vim.VimData
    let _textView = _vimBuffer.TextView
    let _bag = DisposableBag()

    let mutable _recordedSelectedSpans: SelectedSpan array = [||]

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
    member x.RecordedSelectedSpans
        with get () =  _recordedSelectedSpans
        and set (value: SelectedSpan array) =
            for caretIndex = _recordedSelectedSpans.Length to value.Length - 1 do
                x.InitializeSecondaryCaretRegisters caretIndex
            _recordedSelectedSpans <- value

    /// Raised when the selected spans are set
    member x.OnSelectedSpansSet () = 
        x.RecordedSelectedSpans <- x.GetSelectedSpans()

    /// Raised when the buffer starts processing input
    member x.OnKeyInputStart () = 
        x.RecordedSelectedSpans <- x.GetSelectedSpans()

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
            x.RecordedSelectedSpans <- newSelectedSpans
        | _ ->
            ()

    /// Raised when the vim buffer is closed
    member x.OnBufferClosed() = 
        _bag.DisposeAll()

    /// Get all the caret points
    member x.GetSelectedSpans () =
        _commonOperations.SelectedSpans |> Seq.toArray

    /// Copy the primary caret's registers to a secondary caret
    member x.InitializeSecondaryCaretRegisters caretIndex =
        let oldCaretIndex = _vimData.CaretIndex
        let register = _commonOperations.GetRegister None
        try
            _vimData.CaretIndex <- 0
            let oldValue = register.RegisterValue
            _vimData.CaretIndex <- caretIndex
            register.RegisterValue <- oldValue
        finally
            _vimData.CaretIndex <- oldCaretIndex

    /// Restore selected spans present at the start of key processing
    member x.RestoreSelectedSpans () =
        let snapshot = _textView.TextBuffer.CurrentSnapshot
        let oldSelectedSpans = x.RecordedSelectedSpans
        let newSelectedSpans = x.GetSelectedSpans()
        if
            oldSelectedSpans.[0] = newSelectedSpans.[0]
            && oldSelectedSpans.Length = newSelectedSpans.Length
        then

            // The caret didn't move and the number of selected spans didn't
            // change.
            ()

        elif oldSelectedSpans.Length > 1 then

            // We previously had selected spans.
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
