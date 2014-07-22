using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Vim.VisualStudio.Implementation.SharedService
{
    [Export(typeof(ISharedServiceFactory))]
    internal sealed class SharedServiceFactory : ISharedServiceFactory
    {
        private ISharedServiceVersionFactory _factory;

        [ImportingConstructor]
        internal SharedServiceFactory(
            SVsServiceProvider serviceProvider,
            [ImportMany]IEnumerable<ISharedServiceVersionFactory> factories)
        {
            var dte = serviceProvider.GetService<SDTE, _DTE>();
            var version = dte.GetVisualStudioVersion();
            var factory = factories.FirstOrDefault(x => x.Version == version);
            if (factory == null)
            {
                factory = new DefaultSharedServiceFactory();
            }

            _factory = factory;
        }

        #region ISharedServiceFactory

        ISharedService ISharedServiceFactory.Create()
        {
            return _factory.Create();
        }

        #endregion
    }
}
