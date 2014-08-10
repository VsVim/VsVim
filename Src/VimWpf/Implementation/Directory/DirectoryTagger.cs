using EditorUtils;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using System.Collections.ObjectModel;

namespace Vim.UI.Wpf.Implementation.Directory
{
    internal sealed class DirectoryTagger : IBasicTaggerSource<TextMarkerTag>
    {
        private readonly ITextBuffer _textBuffer;
        private readonly TextMarkerTag _textMarkerTag = new TextMarkerTag(VimWpfConstants.DirectoryFormatDefinitionName);
        private EventHandler _changed;

        internal DirectoryTagger(ITextBuffer textBuffer)
        {
            _textBuffer = textBuffer;
        }

        private ReadOnlyCollection<ITagSpan<TextMarkerTag>> GetTags(SnapshotSpan span)
        {
            var lineSpan = SnapshotLineRangeUtil.CreateForSpan(span);
            var snapshot = span.Snapshot;
            var list = new List<ITagSpan<TextMarkerTag>>();
            foreach (var line in lineSpan.Lines)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                if (snapshot[line.End.Position - 1] == '/')
                {
                    var directorySpan = new SnapshotSpan(line.Start, line.Length - 1);
                    list.Add(new TagSpan<TextMarkerTag>(directorySpan, _textMarkerTag));
                }
            }

            return list.ToReadOnlyCollectionShallow();
        }

        #region IBasicTaggerSource<TextMarkerTag>

        ITextSnapshot IBasicTaggerSource<TextMarkerTag>.TextSnapshot
        {
            get { return _textBuffer.CurrentSnapshot; }
        }

        event EventHandler IBasicTaggerSource<TextMarkerTag>.Changed
        {
            add { _changed += value; }
            remove { _changed -= value; }
        }

        ReadOnlyCollection<ITagSpan<TextMarkerTag>> IBasicTaggerSource<TextMarkerTag>.GetTags(SnapshotSpan span)
        {
            return GetTags(span);
        }

        #endregion
    }
}
