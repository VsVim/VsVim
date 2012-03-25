using System.ComponentModel.Composition;

#if DEV10
namespace VsVim.Dev10
#else
namespace VsVim.Dev11
#endif
{
    [Export(typeof(ISharedServiceVersionFactory))]
    internal sealed class SharedServiceVersionFactory : ISharedServiceVersionFactory
    {
        private readonly IVsAdapter _vsAdapter;

        [ImportingConstructor]
        internal SharedServiceVersionFactory(IVsAdapter vsAdapter)
        {
            _vsAdapter = vsAdapter;
        }

        #region ISharedServiceVersionFactory

        VisualStudioVersion ISharedServiceVersionFactory.Version
        {
#if DEV10
            get { return VisualStudioVersion.Dev10; }
#else
            get { return VisualStudioVersion.Dev11; }
#endif
        }

        ISharedService ISharedServiceVersionFactory.Create()
        {
            return new SharedService(_vsAdapter);
        }

        #endregion
    }
}
