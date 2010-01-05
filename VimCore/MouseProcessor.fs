#light

namespace Vim
open Vim
open Vim.Modes.Visual
open System.Windows.Input

type internal SelectionData = { 
    Mode : VisualMode;
}

type internal MouseProcessor( _buffer : IVimBuffer ) as this = 
    inherit Microsoft.VisualStudio.Text.Editor.MouseProcessorBase()

    let _selection = _buffer.TextView.Selection

    /// Is the selection currently being updated by the user? 
    let mutable _selectionData : SelectionData option = None
    let mutable _selectionChangedHandler = ToggleHandler.Empty
    let mutable _textViewClosedHandler = ToggleHandler.Empty

    do 
        _selectionChangedHandler <- ToggleHandler.Create _selection.SelectionChanged (fun _ -> this.OnSelectionChanged())
        _selectionChangedHandler.Add()
        _textViewClosedHandler <- ToggleHandler.Create _buffer.TextView.Closed (fun _ -> this.OnTextViewClosed())
        _textViewClosedHandler.Add()

    member private x.IsSelectionChanging = Option.isSome _selectionData

    /// When the view is closed, disconnect all information
    member private x.OnTextViewClosed() =
        _selectionChangedHandler.Remove()
        _textViewClosedHandler.Remove()

    member private x.OnSelectionChanged() =
        if not x.IsSelectionChanging && not _selection.IsEmpty then
            if ModeKind.VisualCharacter <> _buffer.ModeKind then
                _buffer.SwitchMode ModeKind.VisualCharacter |> ignore
            let mode = _buffer.Mode :?> VisualMode
            mode.BeginExplicitMove()
            _selectionData <- Some ( { Mode = mode })
        
    override x.PostprocessMouseLeftButtonUp (e:MouseButtonEventArgs) = 
        match e.ChangedButton = MouseButton.Left, _selectionData with
        | true,Some(data) ->
            data.Mode.EndExplicitMove()
            _selectionData <- None
        | _ -> () 
            
        


