using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation.Paste
{
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class PasteFactoryService : IVimBufferCreationListener
    {
        internal const string PasteAdornmentLayerName = "VimPasteAdornmentLayer";

        private readonly IClassificationFormatMapService _classificationFormatMapService;
        private readonly IEditorFormatMapService _formatMapService;
        private readonly IProtectedOperations _protectedOperations;

        [ImportingConstructor]
        internal PasteFactoryService(
            IClassificationFormatMapService classificationFormatMapService,
            IEditorFormatMapService formatMapService,
            IProtectedOperations protectedOperations)
        {
            _classificationFormatMapService = classificationFormatMapService;
            _formatMapService = formatMapService;
            _protectedOperations = protectedOperations;
        }

#pragma warning disable 169, IDE0044
        [Export(typeof(AdornmentLayerDefinition))]
        [Name(PasteAdornmentLayerName)]
        [Order(After = PredefinedAdornmentLayers.Text)]
        private AdornmentLayerDefinition _pasteAdornmentLayerDefinition;
#pragma warning restore 169

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            var wpfTextView = vimBuffer.TextView as IWpfTextView;
            if (wpfTextView == null)
            {
                return;
            }

            var classificationFormaptMap = _classificationFormatMapService.GetClassificationFormatMap(wpfTextView);
            var editorFormatMap = _formatMapService.GetEditorFormatMap(wpfTextView);
            var controller = new PasteController(
                vimBuffer,
                wpfTextView,
                _protectedOperations,
                classificationFormaptMap,
                editorFormatMap);
        }
    }
}
