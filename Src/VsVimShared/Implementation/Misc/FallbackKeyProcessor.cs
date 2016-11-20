using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Vim;
using Vim.UI.Wpf;

namespace Vim.VisualStudio.Implementation.Misc
{
    /// <summary>
    /// The fallback key processor handles all the keys VsVim had to unbind
    /// due to conflicts
    /// </summary>
    internal sealed class FallbackKeyProcessor : KeyProcessor
    {
        /// <summary>
        /// A smaller container that represents the parts of a KeyStroke that
        /// the FallbackKeyProcessor uses.
        /// </summary>
        internal struct KeyCharModifier
        {
            private readonly char _char;
            private readonly VimKeyModifiers _keyModifiers;

            internal KeyCharModifier(char c, VimKeyModifiers keyModifiers)
            {
                _char = c;
                _keyModifiers = keyModifiers;
            }

            internal char Char { get { return _char; } }
            internal VimKeyModifiers KeyModifiers { get { return _keyModifiers; } }

            internal static KeyCharModifier Create(KeyStroke stroke)
            {
                return new KeyCharModifier(stroke.Char, stroke.KeyModifiers);
            }
        }

        /// <summary>
        /// A fallback command is a tuple of a previously bound key paired
        /// with its corresponding Visual Studio command
        /// </summary>
        internal struct FallbackCommand
        {
            private readonly ScopeKind _scopeKind;
            private readonly List<KeyCharModifier> _keyBinding;
            private readonly CommandId _command;

            internal FallbackCommand(ScopeKind scopeKind, KeyBinding keyBinding, CommandId command)
            {
                _scopeKind = scopeKind;
                _keyBinding = keyBinding.KeyStrokes.Select(KeyCharModifier.Create).ToList();
                _command = command;
            }
            internal ScopeKind ScopeKind { get { return _scopeKind; } }
            internal List<KeyCharModifier> KeyBindings { get { return _keyBinding; } }
            internal CommandId Command { get { return _command; } }
        }

        private readonly _DTE _dte;
        private readonly IVsShell _vsShell;
        private readonly IKeyUtil _keyUtil;
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly IVimBuffer _vimBuffer;
        private readonly ScopeData _scopeData;

        private KeyInput _firstChord;
        private ILookup<char, FallbackCommand> _fallbackCommandList;

        /// <summary>
        /// In general a key processor applies to a specific IWpfTextView but
        /// by not making use of it, the fallback processor can be reused for
        /// multiple text views
        /// </summary>
        internal FallbackKeyProcessor(IVsShell vsShell, _DTE dte, IKeyUtil keyUtil, IVimApplicationSettings vimApplicationSettings, ITextView textView, IVimBuffer vimBuffer, ScopeData scopeData)
        {
            _vsShell = vsShell;
            _dte = dte;
            _keyUtil = keyUtil;
            _vimApplicationSettings = vimApplicationSettings;
            _vimBuffer = vimBuffer;
            _scopeData = scopeData;
            _fallbackCommandList = Enumerable.Empty<FallbackCommand>().ToLookup(x => 'a');

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
            // We are only interested in the bindings scoped to text views and global.
            _fallbackCommandList = _vimApplicationSettings.RemovedBindings
                .Where(binding => IsTextViewBinding(binding))
                .Select(binding => new FallbackCommand(
                    _scopeData.GetScopeKind(binding.KeyBinding.Scope),
                    KeyBinding.Parse(binding.KeyBinding.CommandString),
                    binding.Id
                ))
                .ToLookup(fallbackCommand => fallbackCommand.KeyBindings[0].Char);
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
        private int GetScopeOrder(ScopeKind scope)
        {
            switch (scope)
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
                : VimKeyModifiers.Alt;
            if (keyModifiers == VimKeyModifiers.None)
            {
                return false;
            }

            var key = keyEventArgs.Key;
            if (key < Key.A || key > Key.Z)
            {
                return false;
            }

            var c = (char)('a' + (key - Key.A));
            keyInput = KeyInputUtil.ChangeKeyModifiersDangerous(KeyInputUtil.CharToKeyInput(c), keyModifiers);
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
                _firstChord = null;
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
                _firstChord = null;
                return false;
            }

            // When a modal dialog is active don't turn key strokes into commands.  This happens when
            // the editor is hosted as a control in a modal window.  No command routing should 
            // take place in this scenario
            if (_vsShell.IsInModalState())
            {
                _firstChord = null;
                return false;
            }

            // Check for any applicable fallback bindings, in order
            VimTrace.TraceInfo("FallbackKeyProcessor::TryProcess {0}", keyInput);
            var findFirstChar = _firstChord != null ? _firstChord.Char : keyInput.Char;
            var findFirstModifiers = _firstChord != null ? _firstChord.KeyModifiers : keyInput.KeyModifiers;
            var cmds = _fallbackCommandList[findFirstChar]
                .Where(fallbackCommand => fallbackCommand.KeyBindings[0].KeyModifiers == findFirstModifiers)
                .OrderBy(fallbackCommand => GetScopeOrder(fallbackCommand.ScopeKind))
                .ToList();

            if (cmds.Count == 0)
            {
                _firstChord = null;
                return false;
            }
            else if (cmds.Count == 1 && cmds[0].KeyBindings.Count == 1)
            {
                _firstChord = null;
                var cmd = cmds.First();
                return SafeExecuteCommand(cmd.Command);
            }
            else if (_firstChord != null)
            {
                var secondChord = cmds
                    .Where(fallbackCommand => fallbackCommand.KeyBindings[1].KeyModifiers == keyInput.KeyModifiers &&
                        fallbackCommand.KeyBindings[1].Char == keyInput.Char)
                    .OrderBy(fallbackCommand => GetScopeOrder(fallbackCommand.ScopeKind))
                    .ToList();

                if (secondChord.Count == 0)
                {
                    _firstChord = null;
                    return false;
                }

                _firstChord = null;
                var cmd = secondChord.First();
                return SafeExecuteCommand(cmd.Command);
            }
            else
            {
                _firstChord = keyInput;
                return true;
            }
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
