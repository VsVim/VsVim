using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

            _markMap.MarkChanged += OnMarkChanged;
        }

        private void Dispose()
        {
            _markMap.MarkChanged -= OnMarkChanged;
        }

        private void OnMarkChanged(object sender, MarkChangedEventArgs args)
        {
            if (args.VimBufferData == _vimBufferData)
            {
                UpdateMark(args.Mark);
                RaiseChanged();
            }
        }

        private void UpdateMark(Mark mark)
        {
            var virtualPoint = _markMap.GetMark(mark, _vimBufferData);
            if (virtualPoint.IsSome())
            {
                var line = virtualPoint.Value.Position.GetContainingLine();
                _lineNumberMap[mark] = line.LineNumber;
            }
            else
            {
                _lineNumberMap.Remove(mark);
            }
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
            foreach (var pair in _lineNumberMap)
            {
                var mark = pair.Key;
                var lineNumber = pair.Value;
                var lineSpan = new SnapshotSpan(span.Snapshot.GetLineFromLineNumber(lineNumber).Start, 0);
                if (span.Contains(lineSpan))
                {
                    var tag = new MarkGlyphTag(mark.Char);
                    var tagSpan = new TagSpan<MarkGlyphTag>(lineSpan, tag);
                    list.Add(tagSpan);
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
