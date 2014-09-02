using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.UI.Wpf;
using Vim;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

namespace VimHost
{
    [Export(typeof(IKeyProcessorProvider))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    [Name(VimConstants.MainKeyProcessorName)]
    internal sealed class DefaultKeyProcessorProvider : IKeyProcessorProvider
    {
        private readonly IVim _vim;
        private readonly IKeyUtil _keyUtil;

        [ImportingConstructor]
        internal DefaultKeyProcessorProvider(IVim vim, IKeyUtil keyUtil)
        {
            _vim = vim;
            _keyUtil = keyUtil;
        }

        public KeyProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            var vimTextBuffer = _vim.GetOrCreateVimBuffer(wpfTextView);
            return new VimKeyProcessor(vimTextBuffer, _keyUtil);
        }
    }
}
