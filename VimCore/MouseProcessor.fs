#light

namespace Vim
open Vim
open Vim.Modes.Visual
open System.Windows.Input
open System.Windows.Threading

/// Define an interface for System.Windows.Input.MouseDevice which allows me
/// to better test MouseProcessor
type public IMouseDevice =
    abstract LeftButtonState : MouseButtonState

/// Standard implemantation of IMouseDevice
type internal MouseDeviceImpl() =
    let _mouseDevice = InputManager.Current.PrimaryMouseDevice
    interface IMouseDevice with
        member x.LeftButtonState = _mouseDevice.LeftButton

/// The purpose of this component is to manage the transition between the start of a 
/// selection to the completion.  Visual Mode must be started at the start of the selection
/// but not completely enabled until the selection has completed
type internal MouseProcessor
    ( 
        _buffer : IVimBuffer,  
        _mouseDevice : IMouseDevice ) as this =
    inherit Microsoft.VisualStudio.Text.Editor.MouseProcessorBase()

    let _selection = _buffer.TextView.Selection

    /// Is the selection currently being updated by the user? 
    let mutable _isSelectionChanging = false
    let mutable _selectionChangedHandler = ToggleHandler.Empty
    let mutable _textViewClosedHandler = ToggleHandler.Empty

    do 
        _selectionChangedHandler <- ToggleHandler.Create _selection.SelectionChanged (fun _ -> this.OnSelectionChanged())
        _selectionChangedHandler.Add()
        _textViewClosedHandler <- ToggleHandler.Create _buffer.TextView.Closed (fun _ -> this.OnTextViewClosed())
        _textViewClosedHandler.Add()

    /// Is the selection currently changing via a mouse operation
    member x.IsSelectionChanging 
        with get() =  _isSelectionChanging
        and set value = _isSelectionChanging <- value

    member private x.InAnyVisualMode =
        match _buffer.ModeKind with
        | ModeKind.VisualBlock -> true
        | ModeKind.VisualCharacter -> true
        | ModeKind.VisualLine -> true
        | _ -> false

    /// When the view is closed, disconnect all information
    member private x.OnTextViewClosed() =
        _selectionChangedHandler.Remove()
        _textViewClosedHandler.Remove()

    member private x.OnSelectionChanged() =
        if not x.IsSelectionChanging && not _selection.IsEmpty && not x.InAnyVisualMode then
            let mode = _buffer.SwitchMode ModeKind.VisualCharacter 
            let mode = mode :?> IVisualMode

            // If the left mouse button is pressed then we are in the middle of 
            // a mouse selection event and need to record the data
            if _mouseDevice.LeftButtonState = MouseButtonState.Pressed then
                _isSelectionChanging <- true

    override x.PostprocessMouseLeftButtonUp (e:MouseButtonEventArgs) = 
        if e.ChangedButton = MouseButton.Left then

            if x.IsSelectionChanging then
                // Just completed a selection event.  Nothing to worry about
                _isSelectionChanging <- false
            else if x.InAnyVisualMode then
                // Mouse was clicked and we are in visual mode.  Switch out to the previous
                // mode.  Do this at background so it doesn't interfer with other processing
                let func() =  _buffer.SwitchMode ModeKind.Normal |> ignore
                Dispatcher.CurrentDispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new System.Action(func)) |> ignore
    

