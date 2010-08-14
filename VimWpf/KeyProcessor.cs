using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Vim.Extensions;

namespace Vim.UI.Wpf
{
    public sealed class KeyProcessor : Microsoft.VisualStudio.Text.Editor.KeyProcessor
    {
        private readonly IVimBuffer _buffer;

        public KeyProcessor(IVimBuffer buffer)
        {
            _buffer = buffer;
        }

        public override bool IsInterestedInHandledEvents
        {
            get { return true; }
        }

        /// <summary>
        /// When the user is typing we get events for every single key press.  This means that 
        /// typing something like an upper case character will cause at least 2 events to be
        /// generated.  
        ///  1) LeftShift 
        ///  2) LeftShift + b
        /// This helps us filter out items like #1 which we don't want to process
        /// </summar>
        private bool IsNonInputKey(Key k)
        {
            switch (k)
            {
                case Key.LeftAlt:
                case Key.LeftCtrl:
                case Key.LeftShift:
                case Key.RightAlt:
                case Key.RightCtrl:
                case Key.RightShift:
                case Key.System:
                    return true;
                default:
                    return false;
            }
        }

        private bool IsInputKey(Key k)
        {
            return !IsNonInputKey(k);
        }

        private bool TryHandleTextInput(TextCompositionEventArgs args)
        {
            if (1 != args.Text.Length)
            {
                return false;
            }
            else
            {
                // Only want to intercept text coming from the keyboard.  Let other 
                // components edit without having to come through us
                var keyboard = args.Device as KeyboardDevice;
                if (keyboard == null)
                {
                    return false;
                }

                var opt = KeyInputUtil.TryCharToKeyInput(args.Text[0]);
                if (!opt.IsSome())
                {
                    return false;
                }

                var ki = opt.Value;
                return _buffer.CanProcess(ki) && _buffer.Process(ki);
            }
        }

        public override void TextInput(TextCompositionEventArgs args)
        {
            if (TryHandleTextInput(args))
            {
                args.Handled = true;
            }
            else
            {
                base.TextInput(args);
            }
        }

        public override void KeyDown(KeyEventArgs args)
        {
            var isHandled = false;
            if (IsInputKey(args.Key))
            {
                var ki = KeyUtil.ConvertToKeyInput(args.Key, args.KeyboardDevice.Modifiers);
                isHandled = _buffer.CanProcess(ki) && _buffer.Process(ki);
            }

            if (isHandled)
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
