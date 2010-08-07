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
    /// and hierarchies
    /// </summary>
    public interface IVsAdapter
    {
        /// <summary>
        /// Get the IVsCodeWindow for the given ITextView.  Multiple ITextView
        /// instances may resolve to the same IVsCodeWindow
        /// </summary>
        bool TryGetCodeWindow(ITextView textView, out IVsCodeWindow codeWindow);

        /// <summary>
        /// Get the IVsWindowFrame.  Note that multiple ITextView instances
        /// may resolve to the same IVsWindowFrame.  Will happen in split view
        /// scenarios
        /// </summary>
        bool TryGetContainingWindowFrame(ITextView textView, out IVsWindowFrame frame);

        /// <summary>
        /// Get the IVsWindowFrame.  Note that multiple IVsTextView instances
        /// may resolve to the same IVsWindowFrame.  Will happen in split view
        /// scenarios
        /// </summary>
        bool TryGetContainingWindowFrame(IVsTextView textView, out IVsWindowFrame windowFrame);

        bool TryGetTextBufferForDocCookie(uint cookie, out ITextBuffer textBuffer);
        IEnumerable<IVsTextView> GetTextViews(ITextBuffer textBuffer);
    }
}
