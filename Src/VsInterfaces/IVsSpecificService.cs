using EnvDTE;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Vim.Interpreter;

namespace Vim.VisualStudio
{
    /// <summary>
    /// This is a marker interface that allows us to do MEF composition logic inside the VsSpecific 
    /// layer. Need a single interface (same AQN) for them to export / import.
    /// </summary>
    public interface IVsSpecificService
    {
        VisualStudioVersion VisualStudioVersion { get; }
    }

    public sealed class VsSpecificServiceHost
    {
        public SVsServiceProvider VsServiceProvider { get; }
        public IEnumerable<Lazy<IVsSpecificService>> VsSpecificServices { get; }

        public VsSpecificServiceHost(SVsServiceProvider vsServiceProvider, IEnumerable<Lazy<IVsSpecificService>> vsSpecificServices)
        {
            VsServiceProvider = vsServiceProvider;
            VsSpecificServices = vsSpecificServices;
        }

        public T GetService<T>()
        {
            if (!TryGetService<T>(out T t))
            {
                throw new Exception($"Service of type {typeof(T)} is not available");
            }

            return t;
        }

        public bool TryGetService<T>(out T service)
        {
            var dte = (_DTE)VsServiceProvider.GetService(typeof(SDTE));
            var version = dte.GetVisualStudioVersion();
            foreach (var s in VsSpecificServices)
            {
                try
                {
                    var current = s.Value;
                    if (version == current.VisualStudioVersion &&
                        current is T t)
                    {
                        service = t;
                        return true;
                    }
                }
                catch
                {
                    // Possible for service to load fail when in the wrong VS version
                }
            }

            service = default;
            return false;
        }
    }
}
