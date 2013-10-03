using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.ComponentModel.Composition;

namespace VsVim.Vs2013
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
            get { return VisualStudioVersion.Vs2013; }
        }

        ISharedService ISharedServiceVersionFactory.Create()
        {
            return new SharedService(_vsRunningDocumentTable);
        }

        #endregion
    }
}
