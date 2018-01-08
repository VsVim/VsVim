using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Platform.WindowManagement;
using Microsoft.VisualStudio.PlatformUI.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Vim.VisualStudio.Specific
{
    internal partial class SharedService 
    {
        private bool IsLazyLoaded(uint documentCookie)
        {
#if VS_SPECIFIC_2012
            return false;
#else
            try
            {
                var rdt = (IVsRunningDocumentTable4)VsRunningDocumentTable;
                var flags = (_VSRDTFLAGS4)rdt.GetDocumentFlags(documentCookie);
                return 0 != (flags & _VSRDTFLAGS4.RDT_PendingInitialization);
            }
            catch (Exception)
            {
                return false;
            }
#endif
        }
    }
}

