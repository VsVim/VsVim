#if VS_SPECIFIC_2019
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Vim.Extensions;

namespace VsSpecific.Implementation.WordCompletion.Async
{
    /// <summary>
    /// This type is responsible for providing word completion sessions over a given ITextView
    /// instance and given set of words.
    /// 
    /// Properly integrating with the IntelliSense stack here is a bit tricky.  In order to participate
    /// in any completion session you must provide an ICompletionSource for the lifetime of the 
    /// ITextView.  Ideally we don't want to provide any completion information unless we are actually
    /// starting a word completion session
    /// </summary>
    [Name("Vim Word Completion Session Factory Service")]
    [Export(typeof(IWordCompletionSessionFactoryService))]
    internal sealed class WordAsyncCompletionSessionFactoryService : IWordCompletionSessionFactoryService
    {
        /// <summary>
        /// Key used to hide the CompletionData in the ITextView
        /// </summary>
        private readonly object _completionDataKey = new object();
        private readonly IAsyncCompletionBroker _asyncCompletionBroker;

        private event EventHandler<WordCompletionSessionEventArgs> _createdEvent = delegate { };

        [ImportingConstructor]
        internal WordAsyncCompletionSessionFactoryService(IAsyncCompletionBroker asyncCompletionBroker)
        {
            _asyncCompletionBroker = asyncCompletionBroker;
        }

        private void RaiseCompleted(IWordCompletionSession wordCompletionSession)
        {
            var args = new WordCompletionSessionEventArgs(wordCompletionSession);
            _createdEvent(this, args);
        }

        #region IWordCompletionSessionFactoryService

        IWordCompletionSession IWordCompletionSessionFactoryService.CreateWordCompletionSession(ITextView textView, SnapshotSpan wordSpan, IEnumerable<string> wordCollection, bool isForward)
        {
            // Dismiss any active ICompletionSession instances.  It's possible and possibly common for 
            // normal intellisense to be active when the user invokes word completion.  We want only word
            // completion displayed at this point 
            _asyncCompletionBroker.GetSession(textView)?.Dismiss();

            // Store the WordCompletionData inside the ITextView. The IAsyncCompletionSource implementation will 
            // asked to provide data for the creation of the IAsyncCompletionSession. Hence we must share through
            // ITextView
            var wordCompletionData = new WordCompletionData(
                wordSpan,
                new ReadOnlyCollection<string>(wordCollection.ToList()));
            textView.SetWordCompletionData(wordCompletionData);

            // Create a completion session at the start of the word.  The actual session information will 
            // take care of mapping it to a specific span
            var completionTrigger = new CompletionTrigger(CompletionTriggerReason.Insertion, wordSpan.Snapshot);
            var asyncCompletionSession = _asyncCompletionBroker.TriggerCompletion(
                textView,
                completionTrigger,
                wordSpan.Start,
                CancellationToken.None);

            // It's possible for the Start method to dismiss the ICompletionSession.  This happens when there
            // is an initialization error such as being unable to find a CompletionSet.  If this occurs we
            // just return the equivalent IWordCompletionSession (one which is dismissed)
            if (asyncCompletionSession.IsDismissed)
            {
                return new DismissedWordCompletionSession(textView);
            }

            asyncCompletionSession.OpenOrUpdate(completionTrigger, wordSpan.Start, CancellationToken.None);
            return new WordAsyncCompletionSession(asyncCompletionSession);
            /*

            var wordCompletionSession = new WordAsyncompletionSession();
            // Now move the word completion set to the fron
            var wordCompletionSet = completionSession.CompletionSets.OfType<WordCompletionSet>().FirstOrDefault();
            if (wordCompletionSet == null)
            {
                wordCompletionSet = new WordCompletionSet();
            }
            completionSession.SelectedCompletionSet = wordCompletionSet;

            var intellisenseSessionStack = _intellisenseSessionStackMapService.GetStackForTextView(textView);
            var wordTrackingSpan = wordSpan.Snapshot.CreateTrackingSpan(wordSpan.Span, SpanTrackingMode.EdgeInclusive);
            var wordCompletionSession = new WordCompletionSession(
                wordTrackingSpan,
                intellisenseSessionStack,
                completionSession,
                wordCompletionSet);

            // Ensure the correct item is selected and committed to the ITextBuffer.  If this is a forward completion
            // then we select the first item, else the last.  Sending the command will go ahead and insert the 
            // completion in the given span
            var command = isForward ? IntellisenseKeyboardCommand.TopLine : IntellisenseKeyboardCommand.BottomLine;
            wordCompletionSession.SendCommand(command);

            // For reasons I don't understand, if the command is 'bottom
            // line', it doesn't seem to take effect on the first try.
            wordCompletionSession.SendCommand(command);

            RaiseCompleted(wordCompletionSession);

            return wordCompletionSession;
            */
        }

        event EventHandler<WordCompletionSessionEventArgs> IWordCompletionSessionFactoryService.Created
        {
            add { _createdEvent += value; }
            remove { _createdEvent -= value; }
        }

        #endregion
    }
}

#elif VS_SPECIFIC_2015 || VS_SPECIFIC_2017
// Nothing to do
#else
#error Unsupported configuration
#endif
