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
        private readonly DispatcherTimer _timer;

        [ImportingConstructor]
        internal StatusBarAdapter(IVim vim, IVimProtectedOperations vimProtectedOperations, ICommandMarginUtil commandMarginUtil, SVsServiceProvider vsServiceProvider)
        {
            _vim = vim;
            _vimProtectedOperations = vimProtectedOperations;
            _commandMarginUtil = commandMarginUtil;
            _vsStatusbar = vsServiceProvider.GetService<SVsStatusbar, IVsStatusbar>();
            _timer = new DispatcherTimer(
                TimeSpan.FromSeconds(.2),
                DispatcherPriority.ApplicationIdle,
                OnTimer,
                Dispatcher.CurrentDispatcher);
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

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            // Intentionally doing nothing here.  This interface is [Export] simply to ensure it is created
            // when at least a single IVimBuffer is around.  
        }
    }
}
