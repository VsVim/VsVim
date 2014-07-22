using System;
using System.Collections.Generic;
using System.Windows.Input;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Vim.VisualStudio
{
    /// <summary>
    /// Adapter layer to convert between 2010 and pre-2010 equivalent types 
    /// and hierarchies
    /// </summary>
    public interface IVsAdapter
    {
        /// <summary>
        /// Returns true if we're in the middle of an automation (think macro) call
        /// </summary>
        bool InAutomationFunction { get; }

        /// <summary>
        /// Returns whether or not Visual Studio is currently in debug mode
        /// </summary>
        bool InDebugMode { get; }

        /// <summary>
        /// Service provider instance
        /// </summary>
        IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Core Editor Adapter factory service
        /// </summary>
        IVsEditorAdaptersFactoryService EditorAdapter { get; }

        /// <summary>
        /// The KeyboardDevice currently in use
        /// </summary>
        KeyboardDevice KeyboardDevice { get; }

        /// <summary>
        /// Get the 
        /// IVsTextLines associated with the ITextBuffer.  May not have
        /// 
        /// one if this is not a shim'd ITextBuffer
        /// </summary>
        Result<IVsTextLines> GetTextLines(ITextBuffer textBuffer);

        /// <summary>
        /// Get all of the IVsTextView's for the given ITextBuffer
        /// </summary>
        /// <param name="textBuffer"></param>
        /// <returns></returns>
        IEnumerable<IVsTextView> GetTextViews(ITextBuffer textBuffer);

        /// <summary>
        /// Is the buffer in the middle of a Visual Studio incremental search
        /// </summary>
        bool IsIncrementalSearchActive(ITextView textView);

        /// <summary>
        /// Is this a Venus window
        /// </summary>
        bool IsVenusView(IVsTextView textView);

        /// <summary>
        /// Determine if this ITextView is readonly.  This needs to mimic the behavior of 
        /// the VsCodeWindowAdapter::IsReadOnly method
        /// </summary>
        bool IsReadOnly(ITextBuffer textBuffer);

        /// <summary>
        /// Determine if this ITextView is readonly.  This needs to mimic the behavior of 
        /// the VsCodeWindowAdapter::IsReadOnly method
        /// </summary>
        bool IsReadOnly(ITextView textView);

        /// <summary>
        /// Get the IVsCodeWindowFrame instances currently open
        /// </summary>
        Result<List<IVsWindowFrame>> GetWindowFrames();

        /// <summary>
        /// Get the containing IVsCodeWindowFrame for the provided ITextBuffer
        /// </summary>
        Result<List<IVsWindowFrame>> GetContainingWindowFrames(ITextBuffer textBuffer);

        /// <summary>
        /// Get the IVsPersisteDocData for the provided ITextBuffer
        /// </summary>
        Result<IVsPersistDocData> GetPersistDocData(ITextBuffer textBuffer);

        /// <summary>
        /// Get the IVsCodeWindow for the given ITextView.  Multiple ITextView
        /// instances may resolve to the same IVsCodeWindow
        /// </summary>
        Result<IVsCodeWindow> GetCodeWindow(ITextView textView);

        /// <summary>
        /// Get the IVsWindowFrame.  Note that multiple ITextView instances
        /// may resolve to the same IVsWindowFrame.  Will happen in split view
        /// scenarios
        /// </summary>
        Result<IVsWindowFrame> GetContainingWindowFrame(ITextView textView);

        /// <summary>
        /// Get the document cookie for the ITextBuffer
        /// </summary>
        Result<uint> GetDocCookie(ITextDocument textDocument);

        Result<ITextBuffer> GetTextBufferForDocCookie(uint cookie);
    }
}
