using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.Windows.Input;
using Vim;

namespace VsVim
{
    public sealed class VimKeyProcessor : KeyProcessor
    {
        private readonly IWpfTextView _view;

        public VimKeyProcessor(IWpfTextView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException("view");
            }

            _view = view;
        }

        public override void KeyUp(KeyEventArgs args)
        {
            var buf = _view.Properties.TryGetTypedProperty<VsVimBuffer>();
            if (buf.IsSome() && TryHandleKeyUp(buf.Value, args))
            {
                args.Handled = true;
            }
            else
            {
                base.KeyUp(args);
            }
        }

        public override void TextInput(TextCompositionEventArgs args)
        {
            var buf = _view.Properties.TryGetTypedProperty<VsVimBuffer>();
            if (buf.IsSome() && TryHandleTextInput(buf.Value, args))
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
            var buf = _view.Properties.TryGetTypedProperty<VsVimBuffer>();
            if (buf.IsSome() && !IsNonInputKey(args))
            {
                KeyDown(buf.Value, args);
            }
            else
            {
                base.KeyDown(args);
            }
        }

        private void KeyDown(VsVimBuffer buffer, KeyEventArgs args)
        {
            var input = args.ConvertToKeyInput();
            var vim = buffer.VimBuffer;
            if (vim.CanProcessInput(input))
            {
                args.Handled = vim.ProcessInput(input);
            }
        }

        /// <summary>
        /// Certain Keys only come as KeyUp event and we need to process
        /// them here
        /// </summary>
        private bool TryHandleKeyUp(VsVimBuffer buffer, KeyEventArgs args)
        {
            KeyInput ki = null;
            switch (args.Key)
            {
                case Key.Escape:
                    ki = InputUtil.KeyToKeyInput(Key.Escape);
                    break;

            }

            if (ki == null)
            {
                return false;
            }

            return buffer.VimBuffer.CanProcessInput(ki) && buffer.VimBuffer.ProcessInput(ki);
        }

        private bool TryHandleTextInput(VsVimBuffer buffer, TextCompositionEventArgs args)
        {
            if (1 != args.Text.Length)
            {
                return false;
            }

            var keyboard = args.Device as KeyboardDevice;
            if (keyboard == null)
            {
                return false;
            }

            var opt = InputUtil.TryCharToKeyInput(args.Text[0]);
            if (!opt.IsSome())
            {
                return false;
            }

            var ki = new KeyInput(opt.Value.Char, opt.Value.Key, keyboard.Modifiers);
            return buffer.VimBuffer.CanProcessInput(ki);
        }

        public static bool IsNonInputKey(KeyEventArgs e)
        {
            return IsNonInputKey(e.Key);
        }

        public static bool IsNonInputKey(Key key)
        {
            switch (key)
            {
                case Key.LeftAlt:
                case Key.LeftCtrl:
                case Key.LeftShift:
                case Key.RightAlt:
                case Key.RightCtrl:
                case Key.RightShift:
                    return true;

                default:
                    return false;

            }
        }
    }
}
