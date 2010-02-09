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
        private readonly IVsVimFactoryService _factory;
        private readonly KeyBindingService _keyBindingService;

        [ImportingConstructor]
        public HostFactory(
            IVsVimFactoryService factory,
            KeyBindingService keyBindingService)
        {
            _factory = factory;
            _keyBindingService = keyBindingService;
        }

        public void TextViewCreated(IWpfTextView textView)
        {
            var buffer = _factory.GetOrCreateBuffer(textView);

            var sp = _factory.GetOrUpdateServiceProvider(textView.TextBuffer);
            if (sp == null)
            {
                return;
            }

            // Run the key binding check now
            var dte = sp.GetService<SDTE, EnvDTE.DTE>();
            _keyBindingService.OneTimeCheckForConflictingKeyBindings(dte, buffer);
        }
    }

}
