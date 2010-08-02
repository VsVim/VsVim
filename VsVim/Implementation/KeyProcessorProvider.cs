using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Vim;

namespace VsVim.Implementation
{
    [Export(typeof(IKeyProcessorProvider))]
    [Order(Before = "VisualStudioKeyProcessor")]
    [Name("VsVim")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [ContentType(Vim.Constants.ContentType)]
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
