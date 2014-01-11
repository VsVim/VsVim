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
    /// <summary>
    /// The fallback key processor handles all the keys VsVim had to unbind
    /// due to conflicts
    /// </summary>
    internal sealed class FallbackKeyProcessor : KeyProcessor
    {
        /// <summary>
        /// A fallback command is a tuple of a previously bound key paired
        /// with its corresponding Visual Studio command
        /// </summary>
        internal struct FallbackCommand
        {
            private KeyInput _keyInput;
            private CommandId _command;

            internal FallbackCommand(KeyInput keyInput, CommandId command)
            {
                _keyInput = keyInput;
                _command = command;
            }
            internal KeyInput KeyInput { get { return _keyInput; } }
            internal CommandId Command { get { return _command; } }
        }

        private readonly _DTE _dte;
        private readonly IKeyUtil _keyUtil;
        private readonly IVimApplicationSettings _vimApplicationSettings;

        private List<FallbackCommand> _fallbackCommandList;

        /// <summary>
        /// In general a key processor applies to a specific IWpfTextView but
        /// by not making use of it, the fallback processor can be reused for
        /// multiple text views
        /// </summary>
        internal FallbackKeyProcessor(_DTE dte, IKeyUtil keyUtil, IVimApplicationSettings vimApplicationSettings)
        {
            _dte = dte;
            _keyUtil = keyUtil;
            _vimApplicationSettings = vimApplicationSettings;
            _fallbackCommandList = new List<FallbackCommand>();

            // Register for key binding changes and get the current bindings
            _vimApplicationSettings.SettingsChanged += SettingsChanged;
            GetKeyBindings();
        }

        private void SettingsChanged(object sender, ApplicationSettingsEventArgs e)
        {
            GetKeyBindings();
        }

        /// <summary>
        /// Get conflicting key bindings stored in our application settings
        /// </summary>
        private void GetKeyBindings()
        {
            // Construct a list of all fallback commands keys we had to unbind.
            // We are only interested in the bindings scoped to text views and
            // consisting of a single keystroke. Sort the bindings in order of
            // more specific bindings first
            _fallbackCommandList = _vimApplicationSettings.RemovedBindings
                .Where(binding => IsTextViewBinding(binding))
                .Select(binding => Tuple.Create(
                    binding.KeyBinding.Scope,
                    KeyBinding.Parse(binding.KeyBinding.CommandString),
                    binding.Id
                ))
                .Where(tuple => tuple.Item2.KeyStrokes.Count == 1)
                .OrderBy(tuple => GetScopeOrder(tuple.Item1))
                .Select(tuple => new FallbackCommand(
                    tuple.Item2.FirstKeyStroke.AggregateKeyInput,
                    tuple.Item3
                ))
                .ToList();
        }

        /// <summary>
        /// True if the binding is applicable to a text view
        /// </summary>
        private bool IsTextViewBinding(CommandKeyBinding binding)
        {
            switch (binding.KeyBinding.Scope)
            {
                case ScopeData.DefaultTextEditorScopeName:
                case ScopeData.DefaultGlobalScopeName:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Get a sortable numeric value corresponding to a scope.  Lower
        /// numbers cause the binding to be considered first
        /// </summary>
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

        /// <summary>
        /// Convert this key processors's text input into KeyInput and forward
        /// it to TryProcess
        /// </summary>
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

        /// <summary>
        /// Convert this key processors's keyboard events into KeyInput and
        /// forward it to TryProcess
        /// </summary>
        public override void KeyDown(KeyEventArgs args)
        {
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

        /// <summary>
        /// Try to process this KeyInput
        /// </summary>
        internal bool TryProcess(KeyInput keyInput)
        {
            // Check for any applicable fallback bindings, in order
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

        /// <summary>
        /// Safely execute a Visual Studio command
        /// </summary>
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
