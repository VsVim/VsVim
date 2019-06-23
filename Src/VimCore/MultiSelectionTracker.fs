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
    let _localSettings = _vimBuffer.LocalSettings
    let _vimBufferData = _vimBuffer.VimBufferData
    let _vimTextBuffer = _vimBuffer.VimTextBuffer 
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

        _vimBuffer.KeyInputEnd
        |> Observable.subscribe (fun _ -> this.OnKeyInputEnd())
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
            if value.Length > _recordedSelectedSpans.Length then
                x.InitializeSecondaryCaretRegisters value
            _recordedSelectedSpans <- value

    /// Raised when the selected spans are set
    member x.OnSelectedSpansSet () = 
        x.RecordedSelectedSpans <- x.SelectedSpans

    /// Raised when the buffer starts processing input
    member x.OnKeyInputStart () = 
        x.RecordedSelectedSpans <- x.SelectedSpans

    /// Raised when the buffer finishes processing input
    member x.OnKeyInputEnd args = 
        if
            _vimBuffer.ModeKind <> ModeKind.Disabled
            && _vimBuffer.ModeKind <> ModeKind.ExternalEdit
            && not _textView.IsClosed
        then
            x.CheckRestoreSelectedSpans()

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

    /// Get all the selected spans
    member x.SelectedSpans =
        _commonOperations.SelectedSpans |> Seq.toArray

    /// Copy the primary caret's registers to all secondary carets
    member x.InitializeSecondaryCaretRegisters selectedSpans =
        let oldCaretIndex = _vimData.CaretIndex
        let register = _commonOperations.GetRegister None
        try
            _vimData.CaretIndex <- 0
            let value = register.RegisterValue
            for caretIndex = 1 to selectedSpans.Length - 1 do
                _vimData.CaretIndex <- caretIndex
                register.RegisterValue <- value
        finally
            _vimData.CaretIndex <- oldCaretIndex

    /// Get the snapshot line and spaces for the specified point
    member x.GetLineAndSpaces point =
        let tabStop = _localSettings.TabStop
        let line, columnNumber = VirtualSnapshotPointUtil.GetLineAndOffset point
        let spaces = VirtualSnapshotColumn.GetSpacesToColumnNumber(line, columnNumber, tabStop)
        line, spaces

    /// Adjust the specified selected span to new relative line and spaces offsets
    member x.AdjustSelectedSpan lineOffset spacesOffset length (span: SelectedSpan) =

        // Adjust the specified point by the line offset and spaces offset.
        let adjustPoint (point: VirtualSnapshotPoint) =
            let snapshot = point.Position.Snapshot
            let tabStop = _localSettings.TabStop
            let oldLine, oldSpaces = x.GetLineAndSpaces point
            let newLineNumber = oldLine.LineNumber + lineOffset
            let newLine = SnapshotUtil.GetLineOrLast snapshot newLineNumber
            let newSpaces = oldSpaces + spacesOffset
            if _vimTextBuffer.UseVirtualSpace then
                VirtualSnapshotColumn.GetColumnForSpaces(newLine, newSpaces, tabStop)
                |> (fun column -> column.VirtualStartPoint)
            else
                SnapshotColumn.GetColumnForSpacesOrEnd(newLine, newSpaces, tabStop)
                |> (fun column -> column.StartPoint)
                |> VirtualSnapshotPointUtil.OfPoint

        let newCaretPoint = adjustPoint span.CaretPoint
        if length = 0 then
            SelectedSpan(newCaretPoint)
        else
            let newAnchorPoint = adjustPoint span.AnchorPoint
            let newActivePoint = adjustPoint span.ActivePoint
            SelectedSpan(newCaretPoint, newAnchorPoint, newActivePoint)

    /// Check whether we need to restore selected spans and if so, restore them
    member x.CheckRestoreSelectedSpans () =
        let oldSelectedSpans = x.RecordedSelectedSpans
        if oldSelectedSpans.Length > 1 then

            // We previously had secondary selected spans.
            let newSelectedSpans = x.SelectedSpans
            if
                newSelectedSpans.[0] = oldSelectedSpans.[0]
                && newSelectedSpans.Length = oldSelectedSpans.Length
            then

                // The caret didn't move and the number of selected spans
                // didn't change.
                ()

            else
                x.RestoreSelectedSpans oldSelectedSpans newSelectedSpans

    /// Restore selected spans present at the start of key processing
    member x.RestoreSelectedSpans oldSelectedSpans newSelectedSpans =

        let oldPrimarySelectedSpan =
            oldSelectedSpans.[0]
            |> _commonOperations.MapSelectedSpanToCurrentSnapshot
        let newPrimarySelectedSpan = newSelectedSpans.[0]
        seq {

            // Return the first selected span as is.
            yield newSelectedSpans.[0]

            if newPrimarySelectedSpan = oldPrimarySelectedSpan then

                // The primary selection hasn't changed but the secondary
                // selections were cleared. Restore them.
                for caretIndex = 1 to oldSelectedSpans.Length - 1 do
                    let newSecondarySelectedSpan =
                        oldSelectedSpans.[caretIndex]
                        |> _commonOperations.MapSelectedSpanToCurrentSnapshot
                    yield newSecondarySelectedSpan

            else

                // The primary selection has changed and we previously had
                // multiple selected spans that were not cleared explicitly.
                let oldCaretPoint = oldPrimarySelectedSpan.CaretPoint
                let newCaretPoint = newPrimarySelectedSpan.CaretPoint
                let oldLine, oldSpaces = x.GetLineAndSpaces oldCaretPoint
                let newLine, newSpaces =x.GetLineAndSpaces newCaretPoint
                let lineOffset = newLine.LineNumber - oldLine.LineNumber
                let spacesOffset = newSpaces - oldSpaces
                let length = newPrimarySelectedSpan.Length

                // Shift the old secondary spans by the change in position of
                // the primary caret.
                for caretIndex = 1 to oldSelectedSpans.Length - 1 do
                    let newSelectedSpan =
                        oldSelectedSpans.[caretIndex]
                        |> _commonOperations.MapSelectedSpanToCurrentSnapshot
                        |> x.AdjustSelectedSpan lineOffset spacesOffset length
                    yield newSelectedSpan

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
