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
#if VS_SPECIFIC_2012

    internal partial class SharedService 
    {
        private void InitLazy() { } 
        private bool IsLazyLoaded(uint documentCookie) => false;
    }

#else

    internal partial class SharedService 
    {
        private IVsRunningDocumentTable4 _runningDocumentTable;

        private void InitLazy()
        {
            _runningDocumentTable = (IVsRunningDocumentTable4)VsServiceProvider.GetService(typeof(SVsRunningDocumentTable));
        }

        private bool IsLazyLoaded(uint documentCookie)
        {
            try
            {
                var flags = (_VSRDTFLAGS4)_runningDocumentTable.GetDocumentFlags(documentCookie);
                return 0 != (flags & _VSRDTFLAGS4.RDT_PendingInitialization);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

#endif
}

