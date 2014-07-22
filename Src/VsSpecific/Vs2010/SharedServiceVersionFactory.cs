using System.ComponentModel.Composition;

namespace Vim.VisualStudio.Vs2010
{
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
            get { return VisualStudioVersion.Vs2010; }
        }

        ISharedService ISharedServiceVersionFactory.Create()
        {
            return new SharedService();
        }

        #endregion
    }
}
