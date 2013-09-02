using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using EditorUtils;

namespace Vim.UI.Wpf.Implementation.BlockCaret
{
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class BlockCaretFactoryService : IVimBufferCreationListener
    {
        internal const string BlockCaretAdornmentLayerName = "BlockCaretAdornmentLayer";

        private readonly IClassificationFormatMapService _classificationFormatMapService;
        private readonly IEditorFormatMapService _formatMapService;
        private readonly IProtectedOperations _protectedOperations;
        private readonly IControlCharUtil _controlCharUtil;

#pragma warning disable 169
        [Export(typeof(AdornmentLayerDefinition))]
        [Name(BlockCaretAdornmentLayerName)]
        [Order(After = PredefinedAdornmentLayers.Selection)]
        private AdornmentLayerDefinition _blockCaretAdornmentLayerDefinition;
#pragma warning restore 169

        [ImportingConstructor]
        internal BlockCaretFactoryService(IClassificationFormatMapService classificationFormatMapService, IEditorFormatMapService formatMapService, IControlCharUtil controlCharUtil, IVimProtectedOperations protectedOperations)
        {
            _classificationFormatMapService = classificationFormatMapService;
            _formatMapService = formatMapService;
            _controlCharUtil = controlCharUtil;
            _protectedOperations = protectedOperations;
        }

        private IBlockCaret CreateBlockCaret(IWpfTextView textView)
        {
            var classificationFormaptMap = _classificationFormatMapService.GetClassificationFormatMap(textView);
            var formatMap = _formatMapService.GetEditorFormatMap(textView);
            return new BlockCaret(textView, BlockCaretAdornmentLayerName,  classificationFormaptMap, formatMap, _controlCharUtil, _protectedOperations);
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
