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
using Microsoft.FSharp.Core;

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
        private readonly ITextEditorFactoryService _editorFactoryService;
        private readonly IVim _vim;

        [ImportingConstructor]
        public HostFactory(
            IVsVimFactoryService factory,
            IVimBufferFactory vimBufferFactory,
            IVim vim,
            ITextEditorFactoryService editorFactoryService,
            KeyBindingService keyBindingService)
        {
            _vim = vim;
            _vsVimFactory = factory;
            _vimBufferFactory = vimBufferFactory;
            _keyBindingService = keyBindingService;
            _editorFactoryService = editorFactoryService;
        }

        public void TextViewCreated(IWpfTextView textView)
        {
            var buffer = _vim.GetOrCreateBuffer(textView);
            var sp = _vsVimFactory.GetOrUpdateServiceProvider(textView.TextBuffer);
            if (sp == null)
            {
                return;
            }

            // Load the VimRC file if we haven't tried yet
            if (!_vim.IsVimRcLoaded && String.IsNullOrEmpty(_vim.Settings.VimRcPaths))
            {
                var func = FSharpFunc<Unit, ITextView>.FromConverter(_ => _editorFactoryService.CreateTextView());
                _vim.LoadVimRc(func);
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
