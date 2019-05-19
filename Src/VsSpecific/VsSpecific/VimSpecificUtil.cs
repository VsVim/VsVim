using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vim.VisualStudio.Specific
{
    internal static class VimSpecificUtil
    {
        /// <summary>
        /// The Visual Studio Version this library was compiled for
        /// </summary>
        internal const VisualStudioVersion TargetVisualStudioVersion =
#if VS_SPECIFIC_2015
    VisualStudioVersion.Vs2015;
#elif VS_SPECIFIC_2017
    VisualStudioVersion.Vs2017;
#elif VS_SPECIFIC_2019
    VisualStudioVersion.Vs2019;
#else
#error Unsupported configuration
#endif

        /// <summary>
        /// The prefix to put on a MEF singleton interface to ensure that singletons between
        /// different VsSpecific assemblies don't collide
        /// </summary>
        internal const string MefNamePrefix =
#if VS_SPECIFIC_2015
    "VsVim.Vs2015 ";
#elif VS_SPECIFIC_2017
    "VsVim.Vs2017 ";
#elif VS_SPECIFIC_2019
    "VsVim.Vs2019 ";
#else
#error Unsupported configuration
#endif

        internal static bool IsTargetVisualStudio(SVsServiceProvider vsServiceProvider)
        {
            var dte = vsServiceProvider.GetService<SDTE, _DTE>();
            return dte.GetVisualStudioVersion() == TargetVisualStudioVersion;
        }
    }

    internal abstract class VsSpecificService : IVsSpecificService
    {
        VisualStudioVersion IVsSpecificService.VisualStudioVersion => VimSpecificUtil.TargetVisualStudioVersion;
    }
}
