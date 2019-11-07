using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.VisualStudio.Specific.Implementation.WordCompletion.Legacy
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
    internal sealed class WordLegacyCompletionSessionFactory
    {
        private readonly ICompletionBroker _completionBroker;
        private readonly IIntellisenseSessionStackMapService _intellisenseSessionStackMapService;

        /// <summary>
        /// This is inserted into every ICompletionSession property bag which is created for
        /// a word completion.  It's used to identify all ICompletionSession values which are 
        /// IWordCompletionSessions
        /// </summary>
        internal static object WordCompletionSessionKey = new object();

        internal WordLegacyCompletionSessionFactory(ICompletionBroker completionBroker, IIntellisenseSessionStackMapService intellisenseSessionStackMapService)
        {
            _completionBroker = completionBroker;
            _intellisenseSessionStackMapService = intellisenseSessionStackMapService;
        }

        internal FSharpOption<IWordCompletionSession> CreateWordCompletionSession(ITextView textView, SnapshotSpan wordSpan, IEnumerable<string> wordCollection, bool isForward)
        {
            var completionData = new VimWordCompletionData(
                wordSpan,
                new ReadOnlyCollection<string>(wordCollection.ToList()));
            textView.SetWordCompletionData(completionData);

            // Dismiss any active ICompletionSession instances.  It's possible and possibly common for 
            // normal intellisense to be active when the user invokes word completion.  We want only word
            // completion displayed at this point 
            foreach (var existingCompletionSession in _completionBroker.GetSessions(textView))
            {
                existingCompletionSession.Dismiss();
            }

            // Create a completion session at the start of the word.  The actual session information will 
            // take care of mapping it to a specific span
            var trackingPoint = textView.TextSnapshot.CreateTrackingPoint(wordSpan.Start, PointTrackingMode.Positive);
            var completionSession = _completionBroker.CreateCompletionSession(textView, trackingPoint, true);
            completionSession.Properties[WordCompletionSessionKey] = WordCompletionSessionKey;

            // Start the completion.  This will cause it to get populated at which point we can go about 
            // filtering the data
            completionSession.Start();

            // It's possible for the Start method to dismiss the ICompletionSession.  This happens when there
            // is an initialization error such as being unable to find a CompletionSet.  If this occurs we
            // just return the equivalent IWordCompletionSession (one which is dismissed)
            if (completionSession.IsDismissed)
            {
                return FSharpOption<IWordCompletionSession>.None;
            }

            // Now move the word completion set to the fron
            var wordCompletionSet = completionSession.CompletionSets.OfType<WordLegacyCompletionSet>().FirstOrDefault();
            if (wordCompletionSet == null)
            {
                wordCompletionSet = new WordLegacyCompletionSet();
            }
            completionSession.SelectedCompletionSet = wordCompletionSet;

            var intellisenseSessionStack = _intellisenseSessionStackMapService.GetStackForTextView(textView);
            var wordTrackingSpan = wordSpan.Snapshot.CreateTrackingSpan(wordSpan.Span, SpanTrackingMode.EdgeInclusive);
            var wordCompletionSession = new WordLegacyCompletionSession(
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

            return FSharpOption<IWordCompletionSession>.Some(wordCompletionSession);
        }
    }
}
