﻿#light

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
        _vimBuffer : IVimBuffer,
        _commonOperations : ICommonOperations,
        _selectionOverrideList : IVisualModeSelectionOverride list,
        _mouseDevice : IMouseDevice
    ) as this =

    let _globalSettings = _vimBuffer.GlobalSettings
    let _textView = _vimBuffer.TextView
    let _bag = DisposableBag()

    let mutable _syncingSelection = false

    /// Did the selection change while we were in the middle of processing 
    /// key input and not in Visual Mode 
    let mutable _selectionDirty = false

    do
        _textView.Selection.SelectionChanged 
        |> Observable.subscribe (fun _ -> this.OnSelectionChanged())
        |> _bag.Add

        _textView.Caret.PositionChanged 
        |> Observable.subscribe (fun _ -> this.OnPositionChanged())
        |> _bag.Add

        _vimBuffer.Closed
        |> Observable.subscribe (fun _ -> this.OnBufferClosed())
        |> _bag.Add

        _vimBuffer.KeyInputProcessed
        |> Observable.subscribe (fun _ -> this.OnKeyInputFinished())
        |> _bag.Add

    member x.ShouldIgnoreSelectionChange() = 
        _selectionOverrideList
        |> Seq.exists (fun x -> x.IsInsertModePreferred _textView)

    /// Raised when the selection changes.  
    member x.OnSelectionChanged() = 
        if _syncingSelection then
            // Ignore selection changes when we are explicitly updating it
            ()
        elif _vimBuffer.ModeKind = ModeKind.Insert && x.ShouldIgnoreSelectionChange() then
            // If one of the IVisualModeSelectionOverride instances wants us to ignore the
            // event then we will
            ()
        elif _vimBuffer.ModeKind = ModeKind.Disabled || _vimBuffer.ModeKind = ModeKind.ExternalEdit then
            // If the selection changes while Vim is disabled then don't update
            () 
        elif _vimBuffer.IsProcessingInput then
            if VisualKind.IsAnyVisualOrSelect _vimBuffer.ModeKind then
                // Do nothing.  Selection changes that occur while processing input during
                // visual or select mode are the responsibility of the mode to handle
                _selectionDirty <- false
            else 
                _selectionDirty <- true
        else
            x.SetModeForSelection()

    /// If the caret changes position and it wasn't initiated by VsVim then we should be 
    /// adjusting the screen to account for 'scrolloff'
    member x.OnPositionChanged() = 
        if not _vimBuffer.IsProcessingInput then
            _commonOperations.EnsureAtCaret ViewFlags.ScrollOffset

    member x.OnBufferClosed() = 
        _bag.DisposeAll()

    /// Linked to the KeyInputProcessed event.  If the selection changed while processing keyinput
    /// and we weren't in Visual Mode then we need to update the selection
    member x.OnKeyInputFinished() = 
        if _selectionDirty then
            _selectionDirty <- false
            x.SetModeForSelection()

    /// Update the mode based on the current Selection
    member x.SetModeForSelection() = 

        let isLeftButtonPressed = _mouseDevice.IsLeftButtonPressed

        // What should the mode be based on the current selection
        let desiredMode () = 
            let inner = 
                if _textView.Selection.IsEmpty then 
                    if VisualKind.IsAnyVisualOrSelect _vimBuffer.ModeKind then
                        Some ModeKind.Normal
                    else 
                        None
                elif Util.IsFlagSet _globalSettings.SelectModeOptions SelectModeOptions.Mouse && isLeftButtonPressed then
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
                        | ModeKind.VisualLine -> ModeKind.VisualLine
                        | ModeKind.SelectCharacter -> ModeKind.SelectCharacter
                        | ModeKind.SelectLine -> ModeKind.SelectLine
                        | ModeKind.SelectBlock -> ModeKind.SelectBlock
                        | _ -> ModeKind.VisualCharacter
                    Some modeKind
                else 
                    if _vimBuffer.ModeKind = ModeKind.SelectBlock then
                        Some ModeKind.SelectBlock
                    else
                        Some ModeKind.VisualBlock
            match inner with 
            | None -> None
            | Some kind -> if kind <> _vimBuffer.ModeKind then Some kind else None 

        // Update the selections.  This is called from a post callback to ensure we don't 
        // interfer with other selection + edit events.
        //
        // Because this occurs at a  later time it is possible that the IVimBuffer was closed
        // in the mean time.  Make sure to guard against this possibility
        let doUpdate () = 
            if not _vimBuffer.IsClosed && not _selectionDirty then 
                match desiredMode() with
                | None -> ()
                | Some modeKind -> _vimBuffer.SwitchMode modeKind ModeArgument.None |> ignore

        match desiredMode() with
        | None ->

            // No mode change is desired.  However the selection has changed and Visual Mode 
            // caches information about the original selection.  Update that information now
            let doSync () = 
                if VisualKind.IsAnyVisual _vimBuffer.ModeKind then
                    let mode = _vimBuffer.Mode :?> IVisualMode
                    mode.SyncSelection()
                elif VisualKind.IsAnySelect _vimBuffer.ModeKind then
                    let mode = _vimBuffer.Mode :?> ISelectMode
                    mode.SyncSelection()

            try
                _syncingSelection <- true
                doSync()
            finally
                _syncingSelection <- false
        | Some _ -> 
            // It's not guaranteed that this will be set.  Visual Studio for instance will
            // null this out in certain WPF designer scenarios
            let context = System.Threading.SynchronizationContext.Current
            if context <> null then context.Post( (fun _ -> doUpdate()), null)
            else doUpdate()

[<Export(typeof<IVimBufferCreationListener>)>]
type internal SelectionChangeTrackerFactory
    [<ImportingConstructor>]
    (
        [<ImportMany>] _selectionOverrideList : IVisualModeSelectionOverride seq,
        _mouseDevice : IMouseDevice,
        _commonOperationsFactory : ICommonOperationsFactory
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


