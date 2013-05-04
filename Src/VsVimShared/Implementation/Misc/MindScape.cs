using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Vim;

namespace VsVim.Implementation.Misc
{
    /// <summary>
    /// This type is responsible for working around a couple of conflicts that VsVim has with the
    /// MindScape plugin.  The main conflict is MindScape listens directly to the Wpf KeyDown event
    /// for determining if intellisense should be displayed.  It doesn't see if the event makes it 
    /// to the actual buffer, just that the key was pressed.  In normal mode this causes a conflict
    /// as it causes intellisense to be displayed during caret navigation commands.  Need to work
    /// around that behavior
    /// </summary>
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class MindScape : IVimBufferCreationListener
    {
        internal static readonly Guid MindScapePackageGuid = new Guid("29C7CD30-2759-428A-895D-50E2B6A8487F");

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

            // Restrict this fix to the file types in which it occurs
            var contentType = vimBuffer.TextBuffer.ContentType;
            if (!contentType.IsOfType("scss") && !contentType.IsOfType("less"))
            {
                return;
            }

            vimBuffer.KeyInputProcessed += (sender, e) => OnKeyInputProcessed(vimBuffer, e);
        }

        #endregion
    }
}
