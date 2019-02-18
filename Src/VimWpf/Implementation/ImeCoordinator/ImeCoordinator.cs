using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf.Implementation.ImeCoordinator
{
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class ImeCoordinator : IVimBufferCreationListener, IDisposable
    {
        internal enum InputMode
        {
            None = 0,
            Command = 1,
            Insert = 2,
            Search = 3,
        };

        /// <summary>
        /// A mapping from input mode to IME state using the
        /// global IME-related settings as a backing store
        /// </summary>
        internal class InputModeState
        {
            private readonly IVimGlobalSettings _globalSettings;

            private bool _synchronizingSettings;

            public InputModeState(IVimGlobalSettings globalSettings)
            {
                _globalSettings = globalSettings;

                _synchronizingSettings = false;
            }

            public bool SynchronizingSettings
            {
                get
                {
                    return _synchronizingSettings;
                }
            }

            public InputMethodState this[InputMode inputMode]
            {
                get
                {
                    return GetState(inputMode);
                }
                set
                {
                    SynchronizeState(inputMode, value);
                }
            }

            private InputMethodState GetState(InputMode inputMode)
            {
                switch (inputMode)
                {
                    case InputMode.Command:
                        if (_globalSettings.ImeCommand)
                        {
                            return GetState(InputMode.Insert);
                        }
                        else
                        {
                            return InputMethodState.Off;
                        }

                    case InputMode.Insert:
                        if (_globalSettings.ImeInsert == 2)
                        {
                            return InputMethodState.On;
                        }
                        else
                        {
                            return InputMethodState.Off;
                        }

                    case InputMode.Search:
                        if (_globalSettings.ImeSearch == -1)
                        {
                            return GetState(InputMode.Insert);
                        }
                        else
                        {
                            if (_globalSettings.ImeSearch == 2)
                            {
                                return InputMethodState.On;
                            }
                            else
                            {
                                return InputMethodState.Off;
                            }
                        }

                    default:
                        throw new ArgumentException(nameof(inputMode));
                }
            }

            private void SynchronizeState(InputMode inputMode, InputMethodState state)
            {
                try
                {
                    _synchronizingSettings = true;
                    SetState(inputMode, state);
                }
                finally
                {
                    _synchronizingSettings = false;
                }
            }

            private void SetState(InputMode inputMode, InputMethodState state)
            {
                switch (inputMode)
                {
                    case InputMode.Command:
                        if (_globalSettings.ImeCommand)
                        {
                            SetState(InputMode.Insert, state);
                        }
                        break;

                    case InputMode.Insert:
                        switch (state)
                        {
                            case InputMethodState.On:
                                _globalSettings.ImeInsert = 2;
                                break;

                            case InputMethodState.Off:
                                _globalSettings.ImeInsert = 0;
                                break;

                            case InputMethodState.DoNotCare:
                                break;

                            default:
                                throw new ArgumentException(nameof(state));
                        }
                        break;

                    case InputMode.Search:
                        if (_globalSettings.ImeSearch == -1)
                        {
                            SetState(InputMode.Insert, state);
                        }
                        else
                        {
                            switch (state)
                            {
                                case InputMethodState.On:
                                    _globalSettings.ImeSearch = 2;
                                    break;

                                case InputMethodState.Off:
                                    _globalSettings.ImeSearch = 0;
                                    break;

                                case InputMethodState.DoNotCare:
                                    break;

                                default:
                                    throw new ArgumentException(nameof(state));
                            }
                        }
                        break;

                    default:
                        throw new ArgumentException(nameof(inputMode));
                }
            }
        }

        private readonly IVim _vim;
        private readonly IVimGlobalSettings _globalSettings;
        private readonly InputModeState _inputModeState;

        private InputMode _inputMode;

        [ImportingConstructor]
        internal ImeCoordinator(IVim vim)
        {
            _vim = vim;
            _globalSettings = _vim.GlobalSettings;
            _inputModeState = new InputModeState(_vim.GlobalSettings);

            _inputMode = InputMode.None;

            _vim.GlobalSettings.SettingChanged += OnSettingChanged;

            AdjustImeState(_inputMode, _inputMode);
        }

        private void Dispose()
        {
            _vim.GlobalSettings.SettingChanged -= OnSettingChanged;
        }

        private void OnSettingChanged(object sender, SettingEventArgs args)
        {
            // Don't manipulate the IME when the IME coordinator is disabled.
            if (_globalSettings.ImeDisable)
            {
                return;
            }

            // Don't manipulate the IME if we have language mappings.
            if (GetHaveLanguageMappings())
            {
                return;
            }

            // Ignore settings changed event when we are synchronizing settings.
            if (_inputModeState.SynchronizingSettings)
            {
                return;
            }

            // Get the input mode for the setting that changed.
            var settingInputMode = GetSettingInputMode(args.Setting);
            if (settingInputMode == InputMode.None)
            {
                return;
            }

            // If we're currently in that input mode, update the live IME state.
            if (settingInputMode == _inputMode)
            {
                SetImeState(_inputModeState[settingInputMode]);
            }
            else if (settingInputMode == InputMode.Insert)
            {
                // If 'imsearch' is -1, treat search mode as if it were insert mode.
                if (_globalSettings.ImeSearch == -1 && _inputMode == InputMode.Search)
                {
                    SetImeState(_inputModeState[settingInputMode]);
                }

                // If 'imcmdline' is set, treat command mode as if it were insert mode.
                if (_globalSettings.ImeCommand && _inputMode == InputMode.Command)
                {
                    SetImeState(_inputModeState[settingInputMode]);
                }
            }
        }

        /// <summary>
        /// Get the input mode corresponding to the specified setting
        /// </summary>
        /// <param name="setting"></param>
        /// <returns></returns>
        private InputMode GetSettingInputMode(Setting setting)
        {
            if (setting.Name == GlobalSettingNames.ImeInsertName)
            {
                return InputMode.Insert;
            }
            else if (setting.Name == GlobalSettingNames.ImeSearchName)
            {
                return InputMode.Search;
            }
            else
            {
                return InputMode.None;
            }
        }

        private void VimBufferCreated(IVimBuffer vimBuffer)
        {
            if (vimBuffer.TextView is IWpfTextView textView)
            {
                vimBuffer.KeyInputProcessed += OnKeyInputProcessed;
                textView.GotAggregateFocus += OnGotFocus;
                vimBuffer.Closed += OnBufferClosed;
            }
        }

        private void OnKeyInputProcessed(object sender, KeyInputProcessedEventArgs e)
        {
            if (sender is IVimBuffer vimBuffer)
            {
                OnInputModeRelatedEvent(vimBuffer);
            }
        }

        private void OnGotFocus(object sender, EventArgs e)
        {
            if (sender is IWpfTextView textView)
            {
                if (_vim.TryGetVimBuffer(textView, out IVimBuffer vimBuffer))
                {
                    OnInputModeRelatedEvent(vimBuffer);
                }
            }
        }

        private void OnBufferClosed(object sender, EventArgs e)
        {
            if (sender is IVimBuffer vimBuffer && vimBuffer.TextView is IWpfTextView textView)
            {
                vimBuffer.KeyInputProcessed -= OnKeyInputProcessed;
                textView.GotAggregateFocus -= OnGotFocus;
                vimBuffer.Closed -= OnBufferClosed;
            }
        }

        private void OnInputModeRelatedEvent(IVimBuffer vimBuffer)
        {
            var oldInputMode = _inputMode;
            var newInputMode = GetInputMode(vimBuffer);
            _inputMode = newInputMode;

            if (oldInputMode != newInputMode)
            {
                AdjustImeState(oldInputMode, newInputMode);
            }
        }

        private static InputMode GetInputMode(IVimBuffer vimBuffer)
        {
            switch (vimBuffer.Mode.ModeKind)
            {
                case ModeKind.Insert:
                case ModeKind.Replace:
                case ModeKind.SelectCharacter:
                case ModeKind.SelectLine:
                case ModeKind.SelectBlock:
                case ModeKind.ExternalEdit:
                case ModeKind.Disabled:
                    return InputMode.Insert;

                case ModeKind.Normal:
                    if (vimBuffer.IncrementalSearch.HasActiveSession)
                    {
                        // User is in the middle of a '/' or '?' search.
                        return InputMode.Search;
                    }
                    else if (vimBuffer.NormalMode.KeyRemapMode == KeyRemapMode.Language)
                    {
                        // User is using 'f', 'F' or 'r' to input one character.
                        return InputMode.Insert;
                    }
                    else
                    {
                        return InputMode.None;
                    }

                case ModeKind.Command:
                    return InputMode.Command;

                default:
                    return InputMode.None;
            }
        }

        private void AdjustImeState(InputMode oldInputMode, InputMode newInputMode)
        {
            // Don't manipulate the IME when the IME coordinator is disabled.
            if (_vim.GlobalSettings.ImeDisable)
            {
                return;
            }

            // Don't manipulate the IME if we have language mappings.
            if (GetHaveLanguageMappings())
            {
                return;
            }

            if (oldInputMode != InputMode.None)
            {
                _inputModeState[oldInputMode] = GetImeState();
            }

            if (newInputMode != InputMode.None)
            {
                SetImeState(_inputModeState[newInputMode]);
            }
            else
            {
                SetImeState(InputMethodState.Off);
            }
        }

        private InputMethodState GetImeState()
        {
            return InputMethod.Current.ImeState;
        }

        private void SetImeState(InputMethodState state)
        {
            InputMethod.Current.ImeState = state;
            VimTrace.TraceInfo($"ImeCoordinator: in mode = {_inputMode} turning IME {state}");
        }

        private bool GetHaveLanguageMappings()
        {
            return !_vim.KeyMap.GetKeyMappingsForMode(KeyRemapMode.Language).IsEmpty;
        }

        #region IVimBufferCreationListener

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            VimBufferCreated(vimBuffer);
        }
        #endregion

        #region IDispose

        void IDisposable.Dispose()
        {
            Dispose();
        }

        #endregion

    }
}
