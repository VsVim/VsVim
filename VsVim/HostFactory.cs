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
using System.Windows.Threading;

namespace VsVim
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Constants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class HostFactory : IWpfTextViewCreationListener
    {
        private readonly IVsVimFactoryService _vsVimFactory;
        private readonly KeyBindingService _keyBindingService;
        private readonly IVimBufferFactory _vimBufferFactory;
        private readonly IVim _vim;

        [ImportingConstructor]
        public HostFactory(
            IVsVimFactoryService factory,
            IVimBufferFactory vimBufferFactory,
            IVim vim,
            KeyBindingService keyBindingService)
        {
            _vim = vim;
            _vsVimFactory = factory;
            _vimBufferFactory = vimBufferFactory;
            _keyBindingService = keyBindingService;
        }

        public void TextViewCreated(IWpfTextView textView)
        {
            var buffer = _vim.GetOrCreateBuffer(textView);
            var sp = _vsVimFactory.GetOrUpdateServiceProvider(textView.TextBuffer);
            if (sp == null)
            {
                return;
            }

            var dte = sp.GetService<SDTE, EnvDTE.DTE>();
            Action doCheck = () =>
                {
                    // Run the key binding check now
                    _keyBindingService.OneTimeCheckForConflictingKeyBindings(dte, buffer);
                };

            Dispatcher.CurrentDispatcher.BeginInvoke(doCheck, null);
        }


    }

}
