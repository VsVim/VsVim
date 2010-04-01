using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;
using Vim;
using Microsoft.VisualStudio.Utilities;

namespace VsVim
{
    [Export(typeof(IKeyProcessorProvider))]
    [Order(Before = "VisualStudioKeyProcessor")]
    [Name("VsVim")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [ContentType(Constants.ContentType)]
    public sealed class KeyProcessorProvider : IKeyProcessorProvider
    {
        private readonly IVim _vim;

        [ImportingConstructor]
        public KeyProcessorProvider(IVim vim)
        {
            _vim = vim;
        }

        public KeyProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            var buffer = _vim.GetOrCreateBuffer(wpfTextView);
            return new Vim.UI.Wpf.KeyProcessor(buffer);
        }
    }
}
