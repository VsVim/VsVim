using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using EnvDTE;

namespace VsVim.Implementation
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
            var version = CalculateVisualStudioVersion(dte);
            var factory = factories.FirstOrDefault(x => x.Version == version);
            if (factory == null)
            {
                factory = new DefaultSharedServiceFactory();
            }

            _factory = factory;
        }

        private static VisualStudioVersion CalculateVisualStudioVersion(_DTE dte)
        {
            var version = dte.Version;
            if (string.IsNullOrEmpty(dte.Version))
            {
                return VisualStudioVersion.Unknown;
            }

            var parts = version.Split('.');
            if (parts.Length == 0)
            {
                return VisualStudioVersion.Unknown;
            }

            switch (parts[0])
            {
                case "10":
                    return VisualStudioVersion.Dev10;
                case "11":
                    return VisualStudioVersion.Dev11;
                default:
                    return VisualStudioVersion.Unknown;
            }
        }

        #region ISharedServiceFactory

        ISharedService ISharedServiceFactory.Create()
        {
            return _factory.Create();
        }

        #endregion
    }
}
