#light

namespace Vim.Utils
open Vim
open System.Windows.Input

type KeyProcessor( _buffer : IVimBuffer ) = 
    inherit Microsoft.VisualStudio.Text.Editor.KeyProcessor()

    /// I'm not entirely sure what the purpose of this function is anymore.  It was directly
    /// ported from the C# version of the function and hence I gave it the same semantics. 
    /// TODO: Investigate the purpose here?  To prevent beeping?
    member private x.IsNonInputKey k =
        match k with
        | Key.LeftAlt -> true
        | Key.LeftCtrl -> true
        | Key.LeftShift -> true
        | Key.RightAlt -> true
        | Key.RightCtrl -> true
        | Key.RightShift -> true
        | _ -> false

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
                    _buffer.CanProcessInput(ki)
                | None -> false
            | _ -> false

    override x.TextInput(args:TextCompositionEventArgs) =
        if x.TryHandleTextInput args then
            // TODO: I think this is a bug.  Probably should be ProcessInput instead of true 
            args.Handled <- true
        else
            base.TextInput(args)

    override x.KeyDown(args:KeyEventArgs) =
        let handled = 
            if x.IsNonInputKey(args.Key) then
                let ki = InputUtil.KeyAndModifierToKeyInput (args.Key) (args.KeyboardDevice.Modifiers)
                _buffer.CanProcessInput ki
            else
                false
        if handled then 
            args.Handled <- true
        else
            base.KeyDown(args)

