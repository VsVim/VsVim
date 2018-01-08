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

namespace Vim.VisualStudio.Specific
{
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
            get { return VsSpecificConstants.VisualStudioVersion; }
        }

        ISharedService ISharedServiceVersionFactory.Create()
        {
            return new SharedService(_vsRunningDocumentTable);
        }

        #endregion
    }
}

