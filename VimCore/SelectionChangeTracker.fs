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
        if _buffer.IsProcessingInput then
            if x.IsAnyVisualMode then 
                // Do nothing.  Selection changes that occur while processing input during
                // visual mode are the responsibility of Visual Mode to handle. 
                _selectionDirty <- false
            else 
                _selectionDirty <- true
        elif not x.IsAnyVisualMode then
            _selectionDirty <- true
            x.SetModeForSelection()

    member private x.OnBufferClosed() = _bag.DisposeAll()
    member private x.OnKeyInputFinished() = 
        if _selectionDirty && not _textView.Selection.IsEmpty && _selectionDirty then x.SetModeForSelection()

    member private x.SetModeForSelection() = 

        // Update the selections.  This is called from a post callback to ensure we don't 
        // interfer with other selection + edit events
        let doUpdate () = 
            try
                if _selectionDirty && not x.IsAnyVisualMode && not _textView.Selection.IsEmpty then
                    let modeKind = 
                        if _textView.Selection.Mode = TextSelectionMode.Stream then ModeKind.VisualCharacter
                        else ModeKind.VisualBlock
                    _buffer.SwitchMode modeKind ModeArgument.None |> ignore
            finally
                _selectionDirty <- false

        let context = System.Threading.SynchronizationContext.Current
        context.Post( (fun _ -> doUpdate()), null)

