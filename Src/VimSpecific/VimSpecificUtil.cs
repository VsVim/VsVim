using System;

namespace Vim.VisualStudio.Specific
{
    internal static class VimSpecificUtil
    {
#if VIM_SPECIFIC_TEST_HOST
        internal const string HostIdentifier = "VsVim Test Host ";
#else

#if VS_SPECIFIC_2015
        internal const string HostIdentifier = "VsVim 2015";
        internal const VisualStudioVersion VisualStudioVersion = VisualStudioVersion.Vs2015;
#elif VS_SPECIFIC_2017
        internal const string HostIdentifier = "VsVim 2017";
        internal const VisualStudioVersion VisualStudioVersion = VisualStudioVersion.Vs2017;
#elif VS_SPECIFIC_2019
        internal const string HostIdentifier = "VsVim 2019";
        internal const VisualStudioVersion VisualStudioVersion = VisualStudioVersion.Vs2019;
#else
#error Unsupported configuration
#endif

        internal static bool IsTargetVisualStudio(SVsServiceProvider vsServiceProvider)
        {
            var dte = vsServiceProvider.GetService<SDTE, _DTE>();
            return dte.GetVisualStudioVersion() == TargetVisualStudioVersion;
        }
#endif
        internal const string MefNamePrefix = HostIdentifier + " ";
    }

    internal abstract class VimSpecificService : IVimSpecificService
    {
        private readonly Lazy<IVimHost> _vimHost;

        internal bool InsideValidHost => _vimHost.Value.HostIdentifier == VimSpecificUtil.HostIdentifier;

        protected VimSpecificService(Lazy<IVimHost> vimHost)
        {
            _vimHost = vimHost;
        }

        string IVimSpecificService.HostIdentifier => VimSpecificUtil.HostIdentifier;
    }
}
