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
#else
            default;
#error "Bad VsSpecific project"
#endif
    }
}
