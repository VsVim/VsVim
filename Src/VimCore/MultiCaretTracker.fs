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
        _textView.Caret.PositionChanged
        |> Observable.subscribe (fun _ -> this.OnPositionChanged())
        |> _bag.Add

        _vimBuffer.SwitchedMode
        |> Observable.subscribe (fun args -> this.OnSwitchMode args)
        |> _bag.Add

        _vimBuffer.Closed
        |> Observable.subscribe (fun _ -> this.OnBufferClosed())
        |> _bag.Add

    /// Raised when the caret position changes
    member x.OnPositionChanged () = 
        let newCaretsPoints = _vimHost.GetCaretPoints(_textView).ToList()
        _caretPoints <- newCaretsPoints

    member x.OnSwitchMode args =
        match args.ModeArgument with
        | ModeArgument.CancelOperation ->
            _vimHost.GetCaretPoints _textView
            |> Seq.take 1
            |> _vimHost.SetCaretPoints _textView
        | _ ->
            ()

    /// Raised when the buffer is closed
    member x.OnBufferClosed() = 
        _bag.DisposeAll()

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
