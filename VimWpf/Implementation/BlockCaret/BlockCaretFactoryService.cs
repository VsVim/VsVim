using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation.BlockCaret
{
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class BlockCaretFactoryService : IVimBufferCreationListener
    {
        internal const string BlockCaretAdornmentLayerName = "BlockCaretAdornmentLayer";

        private readonly IEditorFormatMapService _formatMapService;
        private readonly IProtectedOperations _protectedOperations;

#pragma warning disable 169
        [Export(typeof(AdornmentLayerDefinition))]
        [Name(BlockCaretAdornmentLayerName)]
        [Order(After = PredefinedAdornmentLayers.Selection)]
        private AdornmentLayerDefinition _blockCaretAdornmentLayerDefinition;
#pragma warning restore 169

        [ImportingConstructor]
        internal BlockCaretFactoryService(IEditorFormatMapService formatMapService, IProtectedOperations protectedOperations)
        {
            _formatMapService = formatMapService;
            _protectedOperations = protectedOperations;
        }

        private IBlockCaret CreateBlockCaret(IWpfTextView textView)
        {
            var formatMap = _formatMapService.GetEditorFormatMap(textView);
            return new BlockCaret(textView, BlockCaretAdornmentLayerName, formatMap, _protectedOperations);
        }

        #region IVimBufferCreationListener

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            var textView = vimBuffer.TextView as IWpfTextView;
            if (textView == null)
            {
                return;
            }

            // Setup the block caret 
            var caret = CreateBlockCaret(textView);
            var caretController = new BlockCaretController(vimBuffer, caret);
        }

        #endregion
    }
}
