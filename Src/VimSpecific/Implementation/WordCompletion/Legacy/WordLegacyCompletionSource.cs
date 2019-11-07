using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Vim.VisualStudio.Specific.Implementation.WordCompletion.Legacy
{
    internal sealed class WordLegacyCompletionSource : ICompletionSource
    {
        /// <summary>
        /// The associated ITextBuffer 
        /// </summary>
        private readonly ITextBuffer _textBuffer;

        internal WordLegacyCompletionSource(ITextBuffer textBuffer)
        {
            _textBuffer = textBuffer;
        }

        /// <summary>
        /// Augment the completion session with the provided set of words if this completion session is 
        /// being created for a word completion session
        /// </summary>
        void ICompletionSource.AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            var textView = session.TextView;

            // Only provide completion information for the ITextBuffer directly associated with the 
            // ITextView.  In a projcetion secnario there will be several ITextBuffer instances associated
            // with a given ITextView and we provide ICompletionSource values for all of them.  We want to
            // avoid creating duplicate completion information
            if (textView.TextBuffer != _textBuffer)
            {
                return;
            }

            // Get out the collection of words.  If none is present then there is no information to
            // augment here
            if (!textView.TryGetWordCompletionData(out var completionData))
            {
                return;
            }

            var trackingSpan = completionData.WordSpan.Snapshot.CreateTrackingSpan(
                completionData.WordSpan.Span,
                SpanTrackingMode.EdgeInclusive);
            var completions = completionData.WordCollection.Select(word => new Completion(word));
            var wordCompletionSet = new WordLegacyCompletionSet(trackingSpan, completions);
            completionSets.Add(wordCompletionSet);
        }

        void IDisposable.Dispose()
        {
            // Nothing to dispose of
        }
    }
}
