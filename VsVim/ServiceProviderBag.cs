using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;

namespace VsVim
{

    /// <summary>
    /// Implementation of the OLE IServiceProvider.  Needed to communicate adapted
    /// services back to the Visual Studio layer
    /// </summary>
    internal sealed class ServiceProviderBag : IOleServiceProvider
    {
        private readonly Dictionary<Guid, object> _map = new Dictionary<Guid, object>();
        private readonly IOleServiceProvider _serviceProvider;

        internal ServiceProviderBag(IOleServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Add<TService, TInterface>(TInterface service)
        {
            _map[typeof(TService).GUID] = service;
        }

        public int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject)
        {
            if (QueryServiceCore(ref guidService, ref riid, out ppvObject))
            {
                return VSConstants.S_OK;
            }

            return _serviceProvider.QueryService(ref guidService, ref riid, out ppvObject);
        }

        private bool QueryServiceCore(ref Guid guidService, ref Guid riid, out IntPtr ptr)
        {
            ptr = IntPtr.Zero;
            object service;
            if (!_map.TryGetValue(guidService, out service))
            {
                return false;
            }

            var servicePtr = IntPtr.Zero;
            try
            {
                servicePtr = Marshal.GetIUnknownForObject(service);
                if (servicePtr != IntPtr.Zero)
                {
                    return VSConstants.S_OK == Marshal.QueryInterface(servicePtr, ref riid, out ptr);
                }
                return false;
            }
            finally
            {
                if (servicePtr != IntPtr.Zero)
                {
                    Marshal.Release(servicePtr);
                }
            }
        }
    }
}
