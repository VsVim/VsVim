using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using EditorUtils;

namespace EditorUtils.Implementation.Tagging
{
    /// <summary>
    /// It's possible, and very likely, for an ITagger<T> to be requested multiple times for the 
    /// same scenario via the ITaggerProvider.  This happens when extensions spin up custom 
    /// ITagAggregator instances or simple manually query for a new ITagger.  Having multiple taggers
    /// for the same data is often very unnecessary.  Produces a lot of duplicate work.  For example
    /// consider having multiple :hlsearch taggers for the same ITextView.  
    ///
    /// CountedTagger helps solve this by using a ref counted solution over the raw ITagger.  It allows
    /// for only one ITagger to be created for the same scenario
    /// </summary>
    internal sealed class CountedTagger<TTag> : ITagger<TTag>, IDisposable
        where TTag : ITag
    {
        private readonly CountedValue<ITagger<TTag>> _countedValue;

        internal ITagger<TTag> Tagger
        {
            get { return _countedValue.Value; }
        }

        internal CountedTagger(
            PropertyCollection propertyCollection,
            object key, 
            Func<ITagger<TTag>> createFunc)
        {
            _countedValue = CountedValue<ITagger<TTag>>.GetOrCreate(propertyCollection, key, createFunc);
        }

        internal void Dispose()
        {
            _countedValue.Release();
        }

        #region ITagger<TTag>

        IEnumerable<ITagSpan<TTag>> ITagger<TTag>.GetTags(NormalizedSnapshotSpanCollection col)
        {
            return Tagger.GetTags(col);
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<TTag>.TagsChanged
        {
            add { Tagger.TagsChanged += value; }
            remove { Tagger.TagsChanged -= value; }
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
