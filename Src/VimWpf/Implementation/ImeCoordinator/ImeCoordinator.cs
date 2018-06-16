using System;
using System.ComponentModel.Composition;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf.Implementation.ImeCoordinator
{
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class ImeCoordinator : IVimBufferCreationListener
    {
        private readonly IVim _vim;

        private bool _inInputMode;
        private InputMethodState _lastInputMethodState;

        [ImportingConstructor]
        internal ImeCoordinator(IVim vim)
        {
            _vim = vim;

            _inInputMode = true;
            _lastInputMethodState = InputMethodState.DoNotCare;
        }

        void VimBufferCreated(IVimBuffer vimBuffer)
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
            var wasInInputMode = _inInputMode;
            var isInInputMode = IsInputMode(vimBuffer);
            _inInputMode = isInInputMode;

            if (wasInInputMode != isInInputMode)
            {
                AdjustImeState();
            }
        }

        private static bool IsInputMode(IVimBuffer vimBuffer)
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
                    return true;

                case ModeKind.Normal:
                    return vimBuffer.IncrementalSearch.InSearch;

                default:
                    return false;
            }
        }

        private void AdjustImeState()
        {
            if (_inInputMode)
            {
                InputMethod.Current.ImeState = _lastInputMethodState;
                VimTrace.TraceInfo($"ImeCoordinator: input mode with IME {InputMethod.Current.ImeState}");
            }
            else
            {
                _lastInputMethodState = InputMethod.Current.ImeState;
                InputMethod.Current.ImeState = InputMethodState.Off;
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
