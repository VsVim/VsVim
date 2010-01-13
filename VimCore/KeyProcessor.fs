#light

namespace Vim
open Vim
open System.Windows.Input

type internal KeyProcessor( _buffer : IVimBuffer ) = 
    inherit Microsoft.VisualStudio.Text.Editor.KeyProcessor()

    /// When the user is typing we get events for every single key press.  This means that 
    /// typing something like an upper case character will cause at least 2 events to be
    /// generated.  
    ///  1) LeftShift 
    ///  2) LeftShift + b
    /// This helps us filter out items like #1 which we don't want to process
    member private x.IsNonInputKey k =
        match k with
        | Key.LeftAlt -> true
        | Key.LeftCtrl -> true
        | Key.LeftShift -> true
        | Key.RightAlt -> true
        | Key.RightCtrl -> true
        | Key.RightShift -> true
        | _ -> false

    member private x.IsInputKey k = not (x.IsNonInputKey k)

    member private x.TryHandleTextInput (args: TextCompositionEventArgs ) = 
        if 1 <> args.Text.Length then
            false
        else
        
            // Only want to intercept text coming from the keyboard.  Let other 
            // components edit without having to come through us
            match args.Device with
            | :? KeyboardDevice as keyboard -> 
                match InputUtil.TryCharToKeyInput(args.Text.Chars(0)) with
                | Some(ki) ->
                    let ki = KeyInput(ki.Char,ki.Key,keyboard.Modifiers)
                    _buffer.CanProcessInput(ki) && _buffer.ProcessInput(ki)
                | None -> false
            | _ -> false

    override x.TextInput(args:TextCompositionEventArgs) =
        if x.TryHandleTextInput args then
            args.Handled <- true
        else
            base.TextInput(args)

    override x.KeyDown(args:KeyEventArgs) =
        let handled = 
            if x.IsInputKey(args.Key) then
                let ki = InputUtil.KeyAndModifierToKeyInput (args.Key) (args.KeyboardDevice.Modifiers)
                _buffer.CanProcessInput ki && _buffer.ProcessInput(ki)
            else
                false
        if handled then 
            args.Handled <- true
        else
            base.KeyDown(args)

    override x.IsInterestedInHandledEvents = true


