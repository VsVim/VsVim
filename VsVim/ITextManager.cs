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
        ITextView ActiveTextView { get; }

        /// <summary>
        /// Navigate Visual Studio to the given point
        /// </summary>
        bool NavigateTo(VirtualSnapshotPoint point);

        /// <summary>
        /// Save file if it's dirty
        /// </summary>
        void Save(ITextView textView);

        /// <summary>
        /// Close the passed in document
        /// </summary>
        void Close(ITextView textView, bool checkDirty);
    }
}
