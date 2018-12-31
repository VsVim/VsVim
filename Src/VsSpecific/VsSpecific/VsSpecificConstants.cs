using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vim.VisualStudio.Specific
{
    internal static class VsSpecificConstants
    {
        internal static VisualStudioVersion VisualStudioVersion =>
#if VS_SPECIFIC_2015
    VisualStudioVersion.Vs2015;
#elif VS_SPECIFIC_2017
    VisualStudioVersion.Vs2017;
#elif VS_SPECIFIC_2019
    VisualStudioVersion.Vs2019;
#else
#error "Bad VsSpecific project"
#endif
    }
}
