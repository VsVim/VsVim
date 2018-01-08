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
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.VisualStudio.Specific
{
#if VS_SPECIFIC_2012 || VS_SPECIFIC_2013

    internal partial class SharedService
    {
        private void InitPeek() { }
        private bool ClosePeekView(ITextView textView) => false;
    }

#else

    internal partial class SharedService
    {
        private IPeekBroker _peekBroker;

        private void InitPeek()
        {
            _peekBroker = ExportProvider.GetExportedValue<IPeekBroker>();
        }

        private bool ClosePeekView(ITextView peekView)
        {
            if (peekView.TryGetPeekViewHostView(out var hostView))
            {
                _peekBroker.DismissPeekSession(hostView);
                return true;
            }

            return false;
        }
    }

#endif
}

