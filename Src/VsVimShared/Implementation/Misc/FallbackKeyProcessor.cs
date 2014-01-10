using EnvDTE;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Vim;
using Vim.UI.Wpf;

namespace VsVim.Implementation.Misc
{
    internal sealed class FallbackKeyProcessor : KeyProcessor
    {
        private struct FallbackCommand
        {
            private KeyInput _keyInput;
            private CommandId _command;

            public FallbackCommand(KeyInput keyInput, CommandId command)
            {
                _keyInput = keyInput;
                _command = command;
            }
            public KeyInput KeyInput { get { return _keyInput; } }
            public CommandId Command { get { return _command; } }
        }

        private readonly _DTE _dte;
        private readonly IKeyUtil _keyUtil;
        private readonly IWpfTextView _textView;
        private readonly IVimApplicationSettings _vimApplicationSettings;

        private List<FallbackCommand> _fallbackCommandList;

        internal FallbackKeyProcessor(_DTE dte, IKeyUtil keyUtil, IVimApplicationSettings vimApplicationSettings, IWpfTextView wpfTextView)
        {
            _dte = dte;
            _keyUtil = keyUtil;
            _vimApplicationSettings = vimApplicationSettings;
            _textView = wpfTextView;
            _fallbackCommandList = new List<FallbackCommand>();

            GetKeyBindings();
        }

        private void GetKeyBindings()
        {
            _fallbackCommandList = _vimApplicationSettings.RemovedBindings
                .Where(binding => IsTextViewBinding(binding))
                .Select(binding => Tuple.Create(
                    binding.KeyBinding.Scope,
                    KeyBinding.Parse(binding.KeyBinding.CommandString),
                    binding.Id
                ))
                .Where(tuple => tuple.Item2.KeyStrokes.Count == 1)
                .OrderBy(tuple => GetScopeOrder(tuple.Item1))
                .Select(tuple => new FallbackCommand(tuple.Item2.FirstKeyStroke.AggregateKeyInput, tuple.Item3))
                .ToList();
        }

        private bool IsTextViewBinding(CommandKeyBinding binding)
        {
            var scope = binding.KeyBinding.Scope;
            switch (binding.KeyBinding.Scope)
            {
                case ScopeData.DefaultTextEditorScopeName:
                case ScopeData.DefaultGlobalScopeName:
                    return true;
                default:
                    return false;
            }
        }

        private int GetScopeOrder(string scope)
        {
            switch (scope)
            {
                case ScopeData.DefaultTextEditorScopeName:
                    return 1;
                case ScopeData.DefaultGlobalScopeName:
                    return 2;
                default:
                    throw new InvalidOperationException("scope not handled");
            }
        }

        public override void TextInput(TextCompositionEventArgs args)
        {
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

            args.Handled = handled;
            base.TextInput(args);
        }

        public override void KeyDown(KeyEventArgs args)
        {
            bool handled;
            KeyInput keyInput;
            if (_keyUtil.TryConvertSpecialToKeyInput(args.Key, args.KeyboardDevice.Modifiers, out keyInput))
            {
                args.Handled = TryProcess(keyInput);
            }
            else
            {
                base.KeyDown(args);
            }
        }

        internal bool TryProcess(KeyInput keyInput)
        {
            VimTrace.TraceInfo("FallbackKeyProcessor::TryProcess {0}", keyInput);
            foreach (var fallbackCommand in _fallbackCommandList)
            {
                if (fallbackCommand.KeyInput == keyInput)
                {
                    return SafeExecuteCommand(fallbackCommand.Command);
                }
            }
            return false;
        }

        private bool SafeExecuteCommand(CommandId command)
        {
            try
            {
                VimTrace.TraceInfo("FallbackKeyProcessor::SafeExecuteCommand {0}", command);
                object customIn = null;
                object customOut = null;
                _dte.Commands.Raise(command.Group.ToString(), (int)command.Id, ref customIn, ref customOut);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
