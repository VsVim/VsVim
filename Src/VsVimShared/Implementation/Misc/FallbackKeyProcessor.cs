using EnvDTE;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Windows.Input;
using Vim;
using Vim.UI.Wpf;

namespace VsVim.Implementation.Misc
{
    internal sealed class FallbackKeyProcessor : KeyProcessor, IDisposable
    {
        private struct FallbackCommand
        {
            private KeyInput _keyInput;
            private string _command;

            public FallbackCommand(KeyInput keyInput, string command)
            {
                _keyInput = keyInput;
                _command = command;
            }
            public KeyInput KeyInput { get { return _keyInput; } }
            public string Command { get { return _command; } }
        }

        private readonly _DTE _dte;
        private readonly IKeyUtil _keyUtil;
        private readonly IWpfTextView _textView;
        private readonly IKeyBindingService _keyBindingService;

        private List<FallbackCommand> _fallbackList;

        internal FallbackKeyProcessor(_DTE dte, IKeyUtil keyUtil, IKeyBindingService keyBindingService, IWpfTextView wpfTextView)
        {
            _dte = dte;
            _keyUtil = keyUtil;
            _keyBindingService = keyBindingService;
            _textView = wpfTextView;
            _fallbackList = new List<FallbackCommand>();

            _keyBindingService.ConflictingKeyBindingStateChanged += OnStateChanged;
            OnStateChanged();
        }

        private void Unsubscribe()
        {
            _keyBindingService.ConflictingKeyBindingStateChanged -= OnStateChanged;
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            OnStateChanged();
        }

        private void OnStateChanged()
        {
            VimTrace.TraceInfo("FallbackKeyProcessor::OnStateChanged {0}", _keyBindingService.ConflictingKeyBindingState);
            switch (_keyBindingService.ConflictingKeyBindingState)
            {
                case ConflictingKeyBindingState.HasNotChecked:
                case ConflictingKeyBindingState.ConflictsIgnoredOrResolved:
                case ConflictingKeyBindingState.FoundConflicts:
                    break;
                default:
                    throw new Exception("Enum value unknown");
            }
            GetKeyBindings();
        }

        private void GetKeyBindings()
        {
            _fallbackList = new List<FallbackCommand>
            {
                new FallbackCommand(
                    KeyInputUtil.CharWithControlToKeyInput('F'), "Edit.Find"
                ),
                new FallbackCommand(
                    KeyInputUtil.ApplyModifiersToVimKey(VimKey.Home, KeyModifiers.Control), "Edit.DocumentStart"
                ),
                new FallbackCommand(
                    KeyInputUtil.ApplyModifiersToVimKey(VimKey.Left, KeyModifiers.Control), "Edit.WordPrevious"
                ),
                new FallbackCommand(
                    KeyInputUtil.ApplyModifiersToVimKey(VimKey.Right, KeyModifiers.Control), "Edit.WordNext"
                ),
                new FallbackCommand(
                    KeyInputUtil.ApplyModifiersToVimKey(VimKey.Left, KeyModifiers.Shift), "Edit.CharLeftExtend"
                ),
                new FallbackCommand(
                    KeyInputUtil.ApplyModifiersToVimKey(VimKey.Right, KeyModifiers.Shift), "Edit.CharRightExtend"
                ),
            };
        }

        public override void TextInput(TextCompositionEventArgs args)
        {
            VimTrace.TraceInfo("FallbackKeyProcessor::TextInput Text={0} ControlText={1} SystemText={2}", args.Text, args.ControlText, args.SystemText);
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

            VimTrace.TraceInfo("FallbackKeyProcessor::TextInput Handled={0}", handled);
            args.Handled = handled;
            base.TextInput(args);
        }

        public override void KeyDown(KeyEventArgs args)
        {
            VimTrace.TraceInfo("FallbackKeyProcessor::KeyDown {0} {1}", args.Key, args.KeyboardDevice.Modifiers);

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
            foreach (var fallbackCommand in _fallbackList)
            {
                if (fallbackCommand.KeyInput == keyInput)
                {
                    return SafeExecuteCommand(fallbackCommand.Command);
                }
            }
            return false;
        }

        private bool SafeExecuteCommand(string command, string args = "")
        {
            try
            {
                VimTrace.TraceInfo("FallbackKeyProcessor::SafeExecuteCommand {0} {1}", command, args);
                _dte.ExecuteCommand(command, args);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            Unsubscribe();
        }

    }
}
