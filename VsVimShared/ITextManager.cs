using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace VsVim
{
    public interface ITextManager
    {
        /// <summary>
        /// Set of active ITextBuffers
        /// </summary>
        IEnumerable<ITextBuffer> TextBuffers { get; }

        /// <summary>
        /// Set of all active IWpfTextViews
        /// </summary>
        IEnumerable<ITextView> TextViews { get; }

        /// <summary>
        /// Returns the active IWpfITextView
        /// </summary>
        ITextView ActiveTextViewOptional { get; }

        /// <summary>
        /// Get the ITextView instances which are attachked to this ITextBuffer
        /// </summary>
        IEnumerable<ITextView> GetTextViews(ITextBuffer textBuffer);

        /// <summary>
        /// Navigate Visual Studio to the given point
        /// </summary>
        bool NavigateTo(VirtualSnapshotPoint point);

        /// <summary>
        /// Save file if it's dirty
        /// </summary>
        Result Save(ITextBuffer textBuffer);

        /// <summary>
        /// Close the given view passed in.  If there is a split view only
        /// the split will be closed
        /// </summary>
        bool CloseView(ITextView textView);

        /// <summary>
        /// Split the provided view
        /// </summary>
        bool SplitView(ITextView textView);

        /// <summary>
        /// Move to the view above the current one.  Returns false if there is no 
        /// view above this one
        /// </summary>
        bool MoveViewUp(ITextView textView);

        /// <summary>
        /// Move to the view below the passed in one.  Returns fales if there is 
        /// no view below this one
        /// </summary>
        bool MoveViewDown(ITextView textView);
    }
}
