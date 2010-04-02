using System;
using System.Collections.Generic;
using System.Linq;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;

namespace VsVim
{
    [Export(typeof(IOleServiceProvider))]
    internal sealed class OleServiceProvider : IOleServiceProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Lazy<IOleServiceProvider> _oleServiceProvider;

        [ImportingConstructor]
        internal OleServiceProvider(SVsServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _oleServiceProvider = new Lazy<IOleServiceProvider>(CreateServiceProvider);
        }

        private IOleServiceProvider CreateServiceProvider()
        {
            return (IOleServiceProvider)_serviceProvider.GetService(typeof(IOleServiceProvider));
        }
        
        public int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject)
        {
            return _oleServiceProvider.Value.QueryService(ref guidService, ref riid, out ppvObject);
        }
    }
}
