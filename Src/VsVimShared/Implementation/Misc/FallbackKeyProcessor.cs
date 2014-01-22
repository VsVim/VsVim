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
        private readonly IVimBuffer _vimBuffer;
        private readonly ScopeData _scopeData;

        private List<FallbackCommand> _fallbackCommandList;

        /// <summary>
        /// In general a key processor applies to a specific IWpfTextView but
        /// by not making use of it, the fallback processor can be reused for
        /// multiple text views
        /// </summary>
        internal FallbackKeyProcessor(_DTE dte, IKeyUtil keyUtil, IVimApplicationSettings vimApplicationSettings, ITextView textView, IVimBuffer vimBuffer, ScopeData scopeData)
        {
            _dte = dte;
            _keyUtil = keyUtil;
            _vimApplicationSettings = vimApplicationSettings;
            _vimBuffer = vimBuffer;
            _scopeData = scopeData;
            _fallbackCommandList = new List<FallbackCommand>();

            // Register for key binding changes and get the current bindings
            _vimApplicationSettings.SettingsChanged += OnSettingsChanged;
            GetKeyBindings();

            textView.Closed += OnTextViewClosed;
        }

        private void OnSettingsChanged(object sender, ApplicationSettingsEventArgs e)
        {
            GetKeyBindings();
        }

        private void OnTextViewClosed(object sender, EventArgs e)
        {
            _vimApplicationSettings.SettingsChanged -= OnSettingsChanged;
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
            switch (_scopeData.GetScopeKind(binding.KeyBinding.Scope))
            {
                case ScopeKind.TextEditor:
                case ScopeKind.Global:
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
            switch (_scopeData.GetScopeKind(scope))
            {
                case ScopeKind.TextEditor:
                    return 1;
                case ScopeKind.Global:
                    return 2;
                default:
                    throw new InvalidOperationException("Unexpected ScopeKind");
            }
        }

        /// <summary>
        /// This maps letters which are pressed to a KeyInput value by looking at the virtual
        /// key vs the textual input.  When Visual Studio is processing key mappings it's doing
        /// so in PreTraslateMessage and using the virtual key codes.  We need to simulate this
        /// as best as possible when forwarding keys
        /// </summary>
        private bool TryConvertLetterToKeyInput(KeyEventArgs keyEventArgs, out KeyInput keyInput)
        {
            keyInput = KeyInput.DefaultValue;

            var keyboardDevice = keyEventArgs.Device as KeyboardDevice;
            var keyModifiers = keyboardDevice != null
                ? _keyUtil.GetKeyModifiers(keyboardDevice.Modifiers)
                : KeyModifiers.Alt;
            if (keyModifiers == KeyModifiers.None)
            {
                return false;
            }

            var key = keyEventArgs.Key;
            if (key < Key.A || key > Key.Z)
            {
                return false;
            }

            var c = (char)('a' + (key - Key.A));
            keyInput = KeyInputUtil.ApplyModifiersToChar(c, keyModifiers);
            return true;
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
            else if (TryConvertLetterToKeyInput(args, out keyInput))
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
            // If this processor is associated with a IVimBuffer then don't fall back to VS commands 
            // unless vim is currently disabled
            if (_vimBuffer != null && _vimBuffer.ModeKind != ModeKind.Disabled)
            {
                return false;
            }

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
