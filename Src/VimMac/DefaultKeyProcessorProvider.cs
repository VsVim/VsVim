using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Vim.UI.Cocoa;

namespace VimHost
{
    [Export(typeof(IKeyProcessorProvider))]
    [ContentType(VimConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    [Name(VimConstants.MainKeyProcessorName)]
    [Order(After = "CtrlClickGoToDefKeyProcessor")]
    internal sealed class DefaultKeyProcessorProvider : IKeyProcessorProvider
    {
        private readonly IVim _vim;
        private readonly IKeyUtil _keyUtil;
        private readonly ICompletionBroker _completionBroker;

        [ImportingConstructor]
        internal DefaultKeyProcessorProvider(IVim vim, IKeyUtil keyUtil, ICompletionBroker completionBroker)
        {
            _vim = vim;
            _keyUtil = keyUtil;
            _completionBroker = completionBroker;
        }

        public KeyProcessor GetAssociatedProcessor(ICocoaTextView cocoaTextView)
        {
            var vimTextBuffer = _vim.GetOrCreateVimBuffer(cocoaTextView);
            return new VimKeyProcessor(vimTextBuffer, _keyUtil, _completionBroker);
        }
    }
}
