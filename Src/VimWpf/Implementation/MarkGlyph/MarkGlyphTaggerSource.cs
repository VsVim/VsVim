using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Vim.Extensions;

namespace Vim.UI.Wpf.Implementation.MarkGlyph
{
    internal sealed class MarkGlyphTaggerSource : IBasicTaggerSource<MarkGlyphTag>, IDisposable
    {
        static ReadOnlyCollection<ITagSpan<MarkGlyphTag>> s_emptyTagList = new ReadOnlyCollection<ITagSpan<MarkGlyphTag>>(new List<ITagSpan<MarkGlyphTag>>());

        private readonly IVimBufferData _vimBufferData;
        private readonly IMarkMap _markMap;
        private readonly Dictionary<Mark, int> _lineNumberMap;

        private EventHandler _changedEvent;

        internal MarkGlyphTaggerSource(IVimBufferData vimBufferData)
        {
            _vimBufferData = vimBufferData;
            _markMap = _vimBufferData.Vim.MarkMap;
            _lineNumberMap = new Dictionary<Mark, int>();

            _markMap.MarkSet += OnMarkSet;
            _markMap.MarkDeleted += OnMarkDeleted;
            _vimBufferData.TextBuffer.Changed += OnTextBufferChanged;
        }

        private void Dispose()
        {
            _markMap.MarkSet -= OnMarkSet;
            _markMap.MarkDeleted -= OnMarkDeleted;
            _vimBufferData.TextBuffer.Changed -= OnTextBufferChanged;
        }

        private void OnMarkSet(object sender, MarkChangedEventArgs args)
        {
            if (args.VimBufferData == _vimBufferData)
            {
                UpdateMark(args.Mark);
                RaiseChanged();
            }
        }

        private void OnMarkDeleted(object sender, MarkChangedEventArgs args)
        {
            if (args.VimBufferData == _vimBufferData)
            {
                RemoveMark(args.Mark);
                RaiseChanged();
            }
        }

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            foreach (var mark in _lineNumberMap.Keys.ToList())
            {
                UpdateMark(mark);
            }
            RaiseChanged();
        }

        private void UpdateMark(Mark mark)
        {
            var virtualPoint = _markMap.GetMark(mark, _vimBufferData);
            if (virtualPoint.IsSome())
            {
                var line = virtualPoint.Value.Position.GetContainingLine();
                if (line.Length != 0 || line.LineBreakLength != 0)
                {
                    _lineNumberMap[mark] = line.LineNumber;
                }
                else
                {
                    _lineNumberMap[mark] = -1;
                }
            }
            else
            {
                _lineNumberMap[mark] = -1;
            }
        }

        private void RemoveMark(Mark mark)
        {
            _lineNumberMap.Remove(mark);
        }

        private void RaiseChanged()
        {
            _changedEvent?.Invoke(this, EventArgs.Empty);
        }

        internal ReadOnlyCollection<ITagSpan<MarkGlyphTag>> GetTags(SnapshotSpan span)
        {
            if (_lineNumberMap.Count == 0)
            {
                return s_emptyTagList;
            }

            var list = new List<ITagSpan<MarkGlyphTag>>();
            foreach (var grouping in _lineNumberMap.GroupBy(x => x.Value))
            {
                var pair = grouping.OrderBy(x => x.Key.Char).First();
                var mark = pair.Key;
                var lineNumber = pair.Value;
                if (lineNumber != -1)
                {
                    var lineSpan = new SnapshotSpan(span.Snapshot.GetLineFromLineNumber(lineNumber).Start, 0);
                    if (span.Contains(lineSpan))
                    {
                        var tag = new MarkGlyphTag(mark.Char);
                        var tagSpan = new TagSpan<MarkGlyphTag>(lineSpan, tag);
                        list.Add(tagSpan);
                    }
                }
            }
            return list.ToReadOnlyCollectionShallow();
        }

        #region IBasicTaggerSource<MarkGlyphTag>

        event EventHandler IBasicTaggerSource<MarkGlyphTag>.Changed
        {
            add { _changedEvent += value; }
            remove { _changedEvent -= value; }
        }

        ReadOnlyCollection<ITagSpan<MarkGlyphTag>> IBasicTaggerSource<MarkGlyphTag>.GetTags(SnapshotSpan span)
        {
            return GetTags(span);
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
