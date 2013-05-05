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
    internal sealed class CharDisplayTaggerSource : IBasicTaggerSource<IntraTextAdornmentTag>, IDisposable
    {
        internal struct AdornmentData
        {
            internal readonly int Position;
            internal readonly UIElement Adornment;

            internal AdornmentData(int position, UIElement adornment)
            {
                Position = position;
                Adornment = adornment;
            }

            public override string ToString()
            {
                return Position.ToString();
            }
        }

        private static readonly ReadOnlyCollection<ITagSpan<IntraTextAdornmentTag>> EmptyTagColllection = new ReadOnlyCollection<ITagSpan<IntraTextAdornmentTag>>(new List<ITagSpan<IntraTextAdornmentTag>>());
        private readonly ITextView _textView;
        private readonly List<AdornmentData> _adornmentCache = new List<AdornmentData>();
        private EventHandler _changedEvent;

        internal List<AdornmentData> AdornmentCache
        {
            get { return _adornmentCache; }
        }

        internal CharDisplayTaggerSource(ITextView textView)
        {
            _textView = textView;
            _textView.TextBuffer.Changed += OnTextBufferChanged;
        }

        private void Dispose()
        {
            _textView.TextBuffer.Changed -= OnTextBufferChanged;
        }

        internal ReadOnlyCollection<ITagSpan<IntraTextAdornmentTag>> GetTags(SnapshotSpan span)
        {
            if (span.Snapshot != _textView.TextBuffer.CurrentSnapshot)
            {
                return EmptyTagColllection;
            }

            return GetTagsCore(span);
        }

        private ReadOnlyCollection<ITagSpan<IntraTextAdornmentTag>> GetTagsCore(SnapshotSpan span)
        {
            var list = new List<ITagSpan<IntraTextAdornmentTag>>();
            var offset = span.Start.Position;
            var snapshot = span.Snapshot;
            for (int i = 0; i < span.Length; i++)
            {
                var position = i + offset;
                var c = snapshot[position];

                string text;
                if (!TryGetDisplayText(c, out text))
                {
                    continue;
                }

                UIElement adornment;
                int cacheIndex;
                if (TryFindIndex(position, out cacheIndex))
                {
                    adornment = _adornmentCache[cacheIndex].Adornment;
                }
                else 
                {
                    var textBox = new TextBox();
                    textBox.Text = text;
                    textBox.BorderThickness = new Thickness(0);
                    textBox.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    adornment = textBox;
                    _adornmentCache.Insert(cacheIndex, new AdornmentData(position, adornment));
                }

                var tag = new IntraTextAdornmentTag(adornment, null);
                var adornmentSpan = new SnapshotSpan(snapshot, position, 1);
                var tagSpan = new TagSpan<IntraTextAdornmentTag>(adornmentSpan, tag);
                list.Add(tagSpan);
            }

            return list.ToReadOnlyCollectionShallow();
        }

        /// <summary>
        /// Try find the index into the adornment cache for the specified buffer position.  If the method 
        /// returns true then "index" will represent a valid index into the cache.  If it returns false
        /// then "position" isn't in the cache but "index" will still represent the position where it should
        /// be inserted
        /// </summary>
        private bool TryFindIndex(int position, out int index)
        {
            if (_adornmentCache.Count == 0)
            {
                index = 0;
                return false;
            }

            int min = 0;
            int max = _adornmentCache.Count - 1;
            int mid;
            int current;

            do
            {
                mid = (min + max) / 2;
                current = _adornmentCache[mid].Position;

                if (current == position)
                {
                    index = mid;
                    return true;
                }

                if (position < current)
                {
                    max = mid - 1;
                }
                else
                {
                    min = mid + 1;
                }
            } while (min <= max);

            // Search failed, calculate the insert position
            index = position < current ? mid : mid + 1;
            return false;
        }

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            foreach (var textChange in e.Changes)
            {
                OnTextChange(textChange);
            }
        }

        private void OnTextChange(ITextChange textChange)
        {
            int index = 0;

            // Move past the keys that don't matter 
            while (index < _adornmentCache.Count && _adornmentCache[index].Position < textChange.OldPosition)
            {
                index++;
            }

            if (textChange.Delta < 0)
            {
                // Remove the items which were in the deleted 
                while (index < _adornmentCache.Count && _adornmentCache[index].Position < textChange.OldEnd)
                {
                    _adornmentCache.RemoveAt(index);
                }
            }

            // Now adjust everything after the possible delete by the new value
            while (index < _adornmentCache.Count)
            {
                var old = _adornmentCache[index];
                _adornmentCache[index] = new AdornmentData(old.Position + textChange.Delta, old.Adornment);
                index++;
            }
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

        #region IDisposable

        void IDisposable.Dispose()
        {
            Dispose();            
        }

        #endregion
    }
}
