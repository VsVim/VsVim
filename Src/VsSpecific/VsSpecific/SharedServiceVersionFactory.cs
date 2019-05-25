using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Platform.WindowManagement;
using Microsoft.VisualStudio.PlatformUI.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.ComponentModelHost;
using System.ComponentModel.Composition.Hosting;
using Vim.VisualStudio.Specific.Implementation.WordCompletion;
using System.Collections;

namespace Vim.VisualStudio.Specific
{
    [Export(typeof(ISharedServiceVersionFactory))]
    internal sealed class SharedServiceVersionFactory : ISharedServiceVersionFactory
    {
        internal SVsServiceProvider VsServiceProvider { get; }

        [ImportingConstructor]
        internal SharedServiceVersionFactory(SVsServiceProvider vsServiceProvider)
        {
            VsServiceProvider = vsServiceProvider;
        }

        #region ISharedServiceVersionFactory

        VisualStudioVersion ISharedServiceVersionFactory.Version
        {
            get { return VimSpecificUtil.TargetVisualStudioVersion; }
        }

        ISharedService ISharedServiceVersionFactory.Create()
        {
            return new SharedService(VsServiceProvider);
        }

        #endregion
    }
}

