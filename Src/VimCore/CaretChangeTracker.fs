namespace Vim

open System.ComponentModel.Composition

/// Ensure view properties after external caret events
type internal CaretChangeTracker(_vimBuffer: IVimBuffer, _commonOperations: ICommonOperations, _mouseDevice: IMouseDevice) as this =

    let _globalSettings = _vimBuffer.GlobalSettings
    let _textView = _vimBuffer.TextView
    let _vimHost = _vimBuffer.Vim.VimHost
    let _bag = DisposableBag()

    do
        _textView.Caret.PositionChanged
        |> Observable.subscribe (fun _ -> this.OnPositionChanged())
        |> _bag.Add

    /// React to external caret position changes by ensuring that our view
    /// properties are met, such as the caret being visible and 'scrolloff'
    /// being taken into account
    member x.OnPositionChanged() =

        // If we are processing input then the mode is responsible for
        // controlling the window, so let the mode handle it.
        if _vimHost.IsFocused _textView && not _vimBuffer.Vim.IsDisabled && _vimBuffer.ModeKind <> ModeKind.Disabled
           && _vimBuffer.ModeKind <> ModeKind.ExternalEdit && not _vimBuffer.IsProcessingInput
           && not _mouseDevice.IsRightButtonPressed then

            // Delay the update to give whoever changed the caret position the
            // opportunity to scroll the view according to their own needs. If
            // another extension moves the caret far offscreen and then
            // centers it if the caret is not onscreen, then reacting too
            // early to the caret position will defeat their offscreen
            // handling. An example is double-clicking on a test in an
            // unopened document in "Test Explorer".
            let doUpdate() =

                // Proceed cautiously because the window might have been
                // closed in the meantime.
                if not _vimBuffer.TextView.IsClosed then _commonOperations.EnsureAtCaret ViewFlags.Standard

            _commonOperations.DoActionAsync doUpdate

[<Export(typeof<IVimBufferCreationListener>)>]
type internal CaretChangeTrackerFactory [<ImportingConstructor>] (_mouseDevice: IMouseDevice, _commonOperationsFactory: ICommonOperationsFactory) =

    interface IVimBufferCreationListener with
        member x.VimBufferCreated vimBuffer =

            // It's OK to just ignore this after creation.  It subscribes to
            // several  event handlers which will keep it alive for the
            // duration of the  IVimBuffer.
            let commonOperations = _commonOperationsFactory.GetCommonOperations vimBuffer.VimBufferData
            let caretChangeTracker = CaretChangeTracker(vimBuffer, commonOperations, _mouseDevice)
            ()
