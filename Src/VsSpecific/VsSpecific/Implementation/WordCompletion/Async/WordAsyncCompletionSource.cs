#if VS_SPECIFIC_2019
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VsSpecific.Implementation.WordCompletion.Async
{
    internal sealed class WordAsyncCompletionSource : IAsyncCompletionSource
    {
        /// <summary>
        /// This is inserted into every <see cref="IAsyncCompletionSession"/> property bag which is created for
        /// a word completion.  It's used to pass the <see cref="WordCompletionData"/> instance to the 
        /// <see cref="WordAsyncCompletionSession"/>
        /// </summary>
        internal static object WordCompletionDataSessionKey = new object();

        internal ITextView TextView { get; } 

        internal WordAsyncCompletionSource(ITextView textView)
        {
            TextView = textView;
        }

        Task<CompletionContext> IAsyncCompletionSource.GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
        {
            CompletionContext context;
            if (session.Properties.TryGetProperty(WordCompletionDataSessionKey, out WordCompletionData wordCompletionData))
            {
                var itemsRaw = wordCompletionData.WordCollection.Select(x => new CompletionItem(x, this)).ToArray();
                var items = ImmutableArray.Create<CompletionItem>(itemsRaw);
                context = new CompletionContext(items);
            }
            else
            {
                context = CompletionContext.Empty;
            }

            return Task.FromResult(context);
        }

        Task<object> IAsyncCompletionSource.GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token) =>
            Task.FromResult<object>(item.DisplayText);

        CompletionStartData IAsyncCompletionSource.InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token) =>
            CompletionStartData.ParticipatesInCompletionIfAny;
    }
}

#elif VS_SPECIFIC_2015 || VS_SPECIFIC_2017
// Nothing to do
#else
#error Unsupported configuration
#endif
