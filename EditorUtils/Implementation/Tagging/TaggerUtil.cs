
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
            if (cachedRequestSpan.HasValue)
            {
                if (cachedRequestSpan.Value.Snapshot == requestSpan.Snapshot)
                {
                    return cachedRequestSpan.Value.CreateOverarching(requestSpan);
                }
                else
                {
                    return requestSpan;
                }
            }
            else
            {
                return requestSpan;
            }
        }
    }
}
