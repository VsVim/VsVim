using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.Collections.ObjectModel;

namespace EditorUtils
{
    /// <summary>
    /// Allows callers to create outlining regions over arbitrary SnapshotSpan values
    /// </summary>
    public interface IAdhocOutliner
    {
        /// <summary>
        /// Get the ITextBuffer associated with this instance
        /// </summary>
        ITextBuffer TextBuffer { get; }

        /// <summary>
        /// Get all of the regions in the given ITextSnapshot
        /// </summary>
        ReadOnlyCollection<ITagSpan<OutliningRegionTag>> GetRegions(SnapshotSpan span);

        /// <summary>
        /// Create an outlining region over the given SnapshotSpan.  The int value returned is 
        /// a cookie for later deleting the region
        /// </summary>
        int CreateOutliningRegion(SnapshotSpan span, string text, string hint);

        /// <summary>
        /// Delete the previously created outlining region with the given cookie
        /// </summary>
        bool DeleteOutliningRegion(int cookie);

        /// <summary>
        /// Raised when any outlining regions change
        /// </summary>
        event EventHandler Changed;
    }

    /// <summary>
    /// Factory for acquiring instances of the IAdhocOutliner.  This type is available as a MEF
    /// service
    /// </summary>
    public interface IAdhocOutlinerFactory
    {
        /// <summary>
        /// Get the IAdhocOutliner associated with this ITextBuffer
        /// </summary>
        IAdhocOutliner GetAdhocOutliner(ITextBuffer textBuffer);
    }
}
