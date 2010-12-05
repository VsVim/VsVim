using System.Collections.Generic;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace VsVim
{
    /// <summary>
    /// Adapter layer to convert between Dev10 and pre-Dev10 equivalent types 
    /// and hierarchies
    /// </summary>
    public interface IVsAdapter
    {
        /// <summary>
        /// Core Editor Adapter factory service
        /// </summary>
        IVsEditorAdaptersFactoryService EditorAdapter { get; }

        /// <summary>
        /// Get the IVsTextLines associated with the ITextBuffer.  May not have
        /// one if this is not a shim'd ITextBuffer
        /// </summary>
        Result<IVsTextLines> GetTextLines(ITextBuffer textBuffer);

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

        /// <summary>
        /// Is this a Venus window
        /// </summary>
        bool IsVenusView(IVsTextView textView);

        /// <summary>
        /// Determine if this ITextBuffer is readonly.  This needs to mimic the behavior of 
        /// the VsCodeWindowAdapter::IsReadOnly method.
        /// </summary>
        bool IsReadOnly(ITextBuffer textBuffer);

        bool TryGetTextBufferForDocCookie(uint cookie, out ITextBuffer textBuffer);

        /// <summary>
        /// Get all of the IVsTextView's for the given ITextBuffer
        /// </summary>
        /// <param name="textBuffer"></param>
        /// <returns></returns>
        IEnumerable<IVsTextView> GetTextViews(ITextBuffer textBuffer);
    }
}
