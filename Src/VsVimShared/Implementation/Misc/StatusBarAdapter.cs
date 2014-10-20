using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using Vim.UI.Wpf;
using Vim.Extensions;

namespace Vim.VisualStudio.Implementation.Misc
{
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class StatusBarAdapter : IVimBufferCreationListener
    {
        private readonly ICommandMarginUtil _commandMarginUtil;
        private readonly IVsStatusbar _vsStatusbar;
        private readonly IVimProtectedOperations _vimProtectedOperations;
        private readonly IVim _vim;
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly DispatcherTimer _timer;

        [ImportingConstructor]
        internal StatusBarAdapter(IVim vim, IVimProtectedOperations vimProtectedOperations, ICommandMarginUtil commandMarginUtil, IVimApplicationSettings vimApplicationSettings, SVsServiceProvider vsServiceProvider)
        {
            _vim = vim;
            _vimProtectedOperations = vimProtectedOperations;
            _commandMarginUtil = commandMarginUtil;
            _vimApplicationSettings = vimApplicationSettings;
            _vsStatusbar = vsServiceProvider.GetService<SVsStatusbar, IVsStatusbar>();
            _timer = new DispatcherTimer(
                TimeSpan.FromSeconds(.2),
                DispatcherPriority.ApplicationIdle,
                OnTimer,
                Dispatcher.CurrentDispatcher);

            _timer.IsEnabled = !_vimApplicationSettings.UseEditorCommandMargin;
            _vimApplicationSettings.SettingsChanged += OnSettingsChanged;
        }

        private void OnTimer(object sender, EventArgs e)
        {
            try
            {
                var vimBuffer = _vim.FocusedBuffer.SomeOrDefault(null);
                var status = vimBuffer != null
                    ? _commandMarginUtil.GetStatus(vimBuffer)
                    : "";

                if (status.Length == 0)
                {
                    status = " ";
                }

                _vsStatusbar.SetText(status);
            }
            catch (Exception ex)
            {
                _vimProtectedOperations.Report(ex);
            }
        }

        private void OnSettingsChanged(object sender, ApplicationSettingsEventArgs e)
        {
            var useCommandMargin = _vimApplicationSettings.UseEditorCommandMargin;
            foreach (var vimBuffer in _vim.VimBuffers)
            {
                _commandMarginUtil.SetMarginVisibility(vimBuffer, useCommandMargin);
            }

            _timer.IsEnabled = !useCommandMargin;
        }

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            _vimProtectedOperations.BeginInvoke(
                () => _commandMarginUtil.SetMarginVisibility(vimBuffer, _vimApplicationSettings.UseEditorCommandMargin),
                DispatcherPriority.ApplicationIdle);

        }
    }
}
