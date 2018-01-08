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

namespace Vim.VisualStudio.Specific
{
    [Export(typeof(ISharedServiceVersionFactory))]
    internal sealed class SharedServiceVersionFactory : ISharedServiceVersionFactory
    {
        internal ExportProvider ExportProvider { get; }
        internal IVsRunningDocumentTable VsRunningDocumentTable { get; }

        [ImportingConstructor]
        internal SharedServiceVersionFactory(SVsServiceProvider vsServiceProvider)
        {
            VsRunningDocumentTable = (IVsRunningDocumentTable)vsServiceProvider.GetService(typeof(SVsRunningDocumentTable));

            var componentModel = (IComponentModel)vsServiceProvider.GetService(typeof(SComponentModel));
            ExportProvider = componentModel.DefaultExportProvider;
        }

        #region ISharedServiceVersionFactory

        VisualStudioVersion ISharedServiceVersionFactory.Version
        {
            get { return VsSpecificConstants.VisualStudioVersion; }
        }

        ISharedService ISharedServiceVersionFactory.Create()
        {
            return new SharedService(ExportProvider, VsRunningDocumentTable);
        }

        #endregion
    }
}

