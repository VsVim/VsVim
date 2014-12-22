using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Platform.WindowManagement;
using Microsoft.VisualStudio.PlatformUI.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;

namespace Vim.VisualStudio.Vs2013
{
    internal partial class SharedService 
    {
        internal SharedService()
        {

        }

        private bool IsLazyLoaded(uint documentCookie)
        {
            return false;
        }
    }
}

