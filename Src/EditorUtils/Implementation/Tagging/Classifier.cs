using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace EditorUtils.Implementation.Tagging
{
    internal sealed class Classifier : IClassifier, IDisposable
    {
        private readonly ITagger<IClassificationTag> _tagger;
        private event EventHandler<ClassificationChangedEventArgs> _classificationChanged;

        internal Classifier(ITagger<IClassificationTag> tagger)
        {
            _tagger = tagger;
            _tagger.TagsChanged += OnTagsChanged;
        }

        private void Dispose()
        {
            _tagger.TagsChanged -= OnTagsChanged;
            var disposable = _tagger as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }

        private void OnTagsChanged(object sender, SnapshotSpanEventArgs e)
        {
            var list = _classificationChanged;
            if (list != null)
            {
                list(this, new ClassificationChangedEventArgs(e.Span));
            }
        }

        #region IClassifier

        event EventHandler<ClassificationChangedEventArgs> IClassifier.ClassificationChanged
        {
            add { _classificationChanged += value; }
            remove { _classificationChanged -= value; }
        }

        IList<ClassificationSpan> IClassifier.GetClassificationSpans(SnapshotSpan span)
        {
            return _tagger
                .GetTags(new NormalizedSnapshotSpanCollection(span))
                .Select(x => new ClassificationSpan(x.Span, x.Tag.ClassificationType))
                .ToList();
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
