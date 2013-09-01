using System.ComponentModel.Composition;
using EditorUtils;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation.Paste
{
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class PasteFactoryService : IVimBufferCreationListener
    {
        internal const string PasteAdornmentLayerName = "VimPasteAdornmentLayer";

        private readonly IEditorFormatMapService _formatMapService;
        private readonly IProtectedOperations _protectedOperations;

        [ImportingConstructor]
        internal PasteFactoryService(IEditorFormatMapService formatMapService, IVimProtectedOperations protectedOperations)
        {
            _formatMapService = formatMapService;
            _protectedOperations = protectedOperations;
        }

#pragma warning disable 169
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

            var controller = new PasteController(
                vimBuffer,
                wpfTextView,
                _protectedOperations,
                _formatMapService.GetEditorFormatMap(wpfTextView));
        }
    }
}
