// !!! Generated file. Do not edit directly !!!
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Platform.WindowManagement;
using Microsoft.VisualStudio.PlatformUI.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;

namespace Vim.VisualStudio.Vs2013
{
    internal partial class SharedService 
    {
        private readonly IVsRunningDocumentTable _vsRunningDocumentTable;

        internal SharedService(IVsRunningDocumentTable vsRunningDocumentTable)
        {
            _vsRunningDocumentTable = vsRunningDocumentTable;
        }

        private bool IsLazyLoaded(uint documentCookie)
        {
            try
            {
                var rdt = (IVsRunningDocumentTable4)_vsRunningDocumentTable;
                var flags = (_VSRDTFLAGS4)rdt.GetDocumentFlags(documentCookie);
                return 0 != (flags & _VSRDTFLAGS4.RDT_PendingInitialization);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    [Export(typeof(ISharedServiceVersionFactory))]
    internal sealed class SharedServiceVersionFactory : ISharedServiceVersionFactory
    {
        private readonly IVsRunningDocumentTable _vsRunningDocumentTable;

        [ImportingConstructor]
        internal SharedServiceVersionFactory(SVsServiceProvider vsServiceProvider)
        {
            _vsRunningDocumentTable = (IVsRunningDocumentTable)vsServiceProvider.GetService(typeof(SVsRunningDocumentTable));
        }

        #region ISharedServiceVersionFactory

        VisualStudioVersion ISharedServiceVersionFactory.Version
        {
            get { return VisualStudioVersion.Vs2013; }
        }

        ISharedService ISharedServiceVersionFactory.Create()
        {
            return new SharedService(_vsRunningDocumentTable);
        }

        #endregion
    }
}

