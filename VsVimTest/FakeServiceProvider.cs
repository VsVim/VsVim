using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;

namespace VsVimTest
{
    internal sealed class FakeServiceProvider :  Microsoft.VisualStudio.OLE.Interop.IServiceProvider 
    {
        private readonly Dictionary<Tuple<Guid, Guid>, object> _map = new Dictionary<Tuple<Guid, Guid>, object>();

        internal FakeServiceProvider(params Tuple<Type, Type, object>[] tuples)
        {
            foreach (var tuple in tuples)
            {
                Add(tuple.Item1,tuple.Item2,tuple.Item3);
            }
        }

        internal void Add(Type serviceType, Type interfaceType, object obj)
        {
            var key = Tuple.Create(serviceType.GUID, interfaceType.GUID);
            _map.Add(key, obj);
        }

        int Microsoft.VisualStudio.OLE.Interop.IServiceProvider.QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject)
        {
            var key = Tuple.Create(guidService, riid);
            object value;
            if (!_map.TryGetValue(key, out value))
            {
                ppvObject = IntPtr.Zero;
                return VSConstants.E_FAIL;
            }

            ppvObject = Marshal.GetIUnknownForObject(value);
            return VSConstants.S_OK;
        }
    }
}
