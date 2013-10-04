using System.ComponentModel.Composition;

namespace VsVim.Vs2012
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
            get { return VisualStudioVersion.Vs2012; }
        }

        ISharedService ISharedServiceVersionFactory.Create()
        {
            return new SharedService();
        }

        #endregion
    }
}
