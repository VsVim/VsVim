using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Vim.UnitTest
{
    /// <summary>
    /// Allows callers to create outlining regions over arbitrary SnapshotSpan values
    ///
    /// The Visual Studio editor provides the ability to create outlining regions over arbitrary 
    /// SnapshotSpan values while Vim only provides for line based regions.  This class is useful
    /// for simulating Visual Studio's abilities as VsVim still needs to interact with them
    /// </summary>
    public interface IAdhocOutliner : ITagger<OutliningRegionTag>
    {
        /// <summary>
        /// Get the ITextBuffer associated with this instance
        /// </summary>
        ITextBuffer TextBuffer { get; }

        /// <summary>
        /// Get all of the regions in the given ITextSnapshot
        /// </summary>
        IEnumerable<ITagSpan<OutliningRegionTag>> GetRegions(ITextSnapshot snapshot);

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
        /// Raised when any regions change in the ITextBuffer
        /// </summary>
        event EventHandler<SnapshotSpanEventArgs> Changed;
    }

    /// <summary>
    /// Factory for acquiring instances of the IAdhocOutliner
    /// </summary>
    public interface IAdhocOutlinerFactory
    {
        /// <summary>
        /// Get the IAdhocOutliner associated with this ITextBuffer
        /// </summary>
        IAdhocOutliner GetAdhocOutliner(ITextBuffer textBuffer);
    }
}
