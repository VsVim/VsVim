using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using EditorUtils;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation.WordCompletion
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
    [Name("Word Completion Session Factory Service")]
    [ContentType(Constants.AnyContentType)]
    [Export(typeof(IWordCompletionSessionFactoryService))]
    [Export(typeof(ICompletionSourceProvider))]
    internal sealed class WordCompletionSessionFactoryService : IWordCompletionSessionFactoryService, ICompletionSourceProvider
    {
        #region CompletionData

        /// <summary>
        /// Information about the completion which needs to be passed around between the 
        /// various components
        /// </summary>
        private struct CompletionData
        {
            internal readonly SnapshotSpan WordSpan;
            internal readonly ReadOnlyCollection<string> WordCollection;

            internal CompletionData(SnapshotSpan wordSpan, ReadOnlyCollection<string> wordCollection)
            {
                WordSpan = wordSpan;
                WordCollection = wordCollection;
            }
        }

        #endregion

        #region CompletionSource

        private sealed class CompletionSource : ICompletionSource
        {
            /// <summary>
            /// Key in which the completion information is stored
            /// </summary>
            private readonly object _completionDataKey;

            /// <summary>
            /// The associated ITextBuffer 
            /// </summary>
            private readonly ITextBuffer _textBuffer;

            internal CompletionSource(ITextBuffer textBuffer, object completionDataKey)
            {
                _completionDataKey = completionDataKey;
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
                CompletionData completionData;
                if (!textView.Properties.TryGetPropertySafe(_completionDataKey, out completionData) || completionData.WordCollection == null)
                {
                    return;
                }

                var trackingSpan = completionData.WordSpan.Snapshot.CreateTrackingSpan(
                    completionData.WordSpan.Span,
                    SpanTrackingMode.EdgeInclusive);
                var completions = completionData.WordCollection.Select(word => new Completion(word));
                var wordCompletionSet = new WordCompletionSet(trackingSpan, completions);
                completionSets.Add(wordCompletionSet);
            }

            void IDisposable.Dispose()
            {
                // Nothing to dispose of
            }
        }

        #endregion

        #region DismissedWordCompletionSession

        /// <summary>
        /// An IWordCompletionSession which starts out dismissed.
        /// </summary>
        private sealed class DismissedWordCompletionSession : IWordCompletionSession
        {
            private readonly ITextView _textView;

            internal DismissedWordCompletionSession(ITextView textView)
            {
                _textView = textView;
            }

            void IWordCompletionSession.Dismiss()
            {
            }

            event EventHandler IWordCompletionSession.Dismissed
            {
                add { }
                remove { }
            }

            bool IWordCompletionSession.IsDismissed
            {
                get { return true; }
            }

            bool IWordCompletionSession.MoveNext()
            {
                return false;
            }

            bool IWordCompletionSession.MovePrevious()
            {
                return false;
            }

            ITextView IWordCompletionSession.TextView
            {
                get { return _textView; }
            }
        }

        #endregion

        /// <summary>
        /// Key used to hide the CompletionData in the ITextView
        /// </summary>
        private readonly object _completionDataKey = new object();

        private readonly ICompletionBroker _completionBroker;
        private readonly IIntellisenseSessionStackMapService _intellisenseSessionStackMapService;

        /// <summary>
        /// This is inserted into every ICompletionSession property bag which is created for
        /// a word completion.  It's used to identify all ICompletionSession values which are 
        /// IWordCompletionSessions
        /// </summary>
        internal static object WordCompletionSessionKey = new object();

        [ImportingConstructor]
        internal WordCompletionSessionFactoryService(ICompletionBroker completionBroker, IIntellisenseSessionStackMapService intellisenseSessionStackMapService)
        {
            _completionBroker = completionBroker;
            _intellisenseSessionStackMapService = intellisenseSessionStackMapService;
        }

        #region IWordCompletionSessionFactoryService

        IWordCompletionSession IWordCompletionSessionFactoryService.CreateWordCompletionSession(ITextView textView, SnapshotSpan wordSpan, IEnumerable<string> wordCollection, bool isForward)
        {
            var completionData = new CompletionData(
                wordSpan,
                new ReadOnlyCollection<string>(wordCollection.ToList()));
            textView.Properties[_completionDataKey] = completionData;
            try
            {
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
                    return new DismissedWordCompletionSession(textView);
                }

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

                return wordCompletionSession;
            }
            finally
            {
                textView.Properties.RemoveProperty(_completionDataKey);
            }
        }

        #endregion

        #region ICompletionSourceProvider

        ICompletionSource ICompletionSourceProvider.TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            return new CompletionSource(textBuffer, _completionDataKey);
        }

        #endregion

    }
}
