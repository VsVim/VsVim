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

        private InputMethodState _lastInsertMethodState;

        [ImportingConstructor]
        internal ImeCoordinator(IVim vim)
        {
            _vim = vim;

            _lastInsertMethodState = InputMethodState.On;
        }

        #region IVimBufferCreationListener

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            if (vimBuffer.TextView is IWpfTextView textView)
            {
                vimBuffer.SwitchedMode += OnModeSwitch;
                textView.GotAggregateFocus += OnGotFocus;
                textView.LostAggregateFocus += OnLostFocus;
                vimBuffer.Closed += OnBufferClosed;
            }
        }

        private void OnBufferClosed(object sender, EventArgs e)
        {
            if (sender is IVimBuffer vimBuffer && vimBuffer.TextView is IWpfTextView textView)
            {
                vimBuffer.SwitchedMode -= OnModeSwitch;
                textView.GotAggregateFocus -= OnGotFocus;
                textView.LostAggregateFocus -= OnLostFocus;
                vimBuffer.Closed -= OnBufferClosed;
            }
        }

        private void OnModeSwitch(object sender, SwitchModeEventArgs e)
        {
            OnLeaveMode(e.PreviousMode);
            OnEnterMode(e.CurrentMode);
        }

        private void OnGotFocus(object sender, EventArgs e)
        {
            if (sender is IWpfTextView textView)
            {
                if (_vim.TryGetVimBuffer(textView, out IVimBuffer vimBuffer))
                {
                    OnEnterMode(vimBuffer.Mode);
                }
            }
        }

        private void OnLostFocus(object sender, EventArgs e)
        {
            if (sender is IWpfTextView textView)
            {
                if (_vim.TryGetVimBuffer(textView, out IVimBuffer vimBuffer))
                {
                    OnLeaveMode(vimBuffer.Mode);
                }
            }
        }

        private void OnLeaveMode(IMode mode)
        {
            switch (mode.ModeKind)
            {
                case ModeKind.Insert:
                case ModeKind.Replace:
                    _lastInsertMethodState = InputMethod.Current.ImeState;
                    VimTrace.TraceInfo("ImeCoordinator: Leaving insert with IME {0}", _lastInsertMethodState);
                    break;

                default:
                    break;
            }
        }

        private void OnEnterMode(IMode mode)
        {
            switch (mode.ModeKind)
            {
                case ModeKind.Insert:
                case ModeKind.Replace:
                    InputMethod.Current.ImeState = _lastInsertMethodState;
                    VimTrace.TraceInfo("ImeCoordinator: Entering insert with IME {0}", _lastInsertMethodState);
                    break;

                default:
                    InputMethod.Current.ImeState = InputMethodState.Off;
                    VimTrace.TraceInfo("ImeCoordinator: Entering non-insert with IME {0}", InputMethodState.Off);
                    break;
            }
        }

        #endregion
    }
}
