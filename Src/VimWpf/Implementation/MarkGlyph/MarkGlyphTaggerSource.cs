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
        private readonly List<Tuple<string, int>> _glyphPairs;

        private int _activeMarks;
        private string _hideMarks;

        private EventHandler _changedEvent;

        internal MarkGlyphTaggerSource(IVimBufferData vimBufferData)
        {
            _vimBufferData = vimBufferData;
            _markMap = _vimBufferData.Vim.MarkMap;
            _lineNumberMap = new Dictionary<Mark, int>();
            _glyphPairs = new List<Tuple<string, int>>();
            _activeMarks = 0;
            _hideMarks = _vimBufferData.LocalSettings.HideMarks;

            LoadGlobalMarks();
            CachePairs();

            _markMap.MarkSet += OnMarkSet;
            _markMap.MarkDeleted += OnMarkDeleted;
            _vimBufferData.TextBuffer.Changed += OnTextBufferChanged;
            _vimBufferData.LocalSettings.SettingChanged += OnLocalSettingsChanged;
        }

        private void Dispose()
        {
            _markMap.MarkSet -= OnMarkSet;
            _markMap.MarkDeleted -= OnMarkDeleted;
            _vimBufferData.TextBuffer.Changed -= OnTextBufferChanged;
            _vimBufferData.LocalSettings.SettingChanged -= OnLocalSettingsChanged;
        }

        private bool IsMarkHidden(Mark mark)
        {
            return _hideMarks.Contains(mark.Char);
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
            if (UpdateAllMarks())
            {
                RaiseChanged();
            }
        }

        private void OnLocalSettingsChanged(object sender, SettingEventArgs e)
        {
            if (e.Setting.Name == LocalSettingNames.HideMarksName)
            {
                _hideMarks = _vimBufferData.LocalSettings.HideMarks;
                if (UpdateAllMarks())
                {
                    RaiseChanged();
                }
            }
        }

        private bool UpdateMark(Mark mark)
        {
            // Check first whether this mark is hidden.
            if (IsMarkHidden(mark))
            {
                if (_lineNumberMap.TryGetValue(mark, out int lineNumber) && lineNumber != -1)
                {
                    // Transition from active to inactive.
                    --_activeMarks;
                    return true;
                }
                else
                {
                    // It might become unhidden.
                    _lineNumberMap[mark] = -1;
                }
                return false;
            }

            // Get old line number.
            var oldLineNumber = -1;
            if (_lineNumberMap.TryGetValue(mark, out int currentLineNumber))
            {
                oldLineNumber = currentLineNumber;
            }

            // Get new line number.
            var newLineNumber = -1;
            var virtualPoint = _markMap.GetMark(mark, _vimBufferData);
            if (virtualPoint.IsSome())
            {
                var point = virtualPoint.Value.Position;
                var line = point.GetContainingLine();

                // Avoid displyaing marks on the phantom line.
                if (line.Length != 0 || line.LineBreakLength != 0 || point.Snapshot.LineCount == 1)
                {
                    newLineNumber = line.LineNumber;
                }
            }

            // Now check for no change.
            if (oldLineNumber == newLineNumber)
            {
                return false;
            }

            // Update active marks.
            if (oldLineNumber == -1)
            {
                ++_activeMarks;
            }
            if (newLineNumber == -1)
            {
                --_activeMarks;
            }

            // Record new line number.
            _lineNumberMap[mark] = newLineNumber;
            return true;
        }

        private bool UpdateAllMarks()
        {
            if (_lineNumberMap.Count == 0)
            {
                return false;
            }

            var wereMarksChanged = false;
            foreach (var mark in _lineNumberMap.Keys.ToList())
            {
                if (UpdateMark(mark))
                {
                    wereMarksChanged = true;
                }
            }
            return wereMarksChanged;
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
            _glyphPairs.Clear();

            if (_activeMarks == 0)
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
            _glyphPairs.AddRange(pairs);
        }

        private ReadOnlyCollection<ITagSpan<MarkGlyphTag>> GetTags(SnapshotSpan span)
        {
            if (_glyphPairs.Count == 0)
            {
                return s_emptyTagList;
            }

            var snapshot = span.Snapshot;
            var list = new List<ITagSpan<MarkGlyphTag>>();
            foreach (var pair in _glyphPairs)
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
