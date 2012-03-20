using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace VsVim.Implementation
{
    [Export(typeof(ISharedServiceFactory))]
    internal sealed class SharedServiceFactory : ISharedServiceFactory
    {
        private ISharedServiceVersionFactory _factory;

        [ImportingConstructor]
        internal SharedServiceFactory([ImportMany]IEnumerable<ISharedServiceVersionFactory> factories)
        {
            var version = CalculateVisualStudioVersion();
            var factory = factories.FirstOrDefault(x => x.Version == version);
            if (factory == null)
            {
                factory = new DefaultSharedServiceFactory();
            }

            _factory = factory;
        }

        /// <summary>
        /// Todo: Find the appropriate way to do this
        /// </summary>
        VisualStudioVersion CalculateVisualStudioVersion()
        {
            return VisualStudioVersion.Dev10;
        }

        #region ISharedServiceFactory

        ISharedService ISharedServiceFactory.Create()
        {
            return _factory.Create();
        }

        #endregion
    }
}
