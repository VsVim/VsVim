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

                string text;
                if (!TryGetDisplayText(c, out text))
                {
                    continue;
                }

                UIElement adornment;
                if (!_adornmentCache.TryGetValue(index, out adornment))
                {
                    var textBox = new TextBox();
                    textBox.Text = text;
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
            var i = (int)c;
            return IsRelevant(i);
        }

        private static bool IsRelevant(int i)
        {
            return i <= 31;
        }

        private static bool TryGetDisplayText(char c, out string text)
        {
            int i = (int)c;
            if (!IsRelevant(i))
            {
                text = null;
                return false;
            }

            text = null;
            switch (i)
            {
                case 0: text = "^@"; break;
                case 1: text = "^A"; break;
                case 2: text = "^B"; break;
                case 3: text = "^C"; break;
                case 4: text = "^D"; break;
                case 5: text = "^E"; break;
                case 6: text = "^F"; break;
                case 7: text = "^G"; break;
                case 8: text = "^H"; break;
                    /*
                    don't transform line break characters at the moment
                case 9: text = "^I"; break;
                case 10: text = "^J"; break;
                case 11: text = "^K"; break;
                case 12: text = "^L"; break;
                case 13: text = "^M"; break;
                    */
                case 14: text = "^N"; break;
                case 15: text = "^O"; break;
                case 16: text = "^P"; break;
                case 17: text = "^Q"; break;
                case 18: text = "^R"; break;
                case 19: text = "^S"; break;
                case 20: text = "^T"; break;
                case 21: text = "^U"; break;
                case 22: text = "^V"; break;
                case 23: text = "^W"; break;
                case 24: text = "^X"; break;
                case 25: text = "^Y"; break;
                case 26: text = "^Z"; break;
                case 27: text = "^["; break;
                case 28: text = "^\\"; break;
                case 29: text = "^]"; break;
                case 30: text = "^^"; break;
                case 31: text = "^_"; break;
            }

            return text != null;
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
