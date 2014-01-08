using EnvDTE;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Windows.Input;
using Vim;
using Vim.UI.Wpf;

namespace VsVim.Implementation.Misc
{
    internal sealed class ForwardingKeyProcessor : KeyProcessor
    {
        private readonly _DTE _dte;
        private readonly IKeyUtil _keyUtil;
        private readonly IWpfTextView _textView;

        internal ForwardingKeyProcessor(_DTE dte, IKeyUtil keyUtil, IWpfTextView wpfTextView)
        {
            _dte = dte;
            _keyUtil = keyUtil;
            _textView = wpfTextView;
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
                for (var i = 0; i < text.Length; i++)
                {
                    var keyInput = KeyInputUtil.CharToKeyInput(text[i]);
                    handled = TryProcess(keyInput);
                }
            }
            else if (!String.IsNullOrEmpty(args.SystemText))
            {
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

        public override void KeyDown(KeyEventArgs args)
        {
            VimTrace.TraceInfo("ForwardingKeyProcessor::KeyDown {0} {1}", args.Key, args.KeyboardDevice.Modifiers);

            bool handled;
            KeyInput keyInput;
            if (_keyUtil.TryConvertSpecialToKeyInput(args.Key, args.KeyboardDevice.Modifiers, out keyInput))
            {
                handled = TryProcess(keyInput);
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

        internal bool TryProcess(KeyInput keyInput)
        {
            if (keyInput == KeyInputUtil.CharWithControlToKeyInput('F'))
            {
                return SafeExecuteCommand("Edit.Find");
            }
            if (keyInput == KeyInputUtil.ApplyModifiersToVimKey(VimKey.Home, KeyModifiers.Control))
            {
                return SafeExecuteCommand("Edit.DocumentStart");
            }
            if (keyInput == KeyInputUtil.ApplyModifiersToVimKey(VimKey.Left, KeyModifiers.Control))
            {
                return SafeExecuteCommand("Edit.WordPrevious");
            }
            if (keyInput == KeyInputUtil.ApplyModifiersToVimKey(VimKey.Right, KeyModifiers.Control))
            {
                return SafeExecuteCommand("Edit.WordNext");
            }
            if (keyInput == KeyInputUtil.ApplyModifiersToVimKey(VimKey.Left, KeyModifiers.Shift))
            {
                return SafeExecuteCommand("Edit.CharLeftExtend");
            }
            if (keyInput == KeyInputUtil.ApplyModifiersToVimKey(VimKey.Right, KeyModifiers.Shift))
            {
                return SafeExecuteCommand("Edit.CharRightExtend");
            }
            return false;
        }

        private bool SafeExecuteCommand(string command, string args = "")
        {
            try
            {
                VimTrace.TraceInfo("ForwardingKeyProcessor::SafeExecuteCommand {0} {1}", command, args);
                _dte.ExecuteCommand(command, args);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
