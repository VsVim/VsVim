#if VSVIM_DEV_2019
using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Primitives;
using Microsoft.VisualStudio.Language.Intellisense.Utilities;
using System.Threading;
using Microsoft.VisualStudio.Text.Utilities;

namespace Vim.EditorHost.Implementation.Misc
{
    [Export(typeof(IExperimentationServiceInternal))]
    internal sealed class BasicExperimentationServiceInternal : IExperimentationServiceInternal
    {
        bool IExperimentationServiceInternal.IsCachedFlightEnabled(string flightName)
        {
            return false;
        }
    }
}
#elif VSVIM_DEV_2017
#else
#error Unsupported configuration
#endif