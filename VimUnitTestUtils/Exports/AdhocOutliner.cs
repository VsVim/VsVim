using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Vim.Extensions;

namespace Vim.UnitTest.Exports
{
    /// <summary>
    /// Implementation of the IAdhocOutliner service.  This class is only used for testing
    /// so it's focused on creating the simplest implementation vs. the most efficient
    /// </summary>
    internal sealed class AdhocOutliner : IAdhocOutliner
    {
        struct OutliningData
        {
            internal ITrackingSpan TrackingSpan;
            internal string Text;
            internal string Hint;
        }

        private readonly Dictionary<int, OutliningData> _map = new Dictionary<int, OutliningData>();
        private readonly ITextBuffer _textBuffer;
        private int m_counter;
        private event EventHandler<SnapshotSpanEventArgs> Changed;

        internal AdhocOutliner(ITextBuffer textBuffer)
        {
            _textBuffer = textBuffer;
        }

        /// <summary>
        /// Get all of the values which map to the given ITextSnapshot
        /// </summary>
        private IEnumerable<ITagSpan<OutliningRegionTag>> GetRegions(ITextSnapshot snapshot)
        {
            foreach (var cur in _map.Values)
            {
                var option = TrackingSpanUtil.GetSpan(snapshot, cur.TrackingSpan);
                if (option.IsSome())
                {
                    var tag = new OutliningRegionTag(cur.Text, cur.Hint);
                    yield return new TagSpan<OutliningRegionTag>(option.Value, tag);
                }
            }
        }

        /// <summary>
        /// Notify that our tags have changed.  Not going to get too fancy here since it's just for 
        /// test code.  Just raise the event for the entire ITextSnapshot
        /// </summary>
        private void RaiseChanged()
        {
            var handler = Changed;
            if (handler != null)
            {
                handler(this, new SnapshotSpanEventArgs(_textBuffer.CurrentSnapshot.GetExtent()));
            }
        }

        #region IAdhocOutliner

        ITextBuffer IAdhocOutliner.TextBuffer
        {
            get { return _textBuffer; }
        }

        IEnumerable<ITagSpan<OutliningRegionTag>> IAdhocOutliner.GetRegions(ITextSnapshot snapshot)
        {
            return GetRegions(snapshot);
        }

        int IAdhocOutliner.CreateOutliningRegion(SnapshotSpan span, string text, string hint)
        {
            var snapshot = span.Snapshot;
            var trackingSpan = snapshot.CreateTrackingSpan(span.Span, SpanTrackingMode.EdgeInclusive);
            var data = new OutliningData() { TrackingSpan = trackingSpan, Text = text, Hint = hint };
            m_counter++;
            _map[m_counter] = data;
            RaiseChanged();
            return m_counter;
        }

        bool IAdhocOutliner.DeleteOutliningRegion(int cookie)
        {
            var success = _map.Remove(cookie);
            if (success)
            {
                RaiseChanged();
            }
            return success;
        }

        event EventHandler<SnapshotSpanEventArgs> IAdhocOutliner.Changed
        {
            add { Changed += value; }
            remove { Changed -= value; }
        }

        #endregion

        #region ITagger<OutliningRegion>

        IEnumerable<ITagSpan<OutliningRegionTag>> ITagger<OutliningRegionTag>.GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            {
                return new ITagSpan<OutliningRegionTag>[] {};
            }

            var snapshot = spans[0].Snapshot;
            return GetRegions(snapshot);
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<OutliningRegionTag>.TagsChanged
        {
            add { Changed += value; }
            remove { Changed -= value; }
        }

        #endregion
    }

    /// <summary>
    /// Responsible for managing instances of IAdhocOutliner for a given ITextBuffer
    /// </summary>
    [Export(typeof(IAdhocOutlinerFactory))]
    [Export(typeof(ITaggerProvider))]
    [ContentType(Constants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TagType(typeof(OutliningRegionTag))]
    internal sealed class AdhocOutlinerFactory : IAdhocOutlinerFactory, ITaggerProvider
    {
        private readonly object _key = new object();

        AdhocOutliner GetOutliner(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(_key, () => new AdhocOutliner(textBuffer));
        }

        IAdhocOutliner IAdhocOutlinerFactory.GetAdhocOutliner(ITextBuffer textBuffer)
        {
            return GetOutliner(textBuffer);
        }

        ITagger<T> ITaggerProvider.CreateTagger<T>(ITextBuffer buffer)
        {
            var outliner = GetOutliner(buffer);
            return (ITagger<T>)(object)(outliner);
        }
    }
}
