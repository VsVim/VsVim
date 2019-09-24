using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Vim.UI.Cocoa;

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

        public KeyProcessor GetAssociatedProcessor(ICocoaTextView cocoaTextView)
        {
            var vimTextBuffer = _vim.GetOrCreateVimBuffer(cocoaTextView);
            return new VimKeyProcessor(vimTextBuffer, _keyUtil);
        }
    }
}
