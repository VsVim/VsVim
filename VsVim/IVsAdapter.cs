using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;

namespace VsVim
{
    /// <summary>
    /// Adapter layer to convert between Dev10 and pre-Dev10 equivalent types
    /// </summary>
    public interface IVsAdapter
    {
        bool TryGetCodeWindow(ITextView textView, out IVsCodeWindow codeWindow);
        bool TryGetContainingWindowFrame(ITextView textView, out IVsWindowFrame frame);
        bool TryGetContainingWindowFrame(IVsTextView textView, out IVsWindowFrame windowFrame);
        bool TryGetTextBufferForDocCookie(uint cookie, out ITextBuffer textBuffer);
        IEnumerable<IVsTextView> GetTextViews(ITextBuffer textBuffer);
    }
}
