using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        [Export(typeof(AdornmentLayerDefinition))]
        [Name(BlockCaretAdornmentLayerName)]
        [Order(After=PredefinedAdornmentLayers.Selection)]
        private AdornmentLayerDefinition _blockCaretAdornmentLayerDefinition = null;

        [ImportingConstructor]
        internal BlockCaretFactoryService(IEditorFormatMapService formatMapService)
        {
            _formatMapService = formatMapService;
        }

        public IBlockCaret CreateBlockCaret(IWpfTextView textView)
        {
            var formatMap = _formatMapService.GetEditorFormatMap(textView);
            return new BlockCaret(textView, BlockCaretAdornmentLayerName, formatMap);
        }
    }
}
