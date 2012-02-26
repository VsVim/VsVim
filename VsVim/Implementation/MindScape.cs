using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Vim;

namespace VsVim.Implementation
{
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class MindScape : IVimBufferCreationListener
    {
        private static readonly Guid MindScapePackageGuid = new Guid("29C7CD30-2759-428A-895D-50E2B6A8487F");

        private readonly bool _isInstalled;
        private readonly ICompletionBroker _completionBroker;

        [ImportingConstructor]
        internal MindScape(SVsServiceProvider serviceProvider, ICompletionBroker completionBroker)
        {
            var vsShell = serviceProvider.GetService<SVsShell, IVsShell>();
            _isInstalled = vsShell.IsPackageInstalled(MindScapePackageGuid);
            _completionBroker = completionBroker;
        }

        private void OnKeyInputProcessed(IVimBuffer vimBuffer, KeyInputProcessedEventArgs e)
        {
            if (_completionBroker.IsCompletionActive(vimBuffer.TextView))
            {
                // If the buffer is not in insert mode or it just transitioned into insert mode
                // then don't let intellisense stay active.  Else intellisense will be popping
                // up when normal mode keys are used to navigate around the buffer
                if (vimBuffer.ModeKind != ModeKind.Insert || e.ProcessResult.IsAnySwitch)
                {
                    _completionBroker.DismissAllSessions(vimBuffer.TextView);
                }
            }
        }

        #region IVimBufferCreationListener

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            if (!_isInstalled)
            {
                return;
            }

            vimBuffer.KeyInputProcessed += (sender, e) => OnKeyInputProcessed(vimBuffer, e);
        }

        #endregion
    }
}
