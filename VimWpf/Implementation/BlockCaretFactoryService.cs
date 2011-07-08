using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation
{
    [Export(typeof(IBlockCaretFactoryService))]
    internal sealed class BlockCaretFactoryService : IBlockCaretFactoryService
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

        public IBlockCaret CreateBlockCaret(IWpfTextView textView)
        {
            var formatMap = _formatMapService.GetEditorFormatMap(textView);
            return new BlockCaret(textView, BlockCaretAdornmentLayerName, formatMap, _protectedOperations);
        }
    }
}
