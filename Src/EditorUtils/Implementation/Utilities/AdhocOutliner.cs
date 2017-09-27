using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace EditorUtils.Implementation.Utilities
{
    /// <summary>
    /// Implementation of the IAdhocOutliner service.  This class is only used for testing
    /// so it's focused on creating the simplest implementation vs. the most efficient
    /// </summary>
    internal sealed class AdhocOutliner : IAdhocOutliner, IBasicTaggerSource<OutliningRegionTag>
    {
        internal static readonly object OutlinerKey = new object();
        internal static readonly object OutlinerTaggerKey = new object();

        private struct OutliningData
        {
            internal readonly ITrackingSpan TrackingSpan;
            internal readonly OutliningRegionTag Tag;
            internal readonly int Cookie;

            internal OutliningData(
                ITrackingSpan trackingSpan,
                OutliningRegionTag tag,
                int cookie)
            {
                TrackingSpan = trackingSpan;
                Tag = tag;
                Cookie = cookie;
            }
        }

        private static readonly ReadOnlyCollection<OutliningRegion> s_emptyCollection = new ReadOnlyCollection<OutliningRegion>(new OutliningRegion[] { });
        private Dictionary<int, OutliningData> _map;
        private readonly ITextBuffer _textBuffer;
        private int _counter;
        private event EventHandler _changed;

        internal AdhocOutliner(ITextBuffer textBuffer)
        {
            _textBuffer = textBuffer;
            _map = new Dictionary<int, OutliningData>();
        }

        /// <summary>
        /// The outlining implementation is worthless unless it is also registered as an ITagger 
        /// component.  If this hasn't happened by the time the APIs are being queried then it is
        /// a bug and we need to notify the developer
        /// </summary>
        private void EnsureTagger()
        {
            if (!_textBuffer.Properties.ContainsProperty(OutlinerTaggerKey))
            {
                var msg = "In order to use IAdhocOutliner you must also export an ITagger implementation for the buffer which return CreateOutliningTagger";
                throw new Exception(msg);
            }
        }

        /// <summary>
        /// Get all of the values which map to the given ITextSnapshot
        /// </summary>
        private ReadOnlyCollection<OutliningRegion> GetOutliningRegions(SnapshotSpan span)
        {
            // Avoid allocating a map or new collection if we are simply empty
            if (_map.Count == 0)
            {
                return s_emptyCollection;
            }

            var snapshot = span.Snapshot;
            var list = new List<OutliningRegion>();
            foreach (var cur in _map.Values)
            {
                var currentSpan = cur.TrackingSpan.GetSpanSafe(snapshot);
                if (currentSpan.HasValue && currentSpan.Value.IntersectsWith(span))
                {
                    list.Add(new OutliningRegion(cur.Tag, currentSpan.Value, cur.Cookie));
                }
            }

            return list.ToReadOnlyCollectionShallow();
        }

        private OutliningRegion CreateOutliningRegion(SnapshotSpan span, SpanTrackingMode spanTrackingMode, string text, string hint)
        {
            var snapshot = span.Snapshot;
            var trackingSpan = snapshot.CreateTrackingSpan(span.Span, spanTrackingMode);
            var tag = new OutliningRegionTag(text, hint);
            var data = new OutliningData(trackingSpan, tag, _counter);
            _map[_counter] = data;
            _counter++;
            RaiseChanged();
            return new OutliningRegion(tag, span, data.Cookie);
        }

        /// <summary>
        /// Notify that our tags have changed.  Not going to get too fancy here since it's just for 
        /// test code.  Just raise the event for the entire ITextSnapshot
        /// </summary>
        private void RaiseChanged()
        {
            var handler = _changed;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        #region IAdhocOutliner

        ITextBuffer IAdhocOutliner.TextBuffer
        {
            get { return _textBuffer; }
        }

        ReadOnlyCollection<OutliningRegion> IAdhocOutliner.GetOutliningRegions(SnapshotSpan span)
        {
            EnsureTagger();
            return GetOutliningRegions(span);
        }

        OutliningRegion IAdhocOutliner.CreateOutliningRegion(SnapshotSpan span, SpanTrackingMode spanTrackingMode, string text, string hint)
        {
            EnsureTagger();
            return CreateOutliningRegion(span, spanTrackingMode, text, hint);
        }

        bool IAdhocOutliner.DeleteOutliningRegion(int cookie)
        {
            EnsureTagger();
            var success = _map.Remove(cookie);
            if (success)
            {
                RaiseChanged();
            }
            return success;
        }

        event EventHandler IAdhocOutliner.Changed
        {
            add { _changed += value; }
            remove { _changed -= value; }
        }

        #endregion

        #region IBasicTagger<OutliningRegionTag>

        ReadOnlyCollection<ITagSpan<OutliningRegionTag>> IBasicTaggerSource<OutliningRegionTag>.GetTags(SnapshotSpan span)
        {
            return GetOutliningRegions(span)
                .Select(x => (ITagSpan<OutliningRegionTag>)(new TagSpan<OutliningRegionTag>(x.Span, x.Tag)))
                .ToReadOnlyCollection();
        }

        event EventHandler IBasicTaggerSource<OutliningRegionTag>.Changed
        {
            add { _changed += value; }
            remove { _changed -= value; }
        }

        #endregion
    }
}
