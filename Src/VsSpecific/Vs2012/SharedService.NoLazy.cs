// !!! Generated file. Do not edit directly !!!
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Platform.WindowManagement;
using Microsoft.VisualStudio.PlatformUI.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;

namespace Vim.VisualStudio.Vs2012
{
    internal partial class SharedService 
    {
        internal SharedService()
        {

        }

        private bool IsLazyLoaded(uint documentCookie)
        {
            return false;
        }
    }

    [Export(typeof(ISharedServiceVersionFactory))]
    internal sealed class SharedServiceVersionFactory : ISharedServiceVersionFactory
    {
        [ImportingConstructor]
        internal SharedServiceVersionFactory()
        {

        }

        #region ISharedServiceVersionFactory

        VisualStudioVersion ISharedServiceVersionFactory.Version
        {
            get { return VisualStudioVersion.Vs2012; }
        }

        ISharedService ISharedServiceVersionFactory.Create()
        {
            return new SharedService();
        }

        #endregion
    }
}

