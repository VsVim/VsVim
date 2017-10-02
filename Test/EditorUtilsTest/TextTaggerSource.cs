using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.Text.Editor;
using System.Threading;

namespace EditorUtils.UnitTest
{
    /// <summary>
    /// Tags all occurences of the specified text in the buffer 
    /// </summary>
    internal abstract class TextTaggerSource<T> 
        where T : ITag
    {
        private readonly T _tag;
        private string _text;

        internal event EventHandler Changed;

        internal string Text
        {
            get { return _text; }
            set
            {
                if (!StringComparer.Ordinal.Equals(_text, value))
                {
                    _text = value;
                    RaiseChanged();
                }
            }
        }

        internal T Tag
        {
            get { return _tag; }
        }

        internal TextTaggerSource(T tag)
        {
            _tag = tag;
        }

        internal void RaiseChanged()
        {
            var list = Changed;
            if (list != null)
            {
                list(this, EventArgs.Empty);
            }
        }

        internal static bool IsMatch(ITextSnapshot snapshot, int position, string text)
        {
            if (position + text.Length > snapshot.Length || string.IsNullOrEmpty(text))
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                if (snapshot[i + position] != text[i])
                {
                    return false;
                }
            }

            return true;
        }

        internal static ReadOnlyCollection<ITagSpan<T>> GetTags(string text, T tag, SnapshotSpan span)
        {
            var list = new List<ITagSpan<T>>();
            var position = span.Start.Position;
            var snapshot = span.Snapshot;

            while (position < span.Length)
            {
                if (IsMatch(snapshot, position, text))
                {
                    var tagSpan = new SnapshotSpan(snapshot, start: position, length: text.Length);
                    list.Add(new TagSpan<T>(tagSpan, tag));
                    position += text.Length;
                }
                else
                {
                    position++;
                }
            }

            return list.ToReadOnlyCollectionShallow();
        }
    }

    /// <summary>
    /// Tags all occurences of the specified text in the buffer 
    /// </summary>
    internal sealed class TextBasicTaggerSource<T> : TextTaggerSource<T>, IBasicTaggerSource<T>
        where T : ITag
    {
        internal TextBasicTaggerSource(T tag) : base(tag)
        {

        }

        event EventHandler IBasicTaggerSource<T>.Changed
        {
            add { Changed += value; }
            remove { Changed -= value; }
        }

        ReadOnlyCollection<ITagSpan<T>> IBasicTaggerSource<T>.GetTags(SnapshotSpan span)
        {
            return GetTags(Text, Tag, span);
        }
    }

    internal sealed class TextAsyncTaggerSource<T> : TextTaggerSource<T>, IAsyncTaggerSource<Tuple<string, T>, T>
        where T : ITag
    {
        private readonly ITextBuffer _textBuffer;
        private readonly ITextView _textView;

        internal TextAsyncTaggerSource(T tag, ITextBuffer textBuffer) : base(tag)
        {
            _textBuffer = textBuffer;
        }

        internal TextAsyncTaggerSource(T tag, ITextView textView) : base(tag)
        {
            _textView = textView;
            _textBuffer = textView.TextBuffer;
        }

        int? IAsyncTaggerSource<Tuple<string, T>, T>.Delay
        {
            get { return 100; }
        }

        ITextSnapshot IAsyncTaggerSource<Tuple<string, T>, T>.TextSnapshot
        {
            get { return _textBuffer.CurrentSnapshot; }
        }

        ITextView IAsyncTaggerSource<Tuple<string, T>, T>.TextViewOptional
        {
            get { return _textView; }
        }

        event EventHandler IAsyncTaggerSource<Tuple<string, T>, T>.Changed
        {
            add { Changed += value; } 
            remove { Changed -= value; }
        }

        Tuple<string, T> IAsyncTaggerSource<Tuple<string, T>, T>.GetDataForSnapshot(ITextSnapshot snapshot)
        {
            return Tuple.Create(Text, Tag);
        }

        ReadOnlyCollection<ITagSpan<T>> IAsyncTaggerSource<Tuple<string, T>, T>.GetTagsInBackground(Tuple<string, T> data, SnapshotSpan span, CancellationToken cancellationToken)
        {
            return GetTags(data.Item1, data.Item2, span);
        }

        bool IAsyncTaggerSource<Tuple<string, T>, T>.TryGetTagsPrompt(SnapshotSpan span, out IEnumerable<ITagSpan<T>> tags)
        {
            tags = null;
            return false;
        }
    }
}
