using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace EditorUtils.Implementation.Outlining
{
    /// <summary>
    /// Implementation of the IAdhocOutliner service.  This class is only used for testing
    /// so it's focused on creating the simplest implementation vs. the most efficient
    /// </summary>
    internal sealed class AdhocOutliner : IAdhocOutliner, IBasicTaggerSource<OutliningRegionTag>
    {
        struct OutliningData
        {
            internal readonly ITrackingSpan TrackingSpan;
            internal readonly string Text;
            internal readonly string Hint;

            internal OutliningData(
                ITrackingSpan trackingSpan,
                string text,
                string hint)
            {
                TrackingSpan = trackingSpan;
                Text = text;
                Hint = hint;
            }
        }

        private static readonly ReadOnlyCollection<ITagSpan<OutliningRegionTag>> s_emptyCollection = new ReadOnlyCollection<ITagSpan<OutliningRegionTag>>(new List<ITagSpan<OutliningRegionTag>>());
        private Dictionary<int, OutliningData> _optionalMap;
        private readonly ITextBuffer _textBuffer;
        private int m_counter;
        private event EventHandler _changed;

        internal AdhocOutliner(ITextBuffer textBuffer)
        {
            _textBuffer = textBuffer;
        }

        private void EnsureMapCreated()
        {
            if (_optionalMap == null)
            {
                _optionalMap = new Dictionary<int, OutliningData>();
            }
        }

        /// <summary>
        /// Get all of the values which map to the given ITextSnapshot
        /// </summary>
        private ReadOnlyCollection<ITagSpan<OutliningRegionTag>> GetRegions(SnapshotSpan span)
        {
            // Avoid allocating a map or new collection if we are simply empty
            if (_optionalMap == null || _optionalMap.Count == 0)
            {
                return s_emptyCollection;
            }

            var snapshot = span.Snapshot;
            var list = new List<ITagSpan<OutliningRegionTag>>();
            foreach (var cur in _optionalMap.Values)
            {
                var currentSpan = cur.TrackingSpan.GetSpanSafe(snapshot);
                if (currentSpan.HasValue && currentSpan.Value.IntersectsWith(span))
                {
                    var tag = new OutliningRegionTag(cur.Text, cur.Hint);
                    list.Add(new TagSpan<OutliningRegionTag>(currentSpan.Value, tag));
                }
            }

            return list.ToReadOnlyCollectionShallow();
        }

        private int CreateOutliningRegion(SnapshotSpan span, string text, string hint)
        {
            EnsureMapCreated();

            var snapshot = span.Snapshot;
            var trackingSpan = snapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeInclusive);
            var data = new OutliningData(trackingSpan, text: text, hint: hint);
            m_counter++;
            _optionalMap[m_counter] = data;
            RaiseChanged();
            return m_counter;
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

        ReadOnlyCollection<ITagSpan<OutliningRegionTag>> IAdhocOutliner.GetRegions(SnapshotSpan span)
        {
            return GetRegions(span);
        }

        int IAdhocOutliner.CreateOutliningRegion(SnapshotSpan span, string text, string hint)
        {
            return CreateOutliningRegion(span, text, hint);
        }

        bool IAdhocOutliner.DeleteOutliningRegion(int cookie)
        {
            if (_optionalMap == null)
            {
                return false;
            }

            var success = _optionalMap.Remove(cookie);
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

        ITextSnapshot IBasicTaggerSource<OutliningRegionTag>.TextSnapshot
        {
            get { return _textBuffer.CurrentSnapshot; }
        }

        ReadOnlyCollection<ITagSpan<OutliningRegionTag>> IBasicTaggerSource<OutliningRegionTag>.GetTags(SnapshotSpan span)
        {
            return GetRegions(span);
        }

        event EventHandler IBasicTaggerSource<OutliningRegionTag>.Changed
        {
            add { _changed += value; }
            remove { _changed -= value; }
        }

        #endregion
    }

    /// <summary>
    /// Responsible for managing instances of IAdhocOutliner for a given ITextBuffer
    /// </summary>
    [Export(typeof(IAdhocOutlinerFactory))]
    [Export(typeof(ITaggerProvider))]
    [ContentType("any")]
    [TextViewRole(PredefinedTextViewRoles.Structured)]
    [TagType(typeof(OutliningRegionTag))]
    internal sealed class AdhocOutlinerFactory : IAdhocOutlinerFactory, ITaggerProvider
    {
        private readonly object _adhocOutlinerKey = new object();
        private readonly object _taggerKey = new object();
        private readonly ITaggerFactory _taggerFactory;

        [ImportingConstructor]
        internal AdhocOutlinerFactory(ITaggerFactory taggerFactory)
        {
            _taggerFactory = taggerFactory;
        }

        internal AdhocOutliner GetOrCreateOutliner(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(_adhocOutlinerKey, () => new AdhocOutliner(textBuffer));
        }

        internal ITagger<OutliningRegionTag> CreateTagger(ITextBuffer textBuffer)
        {
            return _taggerFactory.CreateBasicTagger(
                textBuffer.Properties,
                _taggerKey,
                () => GetOrCreateOutliner(textBuffer));
        }

        IAdhocOutliner IAdhocOutlinerFactory.GetAdhocOutliner(ITextBuffer textBuffer)
        {
            return GetOrCreateOutliner(textBuffer);
        }

        ITagger<T> ITaggerProvider.CreateTagger<T>(ITextBuffer textBuffer)
        {
            var tagger = CreateTagger(textBuffer);
            return (ITagger<T>)(object)tagger;
        }
    }
}
