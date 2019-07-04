#light

namespace Vim
open System.ComponentModel.Composition
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

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

    let mutable _syncingSelection = false
    let mutable _recordedSelectedSpans: SelectedSpan array = [||]

    do
        if _commonOperations.IsMultiSelectionSupported then

            _textView.Selection.SelectionChanged
            |> Observable.subscribe (fun _ -> this.OnSelectionChanged())
            |> _bag.Add

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
            let oldRecordedSelectedSpans = _recordedSelectedSpans
            _recordedSelectedSpans <- value
            if
                x.HasSecondarySelections _recordedSelectedSpans
                && _recordedSelectedSpans.Length <> oldRecordedSelectedSpans.Length
            then
                x.InitializeSecondaryCaretRegisters()

    /// Raised when the selection changes
    member x.OnSelectionChanged () =
        if
            _syncingSelection
            || _vimBuffer.IsProcessingInput
            || _vimBuffer.IsSwitchingMode
            || _vimBuffer.ModeKind = ModeKind.Disabled
            || _vimBuffer.ModeKind = ModeKind.ExternalEdit
            || _textView.IsClosed
        then

            // Other components are responsible for managing the selection.
            ()

        else

            // An external selection event occurred.
            let (selectedSpans: SelectedSpan array) = x.SelectedSpans

            // Check whether we need to switch to a visual mode.
            if x.HasSecondarySelections selectedSpans then
                x.CheckSwitchToVisual selectedSpans

            // Check whether any secondary selections need to be adjusted for
            // inclusive selection.
            if
                x.HasSecondarySelections selectedSpans
                && _globalSettings.IsSelectionInclusive
                && _textView.Selection.Mode = TextSelectionMode.Stream
            then
                x.CheckAdjustForInclusive selectedSpans

    /// Raised when the selected spans are set
    member x.OnSelectedSpansSet () = 
        x.RecordedSelectedSpans <- x.SelectedSpans

    /// Raised when the buffer starts processing input
    member x.OnKeyInputStart () = 
        x.RecordedSelectedSpans <- x.SelectedSpans

    /// Raised when the buffer finishes processing input
    member x.OnKeyInputEnd () = 
        if
            _vimBuffer.ModeKind = ModeKind.Disabled
            || _vimBuffer.ModeKind = ModeKind.ExternalEdit
            || _textView.IsClosed
        then
            // Don't restore selections when disabled.
            ()
        else
            x.CheckRestoreSelections()

    /// Raised when the vim buffer switches modes
    member x.OnSwitchedMode args =
        match args.ModeArgument with
        | ModeArgument.CancelOperation ->
            x.ClearSecondarySelections()
        | _ ->
            ()

    /// Raised when the vim buffer is closed
    member x.OnBufferClosed() = 
        _bag.DisposeAll()

    /// Get all the selected spans
    member x.SelectedSpans =
        _commonOperations.SelectedSpans |> Seq.toArray

    /// Suppress events while syncing the caret and the selection by performing
    /// the specified action
    member x.SyncSelection action =
        try
            _syncingSelection <- true
            action()
        finally
            _syncingSelection <- false

    // Whether there are any secondary selections
    member x.HasSecondarySelections (selectedSpans: SelectedSpan array) =
        selectedSpans.Length > 1

    // Clear any secondary selections
    member x.ClearSecondarySelections () =
        [_commonOperations.PrimarySelectedSpan]
        |> _commonOperations.SetSelectedSpans

    /// Copy the primary caret's registers to all secondary carets
    member x.InitializeSecondaryCaretRegisters () =
        let selectedSpans = x.SelectedSpans
        let oldCaretIndex = _vimBufferData.CaretIndex
        try
            _vimBufferData.CaretIndex <- 0
            let register = _commonOperations.GetRegister None
            let value = register.RegisterValue
            for caretIndex = 1 to selectedSpans.Length - 1 do
                _vimBufferData.CaretIndex <- caretIndex
                register.RegisterValue <- value
        finally
            _vimBufferData.CaretIndex <- oldCaretIndex

    /// Get the snapshot line and spaces for the specified point
    member x.GetLineAndSpaces point =
        let tabStop = _localSettings.TabStop
        let line, columnNumber = VirtualSnapshotPointUtil.GetLineAndOffset point
        let spaces = VirtualSnapshotColumn.GetSpacesToColumnNumber(line, columnNumber, tabStop)
        line, spaces

    /// Adjust the specified selected span for inclusive selection
    member x.AdjustForInclusive (selectedSpan: SelectedSpan) =
        if
            not selectedSpan.IsReversed
            && selectedSpan.Length > 0
            && selectedSpan.CaretPoint = selectedSpan.End
        then
            let caretPoint =
                selectedSpan.CaretPoint
                |> VirtualSnapshotPointUtil.SubtractOneOrCurrent
            let anchorPoint = selectedSpan.AnchorPoint
            let activePoint = selectedSpan.ActivePoint
            SelectedSpan(caretPoint, anchorPoint, activePoint)
            |> Some
        else
            None

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
    member x.CheckRestoreSelections () =
        let oldSelectedSpans = x.RecordedSelectedSpans
        if x.HasSecondarySelections oldSelectedSpans then
            let newSelectedSpans = x.SelectedSpans

            // We previously had secondary selected spans.
            if
                newSelectedSpans.[0] = oldSelectedSpans.[0]
                && newSelectedSpans.Length = oldSelectedSpans.Length
            then

                // The primary selection didn't change and the number of
                // selected spans didn't change.
                ()

            else
                fun () ->
                    x.RestoreSelectedSpans oldSelectedSpans newSelectedSpans
                |> x.SyncSelection

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

    /// Check whether we need to switch to a visual mode
    member x.CheckSwitchToVisual selectedSpans =
        if selectedSpans.[0].Length > 0 then
            match _vimBuffer.ModeKind with
            | ModeKind.VisualCharacter -> ()
            | ModeKind.VisualLine -> ()
            | ModeKind.VisualBlock -> ()
            | ModeKind.SelectCharacter -> ()
            | ModeKind.SelectLine -> ()
            | ModeKind.SelectBlock -> ()
            | _ ->
                let isSelectModeMouse =
                    SelectModeOptions.Mouse 
                    |> Util.IsFlagSet _globalSettings.SelectModeOptions
                let modeKind =
                    if isSelectModeMouse then
                        ModeKind.SelectCharacter
                    else
                        ModeKind.VisualCharacter
                _vimBuffer.SwitchMode modeKind ModeArgument.None
                |> ignore

    /// Check whether we need to adjust any selections for inclusive
    member x.CheckAdjustForInclusive selectedSpans =
        let results =
            seq {
                for caretIndex = 0 to selectedSpans.Length - 1 do
                    let selectedSpan = selectedSpans.[caretIndex]
                    match x.AdjustForInclusive selectedSpan with
                    | Some adjustedSelectedSpan ->
                        yield true, adjustedSelectedSpan
                    | None ->
                        yield false, selectedSpan
            }
            |> Seq.toList
        if results |> Seq.exists (fun (changed, _) -> changed) then
            fun () ->
                results
                |> Seq.map (fun (_, selectedSpan) -> selectedSpan)
                |> _commonOperations.SetSelectedSpans
            |> x.SyncSelection

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
