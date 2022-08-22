using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Primitives;
using Microsoft.VisualStudio.Language.Intellisense.Utilities;
using System.Threading;
using Microsoft.VisualStudio.Text.Utilities;

namespace Vim.EditorHost.Implementation.Misc
{
    /// <summary>
    /// This interface is unconditionally imported by the core editor hence we must provide an export
    /// in order to enable testing. The implementation is meant to control A/B testing on features 
    /// in the editor. The only documentation on this interface is provided in the discussion 
    /// here: https://github.com/dotnet/roslyn/issues/27428
    /// </summary>
    [Export(typeof(IExperimentationServiceInternal))]
    internal sealed class BasicExperimentationServiceInternal : IExperimentationServiceInternal
    {
        bool IExperimentationServiceInternal.IsCachedFlightEnabled(string flightName)
        {
            return false;
        }
    }
}