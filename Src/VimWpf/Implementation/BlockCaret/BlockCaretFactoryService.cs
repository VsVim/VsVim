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

        private readonly IVimHost _vimHost;
        private readonly IClassificationFormatMapService _classificationFormatMapService;
        private readonly IEditorFormatMapService _formatMapService;
        private readonly IProtectedOperations _protectedOperations;
        private readonly IControlCharUtil _controlCharUtil;

#pragma warning disable 169, IDE0044
        [Export(typeof(AdornmentLayerDefinition))]
        [Name(BlockCaretAdornmentLayerName)]
        [Order(After = PredefinedAdornmentLayers.Caret)]
        private AdornmentLayerDefinition _blockCaretAdornmentLayerDefinition;
#pragma warning restore 169

        [ImportingConstructor]
        internal BlockCaretFactoryService(
            IVimHost vimHost,
            IClassificationFormatMapService classificationFormatMapService,
            IEditorFormatMapService formatMapService,
            IControlCharUtil controlCharUtil,
            IProtectedOperations protectedOperations)
        {
            _vimHost = vimHost;
            _classificationFormatMapService = classificationFormatMapService;
            _formatMapService = formatMapService;
            _controlCharUtil = controlCharUtil;
            _protectedOperations = protectedOperations;
        }

        private IBlockCaret CreateBlockCaret(IWpfTextView textView)
        {
            var classificationFormaptMap = _classificationFormatMapService.GetClassificationFormatMap(textView);
            var editorFormatMap = _formatMapService.GetEditorFormatMap(textView);
            return new BlockCaret(
                _vimHost,
                textView,
                BlockCaretAdornmentLayerName,
                classificationFormaptMap,
                editorFormatMap,
                _controlCharUtil,
                _protectedOperations);
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
            var caretController = new BlockCaretController(_vimHost, vimBuffer, caret);
        }

        #endregion
    }
}
