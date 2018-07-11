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
        private enum InputMode
        {
            None = 0,
            Command = 1,
            Insert = 2,
            Search = 3,
        };

        private readonly IVim _vim;
        private readonly Dictionary<InputMode, InputMethodState> _lastInputMethodState;

        private InputMode _inputMode;

        [ImportingConstructor]
        internal ImeCoordinator(IVim vim)
        {
            _vim = vim;
            _lastInputMethodState = new Dictionary<InputMode, InputMethodState>();

            _inputMode = InputMode.None;
            _lastInputMethodState[InputMode.Command] = InputMethodState.DoNotCare;
            _lastInputMethodState[InputMode.Insert] = InputMethodState.DoNotCare;
            _lastInputMethodState[InputMode.Search] = InputMethodState.DoNotCare;
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
                    return vimBuffer.IncrementalSearch.InSearch ? InputMode.Search : InputMode.None;

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
