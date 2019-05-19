using System;
using System.ComponentModel.Composition.Hosting;
using System.Collections.Generic;

namespace Vim.VisualStudio.Specific
{
    internal static class VimSpecificUtil
    {
#if VIM_SPECIFIC_TEST_HOST
        internal const string HostIdentifier = "VsVim Test Host ";
#else

#if VS_SPECIFIC_2015
        internal const string HostIdentifier = VisualStudioVersionUtil.HostIdentifier2015;
        internal const VisualStudioVersion TargetVisualStudioVersion = VisualStudioVersion.Vs2015;
#elif VS_SPECIFIC_2017
        internal const string HostIdentifier = VisualStudioVersionUtil.HostIdentifier2017;
        internal const VisualStudioVersion TargetVisualStudioVersion = VisualStudioVersion.Vs2017;
#elif VS_SPECIFIC_2019
        internal const string HostIdentifier = VisualStudioVersionUtil.HostIdentifier2019;
        internal const VisualStudioVersion TargetVisualStudioVersion = VisualStudioVersion.Vs2019;
#else
#error Unsupported configuration
#endif
#endif
        internal const string MefNamePrefix = HostIdentifier + " ";

        internal static TypeCatalog GetTypeCatalog()
        {
            var list = new List<Type>()
            {
#if VS_SPECIFIC_2019
                typeof(Implementation.WordCompletion.Async.WordAsyncCompletionSourceProvider),
#endif
                typeof(Implementation.WordCompletion.Legacy.WordLegacyCompletionPresenterProvider),
                typeof(Implementation.WordCompletion.Legacy.WordLegacyCompletionSourceProvider),
                typeof(Implementation.WordCompletion.VimWordCompletionUtil),
            };

            return new TypeCatalog(list);
        }
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
