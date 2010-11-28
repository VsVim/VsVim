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
        _buffer : IVimBuffer ) as this =

    let _textView = _buffer.TextView
    let _bag = DisposableBag()

    /// Did the selection change while we were in the middle of processing 
    /// key input and not in Visual Mode 
    let mutable _selectionDirty = false

    do
        _textView.Selection.SelectionChanged 
        |> Observable.subscribe (fun args -> this.OnSelectionChanged() )
        |> _bag.Add

        _buffer.Closed
        |> Observable.subscribe (fun args -> this.OnBufferClosed() )
        |> _bag.Add

        _buffer.KeyInputProcessed
        |> Observable.subscribe (fun args -> this.OnKeyInputFinished() )
        |> _bag.Add

        _buffer.KeyInputBuffered
        |> Observable.subscribe (fun args -> this.OnKeyInputFinished() )
        |> _bag.Add

    member private x.IsAnyVisualMode = _buffer.ModeKind |> VisualKind.ofModeKind |> Option.isSome

    /// Raised when the selection changes.  
    member private x.OnSelectionChanged() = 
        if _buffer.ModeKind = ModeKind.Disabled then
            // If the selection changes while Vim is disabled then don't update
            () 
        elif _buffer.IsProcessingInput then
            if x.IsAnyVisualMode then 
                // Do nothing.  Selection changes that occur while processing input during
                // visual mode are the responsibility of Visual Mode to handle. 
                _selectionDirty <- false
            else 
                _selectionDirty <- true
        else
            x.SetModeForSelection()

    member private x.OnBufferClosed() = _bag.DisposeAll()

    /// Linked to the KeyInputProcessed event.  If the selection changed while processing keyinput
    /// and we weren't in Visual Mode then we need to update the selection
    member private x.OnKeyInputFinished() = 
        if _selectionDirty then
            _selectionDirty <- false
            x.SetModeForSelection()

    /// Update the mode based on the current Selection
    member private x.SetModeForSelection() = 

        // What should the mode be based on the current selection
        let desiredMode () = 
            let inner = 
                if _textView.Selection.IsEmpty then 
                    if x.IsAnyVisualMode then Some ModeKind.Normal
                    else None
                elif _textView.Selection.Mode = TextSelectionMode.Stream then 
                    if _buffer.ModeKind = ModeKind.VisualLine then Some ModeKind.VisualLine
                    else Some ModeKind.VisualCharacter 
                else Some ModeKind.VisualBlock
            match inner with 
            | None -> None
            | Some(kind) -> if kind <> _buffer.ModeKind then Some kind else None 

        // Update the selections.  This is called from a post callback to ensure we don't 
        // interfer with other selection + edit events
        let doUpdate () = 
            if not  _selectionDirty then 
                match desiredMode() with
                | None -> ()
                | Some(modeKind) -> _buffer.SwitchMode modeKind ModeArgument.None |> ignore

        match desiredMode() with
        | None ->
            // No mode change is desired.  However the selection has changed and Visual Mode 
            // caches information about the original selection.  Update that information now
            if x.IsAnyVisualMode then
                let mode = _buffer.Mode :?> IVisualMode
                mode.SyncSelection()
        | Some(_) -> 
            // It's not guaranteed that this will be set.  Visual Studio for instance will
            // null this out in certain WPF designer scenarios
            let context = System.Threading.SynchronizationContext.Current
            if context <> null then context.Post( (fun _ -> doUpdate()), null)
            else doUpdate()

