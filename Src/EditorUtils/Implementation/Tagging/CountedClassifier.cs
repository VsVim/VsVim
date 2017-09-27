using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using EditorUtils;
using Microsoft.VisualStudio.Text.Classification;

namespace EditorUtils.Implementation.Tagging
{
    /// <summary>
    /// This solves the same problem as CountedTagger but for IClassifier
    /// </summary>
    internal sealed class CountedClassifier : IClassifier, IDisposable
    {
        private readonly CountedValue<IClassifier> _countedValue;

        internal IClassifier Classifier
        {
            get { return _countedValue.Value; }
        }

        internal CountedClassifier(
            PropertyCollection propertyCollection,
            object key,
            Func<IClassifier> createFunc)
        {
            _countedValue = CountedValue<IClassifier>.GetOrCreate(propertyCollection, key, createFunc);
        }

        internal void Dispose()
        {
            _countedValue.Release();
        }

        #region IDisposable

        void IDisposable.Dispose()
        {
            Dispose();
        }

        #endregion

        #region IClassifier

        event EventHandler<ClassificationChangedEventArgs> IClassifier.ClassificationChanged
        {
            add { Classifier.ClassificationChanged += value; }
            remove { Classifier.ClassificationChanged -= value; }
        }


        IList<ClassificationSpan> IClassifier.GetClassificationSpans(SnapshotSpan span)
        {
            return Classifier.GetClassificationSpans(span);
        }

        #endregion
    }
}
