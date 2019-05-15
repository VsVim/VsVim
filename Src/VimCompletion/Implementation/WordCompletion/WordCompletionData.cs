using Microsoft.VisualStudio.Text;
using System.Collections.ObjectModel;

namespace Vim.Implementation.WordCompletion
{
    /// <summary>
    /// Information about the completion which needs to be passed around between the 
    /// various components
    /// </summary>
    internal sealed class WordCompletionData
    {
        internal readonly SnapshotSpan WordSpan;
        internal readonly ReadOnlyCollection<string> WordCollection;

        internal WordCompletionData(SnapshotSpan wordSpan, ReadOnlyCollection<string> wordCollection)
        {
            WordSpan = wordSpan;
            WordCollection = wordCollection;
        }
    }
}
