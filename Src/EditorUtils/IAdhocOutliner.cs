using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.Collections.ObjectModel;

namespace EditorUtils
{
    public struct OutliningRegion
    {
        public readonly OutliningRegionTag Tag;
        public readonly SnapshotSpan Span;
        public readonly int Cookie;

        public OutliningRegion(
            OutliningRegionTag tag,
            SnapshotSpan span,
            int cookie)
        {
            Tag = tag;
            Span = span;
            Cookie = cookie;
        }
    }

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
        ReadOnlyCollection<OutliningRegion> GetOutliningRegions(SnapshotSpan span);

        /// <summary>
        /// Create an outlining region over the given SnapshotSpan.  The int value returned is 
        /// a cookie for later deleting the region
        /// </summary>
        OutliningRegion CreateOutliningRegion(SnapshotSpan span, SpanTrackingMode spanTrackingMode, string text, string hint);

        /// <summary>
        /// Delete the previously created outlining region with the given cookie
        /// </summary>
        bool DeleteOutliningRegion(int cookie);

        /// <summary>
        /// Raised when any outlining regions change
        /// </summary>
        event EventHandler Changed;
    }
}
