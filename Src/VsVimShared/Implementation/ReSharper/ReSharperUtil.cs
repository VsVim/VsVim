using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Vim;
using System.Diagnostics;

namespace Vim.VisualStudio.Implementation.ReSharper
{
    [Export(typeof(IReSharperUtil))]
    internal sealed class ReSharperUtil : IReSharperUtil
    {
        private static readonly Guid s_resharper5Guid = new Guid("0C6E6407-13FC-4878-869A-C8B4016C57FE");

        private readonly bool _isResharperInstalled;

        [ImportingConstructor]
        internal ReSharperUtil(SVsServiceProvider serviceProvider, IWordCompletionSessionFactoryService wordCompletionSessionFactory)
        {
            var vsShell = serviceProvider.GetService<SVsShell, IVsShell>();
            _isResharperInstalled = vsShell.IsPackageInstalled(s_resharper5Guid);
            if (_isResharperInstalled)
            {
                wordCompletionSessionFactory.Created += OnWordCompletionSessionCreated;
            }
        }

        internal ReSharperUtil(bool isResharperInstalled)
        {
            _isResharperInstalled = isResharperInstalled;
        }

        private void OnWordCompletionSessionCreated(object sender, WordCompletionSessionEventArgs e)
        {
            // This prevents R# from dismissing any word completion sessions VsVim creates.  
            e.WordCompletionSession.Properties[s_resharper5Guid] = this;
        }

        bool IReSharperUtil.IsInstalled
        {
            get { return _isResharperInstalled; }
        }
    }
}
