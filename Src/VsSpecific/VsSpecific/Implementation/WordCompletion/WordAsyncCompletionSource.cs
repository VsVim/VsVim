#if VS_SPECIFIC_2019
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VsSpecific.Implementation.WordCompletion
{
    internal sealed class WordAsyncCompletionSource : IAsyncCompletionSource
    {
        internal ITextView TextView { get; } 

        internal WordAsyncCompletionSource(ITextView textView)
        {
            TextView = textView;
        }

        Task<CompletionContext> IAsyncCompletionSource.GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
        {
            var items = ImmutableArray.Create(new CompletionItem("test", this));
            var context = new CompletionContext(items);
            return Task.FromResult(context);
        }

        Task<object> IAsyncCompletionSource.GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
        {
            return Task.FromResult<object>("here");
        }

        CompletionStartData IAsyncCompletionSource.InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
        {
            return CompletionStartData.ParticipatesInCompletionIfAny;
        }
    }
}

#elif VS_SPECIFIC_2015 || VS_SPECIFIC_2017
// Nothing to do
#else
#error Unsupported configuration
#endif
