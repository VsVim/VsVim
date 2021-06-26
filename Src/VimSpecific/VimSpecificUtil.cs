using System;
using System.ComponentModel.Composition.Hosting;
using System.Collections.Generic;

namespace Vim.VisualStudio.Specific
{
    // TODO_SHARED this type needs to be re-thought a lot
    internal static class VimSpecificUtil
    {
#if VS_SPECIFIC_2015 || VS_SPECIFIC_2017
        internal static bool HasAsyncCompletion => false;
#elif VS_SPECIFIC_2019 || VS_SPECIFIC_MAC
        internal static bool HasAsyncCompletion => true;
#else
#error Unsupported configuration
#endif
        internal static bool HasLegacyCompletion => !HasAsyncCompletion;

// TODO_SHARED this entire constant may not be needed
#if VIM_SPECIFIC_TEST_HOST
        internal const string HostIdentifier = HostIdentifiers.TestHost;
#else

#if VS_SPECIFIC_2015
        internal const string HostIdentifier = HostIdentifiers.VisualStudio2015;
        internal const VisualStudioVersion TargetVisualStudioVersion = VisualStudioVersion.Vs2015;
#elif VS_SPECIFIC_2017
        internal const string HostIdentifier = HostIdentifiers.VisualStudio2017;
        internal const VisualStudioVersion TargetVisualStudioVersion = VisualStudioVersion.Vs2017;
#elif VS_SPECIFIC_2019
        internal const string HostIdentifier = HostIdentifiers.VisualStudio2019;
        internal const VisualStudioVersion TargetVisualStudioVersion = VisualStudioVersion.Vs2019;
#elif VS_SPECIFIC_MAC
        internal const string HostIdentifier = HostIdentifiers.VisualStudioMac;
#else
#error Unsupported configuration
#endif
#endif
        internal const string MefNamePrefix = HostIdentifier + " ";

        // TODO_SHARED: this should just move to EditorHostFactory as that is the place where we 
        // piecemeal together catalogs.
        internal static TypeCatalog GetTypeCatalog()
        {
            var list = new List<Type>()
            {
#if VS_SPECIFIC_2019
                typeof(Implementation.WordCompletion.Async.WordAsyncCompletionSourceProvider),
#elif !VS_SPECIFIC_MAC
                typeof(Implementation.WordCompletion.Legacy.WordLegacyCompletionPresenterProvider),
#endif
                typeof(Implementation.WordCompletion.Legacy.WordLegacyCompletionSourceProvider),
                typeof(Implementation.WordCompletion.VimWordCompletionUtil),
#if VS_SPECIFIC_2015 || VS_SPECIFIC_2017
#else
                typeof(global::Vim.Specific.Implementation.MultiSelection.MultiSelectionUtilFactory),
#endif
            };

            return new TypeCatalog(list);
        }
    }
}
