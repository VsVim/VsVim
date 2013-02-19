using System.ComponentModel.Composition;

namespace VsVim.Vs2010
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
            get { return VisualStudioVersion.Vs2010; }
        }

        ISharedService ISharedServiceVersionFactory.Create()
        {
            return new SharedService(_vsAdapter);
        }

        #endregion
    }
}
