#light

namespace Vim
open System.Linq
open System.Collections.Generic
open System.ComponentModel.Composition
open Microsoft.VisualStudio.Text

type internal MultiCaretTracker
    ( 
        _vimBuffer: IVimBuffer,
        _commonOperations: ICommonOperations,
        _mouseDevice: IMouseDevice
    ) as this =

    let _globalSettings = _vimBuffer.GlobalSettings
    let _textView = _vimBuffer.TextView
    let _vimHost = _vimBuffer.Vim.VimHost
    let _bag = DisposableBag()

    let mutable _caretPoints = List<VirtualSnapshotPoint>()

    do
        _vimHost.CaretPointsSet
        |> Observable.subscribe (fun _ -> this.OnCaretPointsSet())
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
    member x.CaretPoints 
        with get () =  _caretPoints
        and set value = _caretPoints <- value

    /// Raised when the caret points are set by the host
    member x.OnCaretPointsSet () = 
        x.CaretPoints <- x.GetCaretPoints()

    /// Raised when the buffer starts processing input
    member x.OnKeyInputStart () = 
        x.CaretPoints <- x.GetCaretPoints()

    /// Raised when the caret position changes
    member x.OnKeyInputProcessed args = 
        if
            _vimBuffer.ModeKind <> ModeKind.Disabled
            && _vimBuffer.ModeKind <> ModeKind.ExternalEdit
            && not _textView.IsClosed
        then
            x.RestoreCarets()

    /// Raised when the vim buffer switches modes
    member x.OnSwitchedMode args =
        match args.ModeArgument with
        | ModeArgument.CancelOperation ->
            let newCaretPoints =
                _vimHost.GetCaretPoints _textView
                |> Seq.take 1
                |> GenericListUtil.OfSeq
            _vimHost.SetCaretPoints _textView newCaretPoints
            x.CaretPoints <- newCaretPoints
        | _ ->
            ()

    /// Raised when the vim buffer is closed
    member x.OnBufferClosed() = 
        _bag.DisposeAll()

    /// Get all the caret points
    member x.GetCaretPoints () =
        _vimHost.GetCaretPoints(_textView).ToList()

    /// Restore carets present at the start of key processing
    member x.RestoreCarets () =
        let snapshot = _textView.TextBuffer.CurrentSnapshot
        let oldCaretPoints = x.CaretPoints
        let newCaretPoints = x.GetCaretPoints()
        if oldCaretPoints.Count > 1 then
            let oldPosition =
                oldCaretPoints.[0].Position
                |> _commonOperations.MapPointNegativeToCurrentSnapshot
            let newPosition = newCaretPoints.[0].Position
            let delta = newPosition - oldPosition
            let adjustedCaretPoints =
                seq {
                    for caretIndex = 0 to oldCaretPoints.Count - 1 do
                        let oldPoint =
                            oldCaretPoints.[caretIndex].Position
                            |> _commonOperations.MapPointNegativeToCurrentSnapshot
                        let oldPosition = oldPoint.Position
                        let newPosition = oldPosition + delta
                        let newCaretPoint =
                            if newPosition >= 0 && newPosition <= snapshot.Length then
                                VirtualSnapshotPoint(snapshot, newPosition)
                            else
                                oldCaretPoints.[caretIndex]

                        yield newCaretPoint
                    for caretIndex = oldCaretPoints.Count to newCaretPoints.Count - 1 do
                        yield newCaretPoints.[caretIndex]
                }
                |> GenericListUtil.OfSeq
            _vimHost.SetCaretPoints _textView adjustedCaretPoints
            x.CaretPoints <- adjustedCaretPoints

[<Export(typeof<IVimBufferCreationListener>)>]
type internal MultiCaretTrackerFactory
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
            let multiCaretTracker = MultiCaretTracker(vimBuffer, commonOperations, _mouseDevice)
            ()
