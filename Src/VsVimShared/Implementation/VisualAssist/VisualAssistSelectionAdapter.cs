using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Vim;

namespace VsVim.Implementation.VisualAssist
{
    [Export(typeof(IVisualModeSelectionOverride))]
    internal sealed class VisualAssistSelectionAdapter : IVisualModeSelectionOverride
    {
        private readonly bool _isVisualAssistInstalled;

        [ImportingConstructor]
        internal VisualAssistSelectionAdapter(IVisualAssistUtil visualAssistUtil)
        {
            _isVisualAssistInstalled = visualAssistUtil.IsInstalled;
        }

        bool IVisualModeSelectionOverride.IsInsertModePreferred(ITextView textView)
        {
            if (!_isVisualAssistInstalled)
            {
                return false;
            }

            if (textView.Selection.IsEmpty || textView.Selection.Mode != TextSelectionMode.Stream)
            {
                return false;
            }

            var text = textView.Selection.StreamSelectionSpan.GetText();
            return text == "_" || text == "m_";
        }
    }
}
