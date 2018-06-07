using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vim.Extensions;

namespace Vim.UI.Wpf.Implementation.MarkGlyph
{
    internal sealed class MarkGlyphTaggerSource : IBasicTaggerSource<MarkGlyphTag>, IDisposable
    {
        static ReadOnlyCollection<ITagSpan<MarkGlyphTag>> s_emptyTagList = new ReadOnlyCollection<ITagSpan<MarkGlyphTag>>(new List<ITagSpan<MarkGlyphTag>>());

        private readonly IVimBufferData _vimBufferData;
        private readonly IMarkMap _markMap;

        private EventHandler _changedEvent;

        private Mark _currentMark = Mark.NewLocalMark(LocalMark.NewLetter(Letter.A));

        internal MarkGlyphTaggerSource(IVimBufferData vimBufferData)
        {
            _vimBufferData = vimBufferData;
            _markMap = _vimBufferData.Vim.MarkMap;
            _markMap.MarkChanged += OnMarkChanged;
        }

        private void Dispose()
        {
            _markMap.MarkChanged -= OnMarkChanged;
        }

        private void OnMarkChanged(object sender, EventArgs args)
        {
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            _changedEvent?.Invoke(this, EventArgs.Empty);
        }

        internal ReadOnlyCollection<ITagSpan<MarkGlyphTag>> GetTags(SnapshotSpan span)
        {
            if (_currentMark != null)
            {
                var virtualPoint = _markMap.GetMark(_currentMark, _vimBufferData);
                if (virtualPoint.IsSome())
                {
                    var point = virtualPoint.Value.Position;
                    if (point.Snapshot == span.Snapshot)
                    {
                        var tag = new MarkGlyphTag(_currentMark.Char);
                        var list = new List<ITagSpan<MarkGlyphTag>>();
                        var tagSpan = new TagSpan<MarkGlyphTag>(new SnapshotSpan(point, 0), tag);
                        list.Add(tagSpan);
                        return list.ToReadOnlyCollectionShallow();
                    }
                }
            }
            return s_emptyTagList;
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
