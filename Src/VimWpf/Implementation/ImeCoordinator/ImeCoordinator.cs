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
            }
        }

        private void OnModeSwitch(object sender, SwitchModeEventArgs e)
        {
            OnLeaveModeKind(e.PreviousMode.ModeKind);
            OnEnterModeKind(e.CurrentMode.ModeKind);
        }

        private void OnGotFocus(object sender, EventArgs e)
        {
            if (sender is IWpfTextView textView)
            {
                if (_vim.TryGetVimBuffer(textView, out IVimBuffer vimBuffer))
                {
                    OnEnterModeKind(vimBuffer.ModeKind);
                }
            }
        }

        private void OnLostFocus(object sender, EventArgs e)
        {
            if (sender is IWpfTextView textView)
            {
                if (_vim.TryGetVimBuffer(textView, out IVimBuffer vimBuffer))
                {
                    OnLeaveModeKind(vimBuffer.ModeKind);
                }
            }
        }

        private void OnLeaveModeKind(ModeKind modeKind)
        {
            switch (modeKind)
            {
                case ModeKind.Insert:
                case ModeKind.Replace:
                    _lastInsertMethodState = InputMethod.Current.ImeState;
                    VimTrace.TraceInfo("ImeCoordinator: Leaving insert with {0}", _lastInsertMethodState);
                    break;

                default:
                    break;
            }
        }

        private void OnEnterModeKind(ModeKind modeKind)
        {
            switch (modeKind)
            {
                case ModeKind.Insert:
                case ModeKind.Replace:
                    InputMethod.Current.ImeState = _lastInsertMethodState;
                    VimTrace.TraceInfo("ImeCoordinator: Entering insert with {0}", _lastInsertMethodState);
                    break;

                default:
                    InputMethod.Current.ImeState = InputMethodState.Off;
                    break;
            }
        }

        #endregion
    }
}
