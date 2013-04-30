using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Vim.UI.Wpf.Implementation.CharDisplay
{
    internal sealed class CharDisplayTaggerSource : IBasicTaggerSource<IntraTextAdornmentTag>
    {
        private readonly ITextView _textView;
        private readonly Dictionary<int, UIElement> _adornmentCache = new Dictionary<int, UIElement>();
        private EventHandler _changedEvent;

        internal CharDisplayTaggerSource(ITextView textView)
        {
            _textView = textView;
            _textView.TextBuffer.Changed += OnTextBufferChanged;
            _textView.Closed += OnTextViewClosed;
        }

        internal ReadOnlyCollection<ITagSpan<IntraTextAdornmentTag>> GetTags(SnapshotSpan span)
        {
            var list = new List<ITagSpan<IntraTextAdornmentTag>>();
            GetTags(span, list);
            return list.ToReadOnlyCollectionShallow();
        }

        private void GetTags(SnapshotSpan span, List<ITagSpan<IntraTextAdornmentTag>> list)
        {
            var offset = span.Start.Position;
            var snapshot = span.Snapshot;
            for (int i = 0; i < span.Length; i++)
            {
                var index = i + offset;
                var c = snapshot[index];
                if (!IsRelevant(c))
                {
                    continue;
                }

                UIElement adornment;
                if (!_adornmentCache.TryGetValue(index, out adornment))
                {
                    var textBox = new TextBox();
                    textBox.Text = "^]";
                    textBox.BorderThickness = new Thickness(0);
                    textBox.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    adornment = textBox;
                    _adornmentCache[index] = textBox;
                }

                var tag = new IntraTextAdornmentTag(adornment, null);
                var adornmentSpan = new SnapshotSpan(snapshot, index, 1);
                var tagSpan = new TagSpan<IntraTextAdornmentTag>(adornmentSpan, tag);
                list.Add(tagSpan);
            }
        }

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            foreach (var change in e.Changes)
            {
                if (CheckForChange(change.OldText) || CheckForChange(change.NewText))
                {
                    RaiseChanged();
                    return;
                }
            }

            _adornmentCache.Clear();
        }

        private bool CheckForChange(string text)
        {
            foreach (var c in text)
            {
                if (IsRelevant(c))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnTextViewClosed(object sender, EventArgs e)
        {
            _textView.TextBuffer.Changed -= OnTextBufferChanged;
        }

        private void RaiseChanged()
        {
            var e = _changedEvent;
            if (e != null)
            {
                e(this, EventArgs.Empty);
            }
        }

        private static bool IsRelevant(char c)
        {
            // TODO: Expand to all control characters.  For now this is just proof of concept
            return c == 29;
        }

        #region IBasicTaggerSource<IntraTextAdornmentTag>

        event EventHandler IBasicTaggerSource<IntraTextAdornmentTag>.Changed
        {
            add { _changedEvent += value; }
            remove { _changedEvent -= value; }
        }

        ReadOnlyCollection<ITagSpan<IntraTextAdornmentTag>> IBasicTaggerSource<IntraTextAdornmentTag>.GetTags(SnapshotSpan span)
        {
            return GetTags(span);
        }

        ITextSnapshot IBasicTaggerSource<IntraTextAdornmentTag>.TextSnapshot
        {
            get { return _textView.TextSnapshot; }
        }

        #endregion
    }
}
