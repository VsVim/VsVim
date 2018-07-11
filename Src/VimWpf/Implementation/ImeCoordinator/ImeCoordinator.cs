using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf.Implementation.ImeCoordinator
{
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class ImeCoordinator : IVimBufferCreationListener
    {
        internal enum InputMode
        {
            None = 0,
            Command = 1,
            Insert = 2,
            Search = 3,
        };

        /// <summary>
        /// Provides an associative array abstraction over the underlying
        /// golobal IME-related settings
        /// </summary>
        internal class InputModeMap
        {
            private readonly IVimGlobalSettings _globalSettings;

            public InputModeMap(IVimGlobalSettings globalSettings)
            {
                _globalSettings = globalSettings;

                var state = InputMethod.Current.ImeState;
                SetState(InputMode.Command, state);
                SetState(InputMode.Insert, state);
                SetState(InputMode.Search, state);
            }

            public InputMethodState this[InputMode inputMode]
            {
                get
                {
                    return GetState(inputMode);
                }
                set
                {
                    SetState(inputMode, value);
                }
            }

            private InputMethodState GetState(InputMode inputMode)
            {
                switch (inputMode)
                {
                    case InputMode.Command:
                        if (_globalSettings.ImeCommand)
                        {
                            return InputMethodState.On;
                        }
                        else
                        {
                            return InputMethodState.Off;
                        }

                    case InputMode.Insert:
                        if ((_globalSettings.ImeInsert & 2) != 0)
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
                            if ((_globalSettings.ImeSearch & 2) != 0)
                            {
                                return InputMethodState.On;
                            }
                            else
                            {
                                return InputMethodState.Off;
                            }
                        }

                    default:
                        throw new ArgumentException("inputMode");
                }
            }

            private void SetState(InputMode inputMode, InputMethodState state)
            {
                switch (inputMode)
                {
                    case InputMode.Command:
                        switch (state)
                        {
                            case InputMethodState.On:
                                _globalSettings.ImeCommand = true;
                                break;

                            case InputMethodState.Off:
                                _globalSettings.ImeCommand = false;
                                break;

                            case InputMethodState.DoNotCare:
                                break;

                            default:
                                throw new ArgumentException("state");
                        }
                        break;

                    case InputMode.Insert:
                        switch (state)
                        {
                            case InputMethodState.On:
                                _globalSettings.ImeInsert |= 2;
                                break;

                            case InputMethodState.Off:
                                _globalSettings.ImeInsert &= ~2;
                                break;

                            case InputMethodState.DoNotCare:
                                break;

                            default:
                                throw new ArgumentException("state");
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
                                    _globalSettings.ImeSearch |= 2;
                                    break;

                                case InputMethodState.Off:
                                    _globalSettings.ImeSearch &= ~2;
                                    break;

                                case InputMethodState.DoNotCare:
                                    break;

                                default:
                                    throw new ArgumentException("state");
                            }
                        }
                        break;

                    default:
                        throw new ArgumentException("inputMode");
                }
            }
        }

        private readonly IVim _vim;
        private readonly InputModeMap _lastInputMethodState;

        private InputMode _inputMode;

        [ImportingConstructor]
        internal ImeCoordinator(IVim vim)
        {
            _vim = vim;
            _lastInputMethodState = new InputModeMap(_vim.GlobalSettings);

            _inputMode = InputMode.None;

            AdjustImeState(_inputMode, _inputMode);
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
                    if (vimBuffer.IncrementalSearch.InSearch)
                    {
                        return InputMode.Search;
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
            if (_vim.GlobalSettings.ImeDisable)
            {
                return;
            }

            if (oldInputMode != InputMode.None)
            {
                _lastInputMethodState[oldInputMode] = InputMethod.Current.ImeState;
            }

            if (newInputMode != InputMode.None)
            {
                InputMethod.Current.ImeState = _lastInputMethodState[newInputMode];
            }
            else
            {
                InputMethod.Current.ImeState = InputMethodState.Off;
            }

            if (_inputMode != InputMode.None)
            {
                VimTrace.TraceInfo($"ImeCoordinator: input mode with IME {InputMethod.Current.ImeState}");
            }
            else
            {
                VimTrace.TraceInfo($"ImeCoordinator: non-input mode with IME {InputMethod.Current.ImeState}");
            }
        }

        #region IVimBufferCreationListener

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            VimBufferCreated(vimBuffer);
        }

        #endregion
    }
}
