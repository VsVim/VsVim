
using Microsoft.VisualStudio.Text;
namespace EditorUtils.Implementation.Tagging
{
    internal static class TaggerUtil
    {
        /// <summary>
        /// The simple taggers when changed need to provide an initial SnapshotSpan 
        /// for the TagsChanged event.  It's important that this SnapshotSpan be kept as
        /// small as possible.  If it's incorrectly large it can have a negative performance
        /// impact on the editor.  In particular
        ///
        /// 1. The value is directly provided in ITagAggregator::TagsChanged.  This value
        ///    is acted on directly by many editor components.  Providing a large range 
        ///    unnecessarily increases their work load.
        /// 2. It can cause a ripple effect in Visual Studio 2010 RTM.  The SnapshotSpan 
        ///    returned will be immediately be the vale passed to GetTags for every other
        ///    ITagger in the system (TextMarkerVisualManager issue). 
        ///
        /// In order to provide the minimum possible valid SnapshotSpan the simple taggers
        /// cache the overarching SnapshotSpan for the latest ITextSnapshot of all requests
        /// to which they are given.
        /// </summary>
        internal static SnapshotSpan AdjustRequestedSpan(SnapshotSpan? cachedRequestSpan, SnapshotSpan requestSpan)
        {
            if (!cachedRequestSpan.HasValue)
            {
                return requestSpan;
            }

            var cachedSnapshot = cachedRequestSpan.Value.Snapshot;
            var requestSnapshot = requestSpan.Snapshot;

            if (cachedSnapshot == requestSnapshot)
            {
                // Same snapshot so we just need the overarching SnapshotSpan
                return cachedRequestSpan.Value.CreateOverarching(requestSpan);
            }

            if (cachedSnapshot.Version.VersionNumber < requestSnapshot.Version.VersionNumber)
            {
                // Request for a span on a new ITextSnapshot.  Translate the old SnapshotSpan
                // to the new ITextSnapshot and get the overarching value 
                var trackingSpan = cachedSnapshot.CreateTrackingSpan(cachedRequestSpan.Value.Span, SpanTrackingMode.EdgeInclusive);
                var traslatedSpan = trackingSpan.GetSpanSafe(requestSnapshot);
                if (traslatedSpan.HasValue)
                {
                    return traslatedSpan.Value.CreateOverarching(requestSpan);
                }

                // If we can't translate the previous SnapshotSpan forward then simply use the 
                // entire ITextSnapshot.  This is a correct value, it just has the potential for
                // some inefficiencies
                return requestSnapshot.GetExtent();
            }

            // It's a request for a value in the past.  This is a very rare scenario that is almost
            // always followed by a request for a value on the current snapshot.  Just return the 
            // entire ITextSnapshot.  This is a correct value, it just has the potential for
            // some inefficiencies 
            return requestSpan.Snapshot.GetExtent();
        }
    }
}
