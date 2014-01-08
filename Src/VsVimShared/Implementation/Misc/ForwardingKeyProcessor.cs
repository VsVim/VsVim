using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Vim;
using Vim.UI.Wpf;

namespace VsVim.Implementation.Misc
{
    internal sealed class ForwardingKeyProcessor : KeyProcessor
    {
        private readonly IKeyUtil _keyUtil;
        private readonly IWpfTextView _textView;

        internal ForwardingKeyProcessor(IKeyUtil keyUtil, IWpfTextView wpfTextView)
        {
            _keyUtil = keyUtil;
            _textView = wpfTextView;
        }

        internal bool TryProcess(KeyInput keyInput)
        {
            return false;
        }

        public override void TextInput(TextCompositionEventArgs args)
        {
            VimTrace.TraceInfo("ForwardingKeyProcessor::TextInput Text={0} ControlText={1} SystemText={2}", args.Text, args.ControlText, args.SystemText);
            bool handled = false;

            var text = args.Text;
            if (String.IsNullOrEmpty(text))
            {
                text = args.ControlText;
            }

            if (!String.IsNullOrEmpty(text))
            {
                // In the case of a failed dead key mapping (pressing the accent key twice for
                // example) we will recieve a multi-length string here.  One character for every
                // one of the mappings.  Make sure to handle each of them
                for (var i = 0; i < text.Length; i++)
                {
                    var keyInput = KeyInputUtil.CharToKeyInput(text[i]);
                    handled = TryProcess(keyInput);
                }
            }
            else if (!String.IsNullOrEmpty(args.SystemText))
            {
                // The system text needs to be processed differently than normal text.  When 'a'
                // is pressed with control it will come in as control text as the proper control
                // character.  When 'a' is pressed with Alt it will come in as simply 'a' and we
                // have to rely on the currently pressed key modifiers to determine the appropriate
                // character
                var keyboardDevice = args.Device as KeyboardDevice;
                var keyModifiers = keyboardDevice != null
                    ? _keyUtil.GetKeyModifiers(keyboardDevice.Modifiers)
                    : KeyModifiers.Alt;

                text = args.SystemText;
                for (var i = 0; i < text.Length; i++)
                {
                    var keyInput = KeyInputUtil.ApplyModifiers(KeyInputUtil.CharToKeyInput(text[i]), keyModifiers);
                    handled = TryProcess(keyInput);
                }
            }

            VimTrace.TraceInfo("ForwardingKeyProcessor::TextInput Handled={0}", handled);
            args.Handled = handled;
            base.TextInput(args);
        }
    }
}
