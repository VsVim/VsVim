using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Editor;
using System.Diagnostics;
using System.Collections.Generic;
using Vim;
using Microsoft.VisualStudio.UI.Undo;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;

namespace VsVim
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Constants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class HostFactory : IWpfTextViewCreationListener
    {
        [Import]
        private IVsVimFactoryService _factory = null;
        [Import]
        private KeyBindingService _keyBindingService = null;
        [Import]
        private IVsEditorAdaptersFactoryService _adaptersFactory = null;

        public HostFactory()
        {
        }

        public void TextViewCreated(IWpfTextView textView)
        {
            var vsTextLines = _adaptersFactory.GetBufferAdapter(textView.TextBuffer) as IVsTextLines;
            if (vsTextLines == null)
            {
                return;
            }

            var objectWithSite = vsTextLines as IObjectWithSite;
            if (objectWithSite == null)
            {
                return;
            }

            var sp = objectWithSite.GetServiceProvider();
            var buffer = _factory.GetOrCreateBuffer(textView);

            // Run the key binding check now
            var dte = sp.GetService<SDTE, EnvDTE.DTE>();
            _keyBindingService.OneTimeCheckForConflictingKeyBindings(dte, buffer);
        }
    }

}
