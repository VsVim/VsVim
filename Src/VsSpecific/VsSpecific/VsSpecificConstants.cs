﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vim.VisualStudio.Specific
{
    internal static class VsSpecificConstants
    {
        internal static VisualStudioVersion VisualStudioVersion =>
#if VS_SPECIFIC_2012
    VisualStudioVersion.Vs2012;
#elif VS_SPECIFIC_2013
    VisualStudioVersion.Vs2013;
#elif VS_SPECIFIC_2015
    VisualStudioVersion.Vs2015;
#elif VS_SPECIFIC_2017
    VisualStudioVersion.Vs2017;
#else
#error "Bad VsSpecific project"
#endif
    }
}
