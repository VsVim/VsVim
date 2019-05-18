using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Is this <see cref="_DTE"/> instance the one this assembly was compiled for?
        /// </summary>
        internal static bool IsTargetVisualStudio(_DTE dte) => TargetVisualStudioVersion == dte.GetVisualStudioVersion();

        /// <summary>
        /// Is this instance the one this assembly was compiled for?
        /// </summary>
        internal static bool IsTargetVisualStudio(SVsServiceProvider serviceProvider) =>
            IsTargetVisualStudio(serviceProvider.GetService<SDTE, _DTE>());
    }
}
