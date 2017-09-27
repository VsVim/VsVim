using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace EditorUtils
{
    public abstract class AsyncTaggerSource<TData, TTag> : IAsyncTaggerSource<TData, TTag>
        where TTag : ITag
    {
        private readonly ITextView _textViewOptional;
        private readonly ITextBuffer _textBuffer;
        private event EventHandler _changedEvent;

        public ITextView TextViewOptional
        {
            get { return _textViewOptional; }
        }

        public ITextBuffer TextBuffer
        {
            get { return _textBuffer; }
        }

        protected AsyncTaggerSource(ITextView textView)
        {
            Contract.Requires(textView != null);
            _textViewOptional = textView;
            _textBuffer = textView.TextBuffer;
        }

        protected AsyncTaggerSource(ITextBuffer textBuffer)
        {
            Contract.Requires(textBuffer != null);
            _textBuffer = textBuffer;
        }

        protected void RaiseChanged()
        {
            if (_changedEvent != null)
            {
                _changedEvent(this, EventArgs.Empty);
            }
        }

        protected virtual bool TryGetTagsPrompt(SnapshotSpan span, out IEnumerable<ITagSpan<TTag>> tags)
        {
            tags = null;
            return false;
        }

        /// <summary>
        /// Get the data needed in the background thread from the specified SnapshotSpan.  This is called on
        /// the main thread
        /// </summary>
        protected abstract TData GetDataForSnapshot(ITextSnapshot snapshot);

        /// <summary>
        /// Get the tags for the specified span.  This is called on the background thread
        /// </summary>
        protected abstract ReadOnlyCollection<ITagSpan<TTag>> GetTagsInBackground(TData data, SnapshotSpan span, CancellationToken cancellationToken);

        #region IAsyncTaggerSource<TData, TTag>

        int? IAsyncTaggerSource<TData, TTag>.Delay
        {
            get { return Constants.DefaultAsyncDelay; }
        }

        ITextSnapshot IAsyncTaggerSource<TData, TTag>.TextSnapshot
        {
            get { return TextBuffer.CurrentSnapshot; }
        }

        ITextView IAsyncTaggerSource<TData, TTag>.TextViewOptional
        {
            get { return TextViewOptional; }
        }

        TData IAsyncTaggerSource<TData, TTag>.GetDataForSnapshot(ITextSnapshot snapshot)
        {
            return GetDataForSnapshot(snapshot);
        }

        ReadOnlyCollection<ITagSpan<TTag>> IAsyncTaggerSource<TData, TTag>.GetTagsInBackground(TData data, SnapshotSpan span, CancellationToken cancellationToken)
        {
            return GetTagsInBackground(data, span, cancellationToken);
        }

        bool IAsyncTaggerSource<TData, TTag>.TryGetTagsPrompt(SnapshotSpan span, out IEnumerable<ITagSpan<TTag>> tags)
        {
            return TryGetTagsPrompt(span, out tags);
        }

        event EventHandler IAsyncTaggerSource<TData, TTag>.Changed
        {
            add { _changedEvent += value; }
            remove { _changedEvent -= value; }
        }

        #endregion
    }
}
