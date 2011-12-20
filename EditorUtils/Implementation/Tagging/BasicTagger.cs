
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
namespace EditorUtils.Implementation.Tagging
{
    internal sealed class BasicTagger<TTag> : ITagger<TTag>, IDisposable
        where TTag : ITag
    {
        private readonly IBasicTaggerSource<TTag> _basicTaggerSource;
        private SnapshotSpan? _cachedRequestSpan;
        private event EventHandler<SnapshotSpanEventArgs> _tagsChanged;

        internal SnapshotSpan? CachedRequestSpan
        {
            get { return _cachedRequestSpan; }
            set { _cachedRequestSpan = value; }
        }

        internal BasicTagger(IBasicTaggerSource<TTag> basicTaggerSource)
        {
            Contract.Requires(basicTaggerSource != null);
            _basicTaggerSource = basicTaggerSource;
            _basicTaggerSource.Changed += OnBasicTaggerSourceChanged;
        }

        private void Dispose()
        {
            _basicTaggerSource.Changed -= OnBasicTaggerSourceChanged;
            var disposable = _basicTaggerSource as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }

        private void AdjustRequestSpan(SnapshotSpan requestSpan)
        {
            _cachedRequestSpan = TaggerUtil.AdjustRequestedSpan(_cachedRequestSpan, requestSpan);
        }

        private IEnumerable<ITagSpan<TTag>> GetTags(NormalizedSnapshotSpanCollection col)
        {
            // Adjust the requested SnapshotSpan to be the overarching SnapshotSpan of the 
            // request.  
            var span = col.GetOverarchingSpan();
            AdjustRequestSpan(span);

            // Even though it's easier don't do a GetTags request for the overarching SnapshotSpan
            // of the request.  It's possible for the overarching SnapshotSpan to have an order
            // magnitudes more lines than the items in the collection.  This is very possible when
            // large folded regions or on screen.  Instead just request the individual ones
            return col.Count == 1
                ? _basicTaggerSource.GetTags(col[0])
                : col.SelectMany(_basicTaggerSource.GetTags);
        }

        private void OnBasicTaggerSourceChanged(object sender, EventArgs e)
        {
            if (_cachedRequestSpan.HasValue && _tagsChanged != null)
            {
                var args = new SnapshotSpanEventArgs(_cachedRequestSpan.Value);
                _tagsChanged(this, args);
            }
        }

        #region ITagger<TTag>

        IEnumerable<ITagSpan<TTag>> ITagger<TTag>.GetTags(NormalizedSnapshotSpanCollection col)
        {
            return GetTags(col);
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<TTag>.TagsChanged
        {
            add { _tagsChanged += value; }
            remove { _tagsChanged -= value; }
        }

        #endregion

        #region IDisposable

        void IDisposable.Dispose()
        {
            Dispose();
        }

        #endregion

    }
}
