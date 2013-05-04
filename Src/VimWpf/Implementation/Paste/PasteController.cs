using System;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using EditorUtils;

namespace Vim.UI.Wpf.Implementation.Paste
{
    internal sealed class PasteController
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly PasteAdornment _pasteAdornment;

        internal PasteController(IVimBuffer vimBuffer, IWpfTextView wpfTextView, IProtectedOperations protectedOperations, IEditorFormatMap editorFormatMap)
        {
            _vimBuffer = vimBuffer;
            _pasteAdornment = new PasteAdornment(
                wpfTextView,
                wpfTextView.GetAdornmentLayer(PasteFactoryService.PasteAdornmentLayerName),
                protectedOperations,
                editorFormatMap);

            _vimBuffer.KeyInputProcessed += OnKeyInputProcessed;
            _vimBuffer.Closed += OnVimBufferClosed;
        }

        private void OnKeyInputProcessed(object sender, EventArgs e)
        {
            _pasteAdornment.IsDisplayed =
                _vimBuffer.ModeKind == ModeKind.Insert &&
                _vimBuffer.InsertMode.IsInPaste;
        }

        private void OnVimBufferClosed(object sender, EventArgs e)
        {
            _pasteAdornment.Destroy();
            _vimBuffer.KeyInputProcessed -= OnKeyInputProcessed;
            _vimBuffer.Closed -= OnVimBufferClosed;
        }
    }
}
