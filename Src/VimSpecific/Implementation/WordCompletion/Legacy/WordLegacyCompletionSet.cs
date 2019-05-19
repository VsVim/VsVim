using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vim.VisualStudio.Specific.Implementation.WordCompletion.Legacy
{
    internal sealed class WordLegacyCompletionSet : CompletionSet
    {
        internal const string Name = "Words";

        internal WordLegacyCompletionSet()
        {
        }

        internal WordLegacyCompletionSet(ITrackingSpan wordTrackingSpan, IEnumerable<Completion> completions)
            : base(Name, Name, wordTrackingSpan, completions, null)
        {
        }

        /// <summary>
        /// For a word completion set there is no best match.  This is called very often by the the various
        /// pieces of the intellisense stack to select the best match based on the current data in the
        /// ITextBuffer.  It's meant to filter as the user types.  We don't want any of that behavior in 
        /// the word completion scenario
        /// </summary>
        public override void SelectBestMatch()
        {
        }
    }
}
