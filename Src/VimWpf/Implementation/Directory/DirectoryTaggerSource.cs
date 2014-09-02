using EditorUtils;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.Text.Classification;

namespace Vim.UI.Wpf.Implementation.Directory
{
    internal sealed class DirectoryTaggerSource : IBasicTaggerSource<IClassificationTag>
    {
        private readonly ITextBuffer _textBuffer;
        private readonly IClassificationTag _classificationTag;
        private EventHandler _changed;

        internal DirectoryTaggerSource(ITextBuffer textBuffer, IClassificationType classificationType)
        {
            _textBuffer = textBuffer;
            _classificationTag = new ClassificationTag(classificationType);
        }

        private ReadOnlyCollection<ITagSpan<IClassificationTag>> GetTags(SnapshotSpan span)
        {
            var lineSpan = SnapshotLineRangeUtil.CreateForSpan(span);
            var snapshot = span.Snapshot;
            var list = new List<ITagSpan<IClassificationTag>>();
            foreach (var line in lineSpan.Lines)
            {
                SnapshotSpan directorySpan;
                if (DirectoryUtil.TryGetDirectorySpan(line, out directorySpan))
                {
                    list.Add(new TagSpan<IClassificationTag>(directorySpan, _classificationTag));
                }
            }

            return list.ToReadOnlyCollectionShallow();
        }

        #region IBasicTaggerSource<TextMarkerTag>

        event EventHandler IBasicTaggerSource<IClassificationTag>.Changed
        {
            add { _changed += value; }
            remove { _changed -= value; }
        }

        ReadOnlyCollection<ITagSpan<IClassificationTag>> IBasicTaggerSource<IClassificationTag>.GetTags(SnapshotSpan span)
        {
            return GetTags(span);
        }

        #endregion
    }
}
