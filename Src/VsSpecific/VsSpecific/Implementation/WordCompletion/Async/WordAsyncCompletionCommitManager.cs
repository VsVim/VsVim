#if VS_SPECIFIC_2019
using System;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Extensions;
using System.Collections.Generic;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using System.Threading;

namespace VsSpecific.Implementation.WordCompletion.Async
{
    internal sealed class WordAsyncCompletionCommitManager : IAsyncCompletionCommitManager
    {
        private readonly ITextView _textView;

        internal WordAsyncCompletionCommitManager(ITextView textView)
        {
            _textView = textView;
        }

        internal bool ShouldCommitCompletion(IAsyncCompletionSession session, SnapshotPoint location, char typedChar, CancellationToken token)
        {
            return false;     
        }

        internal CommitResult TryCommit(IAsyncCompletionSession session, ITextBuffer buffer, CompletionItem item, char typedChar, CancellationToken token)
        {
            return CommitResult.Unhandled;
        }

        #region IAsyncCompletionCommitManager

        IEnumerable<char> IAsyncCompletionCommitManager.PotentialCommitCharacters => Array.Empty<char>();

        bool IAsyncCompletionCommitManager.ShouldCommitCompletion(IAsyncCompletionSession session, SnapshotPoint location, char typedChar, CancellationToken token) =>
            ShouldCommitCompletion(session, location, typedChar, token);

        CommitResult IAsyncCompletionCommitManager.TryCommit(IAsyncCompletionSession session, ITextBuffer buffer, CompletionItem item, char typedChar, CancellationToken token) =>
            TryCommit(session, buffer, item, typedChar, token);

        #endregion
    }
}

#elif VS_SPECIFIC_2015 || VS_SPECIFIC_2017
// Nothing to do
#else
#error Unsupported configuration
#endif
