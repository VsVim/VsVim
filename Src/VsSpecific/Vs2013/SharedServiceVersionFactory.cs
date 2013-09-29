using System.ComponentModel.Composition;

namespace VsVim.Vs2013
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
            get { return VisualStudioVersion.Vs2013; }
        }

        ISharedService ISharedServiceVersionFactory.Create()
        {
            return new SharedService();
        }

        #endregion
    }
}
