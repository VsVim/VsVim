#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open Microsoft.VisualStudio.Text.Tagging
open Microsoft.VisualStudio.Text.Classification
open Microsoft.VisualStudio.Utilities
open System.ComponentModel.Composition
open System.Collections.Generic

/// This type is responsible for monitoring selection events.  If at the end of 
/// of a selection event and the corresponding key event we still have a selection
/// then we need to enter the appropriate Visual Mode if we're not already 
/// inside on
type internal SelectionChangeTracker
    ( 
        _vimBuffer: IVimBuffer,
        _commonOperations: ICommonOperations,
        _selectionOverrideList: IVisualModeSelectionOverride list,
        _mouseDevice: IMouseDevice
    ) as this =

    let _globalSettings = _vimBuffer.GlobalSettings
    let _textView = _vimBuffer.TextView
    let _vimHost = _vimBuffer.Vim.VimHost
    let _bag = DisposableBag()

    let mutable _syncingSelection = false

    /// Did the selection change while we were in the middle of processing 
    /// key input and not in Visual Mode 
    let mutable _selectionDirty = false

    do
        _textView.Selection.SelectionChanged 
        |> Observable.subscribe (fun _ -> this.OnSelectionChanged())
        |> _bag.Add

        _vimBuffer.Closed
        |> Observable.subscribe (fun _ -> this.OnBufferClosed())
        |> _bag.Add

        _vimBuffer.KeyInputProcessed
        |> Observable.subscribe (fun _ -> this.OnKeyInputFinished())
        |> _bag.Add

    member x.HasSecondarySelections =
        Seq.length _commonOperations.SelectedSpans > 1

    member x.ShouldIgnoreSelectionChange() = 
        _selectionOverrideList
        |> Seq.exists (fun x -> x.IsInsertModePreferred _textView)

    /// Raised when the selection changes.  
    member x.OnSelectionChanged() = 
        if _syncingSelection then
            // Ignore selection changes when we are explicitly updating it
            ()
        else if not (_vimHost.IsFocused _textView) then
            // It's possible that an edit in another ITextView has affected the selection on this 
            // ITextView.  If it deleted text for example it would cause a selection event in 
            // every ITextView which had a caret in the area.  Only the active one should be the
            // one responding to it.
            ()
        elif _vimBuffer.ModeKind = ModeKind.Insert && x.ShouldIgnoreSelectionChange() then
            // If one of the IVisualModeSelectionOverride instances wants us to ignore the
            // event then we will
            ()
        elif _vimBuffer.ModeKind = ModeKind.Disabled || _vimBuffer.ModeKind = ModeKind.ExternalEdit then
            // If the selection changes while Vim is disabled then don't update
            () 
        elif x.HasSecondarySelections then
            // If there are secondary selections, the multi-selection
            // support is responsible for setting the correct mode.
            _selectionDirty <- false
        elif _vimBuffer.IsProcessingInput then
            if VisualKind.IsAnyVisualOrSelect _vimBuffer.ModeKind then
                // Do nothing.  Selection changes that occur while processing input during
                // visual or select mode are the responsibility of the mode to handle
                _selectionDirty <- false
            else 
                _selectionDirty <- true
        elif _vimBuffer.IsSwitchingMode then
            // Selection is frequently updated while switching between modes.  It's the responsibility
            // of the mode switching logic to properly update the mode here. 
            ()
        else
            // Adjust the caret for the external selection.
            if
                x.NeedInclusiveAdjustmentForSelection
                && not (TextViewUtil.IsSelectionEmpty _textView)
                && _textView.Caret.Position.VirtualBufferPosition = _textView.Selection.End
            then
                x.SyncSelection (fun () -> x.AdjustCaretToSelection())

            // Set the appropriate mode for the external selection.
            x.SetModeForSelection()

    member x.OnBufferClosed() = 
        _bag.DisposeAll()

    /// Linked to the KeyInputProcessed event.  If the selection changed while processing keyinput
    /// and we weren't in Visual Mode then we need to update the selection
    member x.OnKeyInputFinished() = 
        if _selectionDirty then
            _selectionDirty <- false
            x.SetModeForSelection()

    /// Whether an externally applied selection needs an inclusive adjustment
    member x.NeedInclusiveAdjustmentForSelection =
        _globalSettings.SelectionKind = SelectionKind.Inclusive
        && _textView.Selection.Mode = TextSelectionMode.Stream
        && not _textView.Selection.IsReversed

    /// Update the mode based on the current Selection
    member x.SetModeForSelection() = 

        let isLeftButtonPressed = _mouseDevice.IsLeftButtonPressed

        // Do we want to change the mode of IVimBuffer based on the active selection?
        let getDesiredNewMode () = 
            let isSelectModeMouse =
                Util.IsFlagSet _globalSettings.SelectModeOptions SelectModeOptions.Mouse 
            let desiredNewMode = 
                if x.HasSecondarySelections then
                    // If there are secondary selections, it is the responsibility of
                    // the multi-selection tracker to set the mode.
                    None
                elif TextViewUtil.IsSelectionEmpty _textView then
                    if VisualKind.IsAnyVisualOrSelect _vimBuffer.ModeKind then
                        Some ModeKind.Normal
                    else 
                        None
                elif isSelectModeMouse && isLeftButtonPressed then
                    // When the "mouse" is set in 'selectmode' then selection change should ensure
                    // we are in select.  If we are already in select then maintain the current mode
                    // else transition into the standard character one
                    if VisualKind.IsAnySelect _vimBuffer.ModeKind then
                        Some _vimBuffer.ModeKind
                    else
                        Some ModeKind.SelectCharacter
                elif _textView.Selection.Mode = TextSelectionMode.Stream then 
                    let modeKind = 
                        match _vimBuffer.ModeKind with
                        | ModeKind.VisualCharacter -> ModeKind.VisualCharacter
                        | ModeKind.VisualLine -> ModeKind.VisualLine
                        | ModeKind.VisualBlock -> ModeKind.VisualCharacter
                        | ModeKind.SelectCharacter -> ModeKind.SelectCharacter
                        | ModeKind.SelectLine -> ModeKind.SelectLine
                        | ModeKind.SelectBlock -> ModeKind.SelectCharacter
                        | _ ->
                            // We were not already in a visual mode and the
                            // user did not initiate the selection with the
                            // mouse.  In that case handle the external select
                            // by using the 'selectmode=mouse' setting
                            if isSelectModeMouse then
                                ModeKind.SelectCharacter
                            else
                                ModeKind.VisualCharacter
                    Some modeKind
                else 
                    // Handle TextSelectionMode.Box cases
                    if _vimBuffer.ModeKind = ModeKind.SelectBlock then
                        Some ModeKind.SelectBlock
                    elif _vimBuffer.ModeKind = ModeKind.VisualBlock then
                        Some ModeKind.VisualBlock
                    elif isSelectModeMouse then
                        Some ModeKind.SelectBlock
                    else
                        Some ModeKind.VisualBlock
            match desiredNewMode with 
            | None -> None
            | Some kind -> if kind <> _vimBuffer.ModeKind then Some kind else None 

        // Update the selections.  This is called from a post callback to ensure we don't 
        // interfer with other selection + edit events.
        //
        // Because this occurs at a  later time it is possible that the IVimBuffer was closed
        // in the mean time.  Make sure to guard against this possibility
        let doUpdate () = 
            if _vimBuffer.ModeKind = ModeKind.Disabled || _vimBuffer.ModeKind = ModeKind.ExternalEdit then
                // If vim was disabled between the posting of this event and the callback then do
                // nothing
                ()
            elif x.HasSecondarySelections then
                // If there are secondary selections, it is the responsibility
                // of the multi-selection tracker to set the mode.
                ()
            elif not _vimBuffer.IsClosed && not _selectionDirty then
                match getDesiredNewMode() with
                | None -> ()
                | Some modeKind -> 
                    // Switching from an insert mode to a visual/select mode automatically initiates one command mode
                    if VisualKind.IsAnyVisualOrSelect modeKind && VimExtensions.IsAnyInsert _vimBuffer.ModeKind then
                        _vimBuffer.VimTextBuffer.InOneTimeCommand <- Some _vimBuffer.ModeKind
                    _vimBuffer.SwitchMode modeKind ModeArgument.None |> ignore

        match getDesiredNewMode() with
        | None ->
            fun () ->
                x.AdjustSelectionToCaret()

                // No mode change is desired.  However the selection has changed and Visual Mode 
                // caches information about the original selection.  Update that information now
                if VisualKind.IsAnyVisual _vimBuffer.ModeKind then
                    let mode = _vimBuffer.Mode :?> IVisualMode
                    mode.SyncSelection()
                elif VisualKind.IsAnySelect _vimBuffer.ModeKind then
                    let mode = _vimBuffer.Mode :?> ISelectMode
                    mode.SyncSelection()

            |> x.SyncSelection
        | Some _ -> 
            _commonOperations.DoActionAsync doUpdate

    /// Suppress events while syncing the caret and the selection by performing
    /// the specified action
    member x.SyncSelection action =
        try
            _syncingSelection <- true
            action()
        finally
            _syncingSelection <- false

    /// In a normal character style selection vim extends the selection to include the value
    /// under the caret.  The editor by default does an exclusive selection.  Adjust the selection
    /// here to be inclusive 
    member x.AdjustSelectionToCaret() =
        Contract.Assert _syncingSelection

        if
            _mouseDevice.InDragOperation(_textView)
            && _textView.Selection.IsActive
            && x.NeedInclusiveAdjustmentForSelection
            && (_vimBuffer.ModeKind = ModeKind.VisualCharacter || _vimBuffer.ModeKind = ModeKind.SelectCharacter)
        then
            match _commonOperations.MousePoint with
            | Some mousePoint ->
                let activePoint =
                    mousePoint
                    |> VirtualSnapshotPointUtil.AddOneOnSameLine
                let anchorPoint = _textView.Selection.AnchorPoint
                SelectedSpan(mousePoint, anchorPoint, activePoint)
                |> TextViewUtil.SelectSpan _textView
            | _ -> ()

    /// When an non-empty external selection occurs with an inclusive selection,
    /// move the caret back one position.
    member x.AdjustCaretToSelection() =
        Contract.Assert _syncingSelection

        _textView.Caret.Position.BufferPosition
        |> SnapshotPointUtil.SubtractOneOrCurrent
        |> VirtualSnapshotPointUtil.OfPoint
        |> (fun point ->
            TextViewUtil.MoveCaretToVirtualPointRaw _textView point MoveCaretFlags.None)

[<Export(typeof<IVimBufferCreationListener>)>]
type internal SelectionChangeTrackerFactory
    [<ImportingConstructor>]
    (
        [<ImportMany>] _selectionOverrideList: IVisualModeSelectionOverride seq,
        _mouseDevice: IMouseDevice,
        _commonOperationsFactory: ICommonOperationsFactory
    ) =

    let _selectionOverrideList = _selectionOverrideList |> List.ofSeq

    interface IVimBufferCreationListener with
        member x.VimBufferCreated vimBuffer = 

            // It's OK to just ignore this after creation.  It subscribes to several 
            // event handlers which will keep it alive for the duration of the 
            // IVimBuffer
            let commonOperations = _commonOperationsFactory.GetCommonOperations vimBuffer.VimBufferData
            let selectionTracker = SelectionChangeTracker(vimBuffer, commonOperations, _selectionOverrideList, _mouseDevice)
            ()
