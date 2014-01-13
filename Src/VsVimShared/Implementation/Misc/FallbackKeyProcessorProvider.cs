﻿using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Vim.UI.Wpf;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Vim;

namespace VsVim.Implementation.Misc
{
    /// <summary>
    /// The fallback key processor provider inserts a processor inbetween
    /// VsVim's key processor and Visual Studio's key processor.  It also
    /// applies to any interactive non-VsVim text views.  The key processor
    /// handles keys we had to unbind due to to conflicts.
    /// 
    /// This allows those keys to continue to work in other Visual Studio
    /// windows, like the output window and the immediate window.  It also
    /// allows the keys to "work again" whenever VsVim disables itself
    /// temporarily.
    /// </summary>
    [Export(typeof(FallbackKeyProcessorProvider))]
    [Export(typeof(IKeyProcessorProvider))]
    [Order(After = Constants.VsKeyProcessorName)]
    [Order(Before = Constants.VisualStudioKeyProcessorName)]
    [Name(Constants.FallbackKeyProcessorName)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    [ContentType(Vim.Constants.AnyContentType)]
    internal sealed class FallbackKeyProcessorProvider : IKeyProcessorProvider
    {
        private readonly IKeyUtil _keyUtil;
        private readonly _DTE _dte;
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly IVim _vim;
        private FallbackKeyProcessor _keyProcessor;

        [ImportingConstructor]
        internal FallbackKeyProcessorProvider(SVsServiceProvider serviceProvider, IKeyUtil keyUtil, IVimApplicationSettings vimApplicationSettings, IVim vim)
        {
            _dte = (_DTE)serviceProvider.GetService(typeof(_DTE));
            _keyUtil = keyUtil;
            _vimApplicationSettings = vimApplicationSettings;
            _vim = vim;
        }

        /// <summary>
        /// We create a single fallback key processor on demand and reuse it
        /// for all text views
        /// </summary>
        KeyProcessor IKeyProcessorProvider.GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            IVimBuffer vimBuffer;
            if (_vim.TryGetOrCreateVimBufferForHost(wpfTextView, out vimBuffer))
            {
                return null;
            }
            return GetOrCreateFallbackProcessor(wpfTextView);
        }

        internal FallbackKeyProcessor GetOrCreateFallbackProcessor(IWpfTextView wpfTextView)
        {
            if (_keyProcessor == null)
            {
                _keyProcessor = new FallbackKeyProcessor(_dte, _keyUtil, _vimApplicationSettings);
            }
            return _keyProcessor;
        }
    }
}