using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Vim.Extensions;

namespace Vim.UI.Wpf.Implementation.MarkGlyph
{
    internal sealed class MarkGlyphTagger : ITagger<MarkGlyphTag>, IDisposable
    {
        static ReadOnlyCollection<ITagSpan<MarkGlyphTag>> s_emptyTagList =
            new ReadOnlyCollection<ITagSpan<MarkGlyphTag>>(new List<ITagSpan<MarkGlyphTag>>());

        private readonly IVimBufferData _vimBufferData;
        private readonly IMarkMap _markMap;
        private readonly Dictionary<Mark, int> _lineNumberMap;
        private readonly List<Tuple<string, int>> _glyphPairs;

        private bool _isVisible;
        private int _activeMarks;
        private string _hideMarks;

        private EventHandler<SnapshotSpanEventArgs> _changedEvent;

        internal MarkGlyphTagger(IVimBufferData vimBufferData)
        {
            _vimBufferData = vimBufferData;
            _markMap = _vimBufferData.Vim.MarkMap;
            _lineNumberMap = new Dictionary<Mark, int>();
            _glyphPairs = new List<Tuple<string, int>>();
            _isVisible = true;
            _activeMarks = 0;
            _hideMarks = _vimBufferData.LocalSettings.HideMarks;

            LoadNewBufferMarks();
            CachePairs();

            _markMap.MarkSet += OnMarkSet;
            _markMap.MarkDeleted += OnMarkDeleted;
            _vimBufferData.TextView.VisualSnapshot.TextBuffer.Changed += OnTextBufferChanged;
            _vimBufferData.LocalSettings.SettingChanged += OnLocalSettingsChanged;
            _vimBufferData.Vim.VimHost.IsVisibleChanged += OnIsVisibleChanged;
        }

        private void Dispose()
        {
            _markMap.MarkSet -= OnMarkSet;
            _markMap.MarkDeleted -= OnMarkDeleted;
            _vimBufferData.TextView.VisualSnapshot.TextBuffer.Changed -= OnTextBufferChanged;
            _vimBufferData.LocalSettings.SettingChanged -= OnLocalSettingsChanged;
        }

        private bool IsMarkHidden(Mark mark)
        {
            return _hideMarks.Contains(mark.Char);
        }

        private void LoadNewBufferMarks()
        {
            // The last jump mark is set on all buffers.
            UpdateMark(Mark.LastJump);

            // Global marks are restored when a buffer is reloaded.
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
            VimTrace.TraceInfo($"MarkGlyphTagger::TextBufferChanged {e.AfterVersion}");
            UpdateAllMarks();
        }

        private void OnLocalSettingsChanged(object sender, SettingEventArgs e)
        {
            if (e.Setting.Name == LocalSettingNames.HideMarksName)
            {
                _hideMarks = _vimBufferData.LocalSettings.HideMarks;
                UpdateAllMarks();
            }
        }

        private void OnIsVisibleChanged(object sender, TextViewEventArgs e)
        {
            if (e.TextView == _vimBufferData.TextView)
            {
                _isVisible = _vimBufferData.Vim.VimHost.IsVisible(e.TextView);
                if (_isVisible)
                {
                    UpdateAllMarks();
                }
                else
                {
                    _glyphPairs.Clear();
                }
            }
        }

        private bool UpdateMark(Mark mark)
        {
            if (!_isVisible)
            {
                return false;
            }

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

        private void UpdateAllMarks()
        {
            if (!_isVisible)
            {
                return;
            }

            if (_lineNumberMap.Count == 0)
            {
                return;
            }

            foreach (var mark in _lineNumberMap.Keys.ToList())
            {
                UpdateMark(mark);
            }

            RaiseChanged();
        }

        private void RemoveMark(Mark mark)
        {
            _lineNumberMap.Remove(mark);
        }

        private void RaiseChanged()
        {
            if (!_isVisible)
            {
                return;
            }

            CachePairs();
            var snapshot = _vimBufferData.TextView.VisualSnapshot;
            var span = new SnapshotSpan(snapshot, 0, snapshot.Length);
            VimTrace.TraceInfo($"MarkGlyphTagger::RaiseChanged");
            _changedEvent?.Invoke(this, new SnapshotSpanEventArgs(span));
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
            VimTrace.TraceInfo($"MarkGlyphTagger: Glyph Pairs");
            foreach (var pair in _glyphPairs)
            {
                VimTrace.TraceInfo($"MarkGlyphTagger: {pair.Item2} -> {pair.Item1}");
            }
        }

        private IEnumerable<ITagSpan<MarkGlyphTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (_glyphPairs.Count == 0 || spans.Count == 0)
            {
                yield break;
            }

            if (spans[0].Snapshot.Version != _vimBufferData.TextView.VisualSnapshot.Version)
            {
                yield break;
            }

            VimTrace.TraceInfo($"MarkGlyphTagger::GetTags: starting...");
            foreach (var span in spans)
            {
                VimTrace.TraceInfo($"MarkGlyphTagger::GetTags: {span}");
                var snapshot = span.Snapshot;
                var list = new List<ITagSpan<MarkGlyphTag>>();
                foreach (var pair in _glyphPairs)
                {
                    var chars = pair.Item1;
                    var lineNumber = pair.Item2;

                    if (lineNumber < snapshot.LineCount)
                    {
                        var line = snapshot.GetLineFromLineNumber(lineNumber);
                        if (span.OverlapsWith(line.ExtentIncludingLineBreak))
                        {
                            var tag = new MarkGlyphTag(chars);
                            VimTrace.TraceInfo($"MarkGlyphTagger::GetTags: tag {lineNumber} {chars}");
                            yield return new TagSpan<MarkGlyphTag>(line.Extent, tag);
                        }
                    }
                }
            }
        }

        IEnumerable<ITagSpan<MarkGlyphTag>> ITagger<MarkGlyphTag>.GetTags(NormalizedSnapshotSpanCollection spans)
        {
            return GetTags(spans);
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<MarkGlyphTag>.TagsChanged
        {
            add { _changedEvent += value; }
            remove { _changedEvent -= value; }
        }

        void IDisposable.Dispose()
        {
            Dispose();
        }
    }
}
