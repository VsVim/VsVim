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
        static ReadOnlyCollection<ITagSpan<MarkGlyphTag>> s_emptyTagList =
            new ReadOnlyCollection<ITagSpan<MarkGlyphTag>>(new List<ITagSpan<MarkGlyphTag>>());

        private readonly IVimBufferData _vimBufferData;
        private readonly IMarkMap _markMap;
        private readonly Dictionary<Mark, int> _lineNumberMap;
        private readonly List<Tuple<string, int>> _pairs = new List<Tuple<string, int>>();

        private EventHandler _changedEvent;

        internal MarkGlyphTaggerSource(IVimBufferData vimBufferData)
        {
            _vimBufferData = vimBufferData;
            _markMap = _vimBufferData.Vim.MarkMap;
            _lineNumberMap = new Dictionary<Mark, int>();

            LoadGlobalMarks();
            CachePairs();

            _markMap.MarkSet += OnMarkSet;
            _markMap.MarkDeleted += OnMarkDeleted;
            _vimBufferData.VimTextBuffer.MarkSet += OnMarkSet;
            _vimBufferData.TextBuffer.Changed += OnTextBufferChanged;
        }

        private void Dispose()
        {
            _markMap.MarkSet -= OnMarkSet;
            _markMap.MarkDeleted -= OnMarkDeleted;
            _vimBufferData.VimTextBuffer.MarkSet -= OnMarkSet;
            _vimBufferData.TextBuffer.Changed -= OnTextBufferChanged;
        }

        private void LoadGlobalMarks()
        {
            foreach (var tuple in _markMap.GlobalMarks)
            {
                var letter = tuple.Item1;
                var virtualPoint = tuple.Item2;
                if (virtualPoint.Position.Snapshot.TextBuffer == _vimBufferData.TextBuffer)
                {
                    UpdateMark(Mark.NewGlobalMark(letter));
                }
            }
        }

        private void OnMarkSet(object sender, MarkChangedEventArgs args)
        {
            if (args.TextBuffer == _vimBufferData.TextBuffer)
            {
                if (UpdateMark(args.Mark))
                {
                    RaiseChanged();
                }
            }
        }

        private void OnMarkDeleted(object sender, MarkChangedEventArgs args)
        {
            if (args.TextBuffer == _vimBufferData.TextBuffer)
            {
                RemoveMark(args.Mark);
                RaiseChanged();
            }
        }

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            if (_lineNumberMap.Count != 0)
            {
                var wereMarksChanged = false;
                foreach (var mark in _lineNumberMap.Keys.ToList())
                {
                    if (UpdateMark(mark))
                    {
                        wereMarksChanged = true;
                    }
                }
                if (wereMarksChanged)
                {
                    RaiseChanged();
                }
            }
        }

        private bool UpdateMark(Mark mark)
        {
            var virtualPoint = _markMap.GetMark(mark, _vimBufferData);
            var newLineNumber = -1;
            if (virtualPoint.IsSome())
            {
                var line = virtualPoint.Value.Position.GetContainingLine();
                if (line.Length != 0 || line.LineBreakLength != 0)
                {
                    newLineNumber = line.LineNumber;
                }
            }
            if (_lineNumberMap.TryGetValue(mark, out int oldLineNumber))
            {
                if (oldLineNumber == newLineNumber)
                {
                    return false;
                }
            }
            _lineNumberMap[mark] = newLineNumber;
            return true;
        }

        private void RemoveMark(Mark mark)
        {
            _lineNumberMap.Remove(mark);
        }

        private void RaiseChanged()
        {
            CachePairs();
            _changedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void CachePairs()
        {
            _pairs.Clear();

            if (_lineNumberMap.Count == 0)
            {
                return;
            }

            var pairs =
                _lineNumberMap
                .Where(pair => pair.Value != -1)
                .GroupBy(pair => pair.Value)
                .Select(grouping =>
                    Tuple.Create(
                        String.Concat(
                            grouping
                            .Select(pair => pair.Key.Char)
                            .OrderBy(key => key)),
                        grouping.Key
                    )
                );
            _pairs.AddRange(pairs);
        }

        private ReadOnlyCollection<ITagSpan<MarkGlyphTag>> GetTags(SnapshotSpan span)
        {
            if (_pairs.Count == 0)
            {
                return s_emptyTagList;
            }

            var snapshot = span.Snapshot;
            var list = new List<ITagSpan<MarkGlyphTag>>();
            foreach (var pair in _pairs)
            {
                var chars = pair.Item1;
                var lineNumber = pair.Item2;

                if (lineNumber < snapshot.LineCount)
                {
                    var line = snapshot.GetLineFromLineNumber(lineNumber);
                    var startSpan = new SnapshotSpan(line.Start, 0);
                    if (span.Contains(startSpan))
                    {
                        var tag = new MarkGlyphTag(chars);
                        var tagSpan = new TagSpan<MarkGlyphTag>(startSpan, tag);
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
