using System;
using System.Windows.Input;
using Microsoft.VisualStudio.Text;

namespace Vim.UI.Wpf
{
    /// <summary>
    /// The morale of the history surrounding this type is translating key input is
    /// **hard**.  Anytime it's done manually and expected to be 100% correct it 
    /// likely to have a bug.  If you doubt this then I encourage you to read the 
    /// following 10 part blog series
    /// 
    /// http://blogs.msdn.com/b/michkap/archive/2006/04/13/575500.aspx
    ///
    /// Or simply read the keyboard feed on the same blog page.  It will humble you
    /// </summary>
    public class KeyProcessor : Microsoft.VisualStudio.Text.Editor.KeyProcessor
    {
        private readonly IVimBuffer _buffer;

        public IVimBuffer VimBuffer
        {
            get { return _buffer; }
        }

        public ITextBuffer TextBuffer
        {
            get { return _buffer.TextBuffer; }
        }

        public KeyProcessor(IVimBuffer buffer)
        {
            _buffer = buffer;
        }

        public override bool IsInterestedInHandledEvents
        {
            get { return true; }
        }

        /// <summary>
        /// Last chance at custom handling of user input.  At this point we have the 
        /// advantage that WPF has properly converted the user input into a char which 
        /// can be effeciently mapped to a KeyInput value.  
        /// </summary>
        public override void TextInput(TextCompositionEventArgs args)
        {
            bool handled = false;
            if (!String.IsNullOrEmpty(args.Text) && 1 == args.Text.Length)
            {
                // Only want to intercept text coming from the keyboard.  Let other 
                // components edit without having to come through us
                var keyboard = args.Device as KeyboardDevice;
                if (keyboard != null)
                {
                    var ki = KeyUtil.CharAndModifiersToKeyInput(args.Text[0], keyboard.Modifiers);
                    handled = _buffer.CanProcess(ki) && _buffer.Process(ki);
                }
            }

            if (handled)
            {
                args.Handled = true;
            }
            else
            {
                base.TextInput(args);
            }
        }

        /// <summary>
        /// This handler is the best place to intercept keyboard input which contains
        /// modifiers that make it unsuitable for actual textual input.  Any combination 
        /// which can be translated into actual text input will be done so much more 
        /// accurately by WPF and will end up in the TextInput event.
        /// 
        /// Attempting to manually translate the arguments here from a Key and KeyModifiers
        /// to a char **will** fail because we lack enough information (dead keys, etc ...)
        /// 
        /// Instead we limit our search to the keys which we have a high degree of 
        /// confidence can't be mapped to textual input and will still be a valid Vim
        /// command.  Those are keys with control modifiers or those which don't map 
        /// to characters
        /// </summary>
        public override void KeyDown(KeyEventArgs args)
        {
            bool handled;
            if (!KeyUtil.IsInputKey(args.Key))
            {
                // Non input keys such as Alt, Control, etc ... by themselves are uninteresting
                // to Vim
                handled = false;
            }
            else if (KeyUtil.IsAltGr(args.KeyboardDevice.Modifiers))
            {
                // AltGr greatly confuses things becuase it's realized in WPF as Control | Alt.  So
                // while it's possible to use Control to further modify a key which used AltGr
                // originally the result is indistinguishable here (and in gVim).  Don't attempt
                // to process it
                handled = false;
            }
            else if (args.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                // When there are no keyboard modifiers then we only want to process input which 
                // can't be represented as a char.  If it can be represented by a char then 
                // it will appear in TextInput and we can do a much more definitive mapping
                KeyInput ki;
                handled = KeyUtil.TryConvertToKeyInput(args.Key, out ki) && !ki.IsCharOnly
                    ? _buffer.CanProcess(ki) && _buffer.Process(ki)
                    : false;
            }
            else if (0 != (args.KeyboardDevice.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)))
            {
                // There is a modifier and it's not just shift.  Attempt to convert the input 
                // and see if can be handled by Vim
                KeyInput ki;
                handled = KeyUtil.TryConvertToKeyInput(args.Key, args.KeyboardDevice.Modifiers, out ki)
                    && _buffer.CanProcess(ki)
                    && _buffer.Process(ki);
            }
            else
            {
                handled = false;
            }

            if (handled)
            {
                args.Handled = true;
            }
            else
            {
                base.KeyDown(args);
            }
        }

    }
}
