using Microsoft.VisualStudio.Text;
using System.Collections.ObjectModel;

namespace Vim.VisualStudio.Specific.Implementation.WordCompletion
{
    /// <summary>
    /// Information about the completion which needs to be passed around between the 
    /// various components
    /// </summary>
    internal sealed class VimWordCompletionData
    {
        internal readonly SnapshotSpan WordSpan;
        internal readonly ReadOnlyCollection<string> WordCollection;

        internal VimWordCompletionData(SnapshotSpan wordSpan, ReadOnlyCollection<string> wordCollection)
        {
            WordSpan = wordSpan;
            WordCollection = wordCollection;
        }
    }
}
