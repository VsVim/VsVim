using System;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace EditorUtils
{
    public interface IBasicTaggerSource<TTag>
        where TTag : ITag
    {
        /// <summary>
        /// The current ITextSnaphot for the buffer
        /// </summary>
        ITextSnapshot TextSnapshot { get; }

        /// <summary>
        /// Get the tags for the given SnapshotSpan
        /// </summary>
        ReadOnlyCollection<ITagSpan<TTag>> GetTags(SnapshotSpan span);

        /// <summary>
        /// Raised when the source changes in some way
        /// </summary>
        event EventHandler Changed;
    }
}
