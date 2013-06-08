using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text.Classification;
using System.Windows.Media;

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
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly IVimGlobalSettings _globalSettings;
        private readonly List<AdornmentData> _adornmentCache = new List<AdornmentData>();
        private Brush _foregroundBrush;
        private EventHandler _changedEvent;

        internal List<AdornmentData> AdornmentCache
        {
            get { return _adornmentCache; }
        }

        internal CharDisplayTaggerSource(ITextView textView, IEditorFormatMap editorFormatMap, IVimGlobalSettings globalSettings)
        {
            _textView = textView;
            _editorFormatMap = editorFormatMap;
            _globalSettings = globalSettings;
            UpdateBrushes();

            _textView.TextBuffer.Changed += OnTextBufferChanged;
            _editorFormatMap.FormatMappingChanged += OnFormatMappingChanged;
            _globalSettings.SettingChanged += OnSettingChanged;
        }

        private void Dispose()
        {
            _textView.TextBuffer.Changed -= OnTextBufferChanged;
            _editorFormatMap.FormatMappingChanged -= OnFormatMappingChanged;
            _globalSettings.SettingChanged -= OnSettingChanged;
        }

        internal ReadOnlyCollection<ITagSpan<IntraTextAdornmentTag>> GetTags(SnapshotSpan span)
        {
            if (span.Snapshot != _textView.TextBuffer.CurrentSnapshot)
            {
                return EmptyTagColllection;
            }

            if (!_globalSettings.ControlChars)
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
                if (!ControlCharUtil.TryGetDisplayText(c, out text))
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
                    textBox.Foreground = _foregroundBrush;
                    textBox.FontWeight = FontWeights.Bold;
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

        private void UpdateBrushes()
        {
            var map = _editorFormatMap.GetProperties(ControlCharFormatDefinition.Name);
            _foregroundBrush = map.GetForegroundBrush(ControlCharFormatDefinition.DefaultForegroundBrush);
        }

        private void OnFormatMappingChanged(object sender, FormatItemsEventArgs e)
        {
            foreach (var key in e.ChangedItems)
            {
                if (key == ControlCharFormatDefinition.Name)
                {
                    UpdateBrushes();
                    _adornmentCache.Clear();
                    RaiseChanged();
                    break;
                }
            }
        }

        private void OnSettingChanged(object sender, SettingEventArgs e)
        {
            if (e.Setting.Name == GlobalSettingNames.ControlCharsName && e.IsValueChanged)
            {
                _adornmentCache.Clear();
                RaiseChanged();
            }
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
