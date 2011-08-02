﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation
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
    [ContentType(Constants.ContentType)]
    [Export(typeof(IWordCompletionSessionFactoryService))]
    [Export(typeof(ICompletionSourceProvider))]
    internal sealed class WordCompletionSessionFactoryService : IWordCompletionSessionFactoryService, ICompletionSourceProvider
    {
        private const string WordCompletionSetName = "Word Completion";

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
                if (!textView.Properties.TryGetProperty(_completionDataKey, out completionData) || completionData.WordCollection == null)
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

        #region WordCompletionSet

        private sealed class WordCompletionSet : CompletionSet
        {
            internal WordCompletionSet(ITrackingSpan wordTrackingSpan, IEnumerable<Completion> completions)
                : base(WordCompletionSetName, WordCompletionSetName, wordTrackingSpan, completions, null)
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

        #endregion

        /// <summary>
        /// Key used to hide the CompletionData in the ITextView
        /// </summary>
        private readonly object _completionDataKey = new object();

        private readonly ICompletionBroker _completionBroker;
        private readonly IIntellisenseSessionStackMapService _intellisenseSessionStackMapService;

        [ImportingConstructor]
        internal WordCompletionSessionFactoryService(ICompletionBroker completionBroker, IIntellisenseSessionStackMapService intellisenseSessionStackMapService)
        {
            _completionBroker = completionBroker;
            _intellisenseSessionStackMapService = intellisenseSessionStackMapService;
        }

        #region IWordCompletionSession

        IWordCompletionSession IWordCompletionSessionFactoryService.CreateWordCompletionSession(ITextView textView, SnapshotSpan wordSpan, IEnumerable<string> wordCollection, bool isForward)
        {
            var completionData = new CompletionData(
                wordSpan,
                new ReadOnlyCollection<string>(wordCollection.ToList()));
            textView.Properties[_completionDataKey] = completionData;
            try
            {
                // Create a completion session at the start of the word.  The actual session information will 
                // take care of mapping it to a specific span
                var trackingPoint = textView.TextSnapshot.CreateTrackingPoint(wordSpan.Start, PointTrackingMode.Positive);
                var completionSession = _completionBroker.CreateCompletionSession(textView, trackingPoint, true);

                // Start the completion.  This will cause it to get populated at which point we can go about 
                // filtering the data
                completionSession.Start();

                // Now move the word completion set to the fron
                var wordCompletionSet = completionSession.CompletionSets.FirstOrDefault(x => x.Moniker == WordCompletionSetName);
                if (wordCompletionSet == null)
                {
                    wordCompletionSet = new CompletionSet();
                }

                completionSession.SelectedCompletionSet = wordCompletionSet;

                var wordTrackingSpan = wordSpan.Snapshot.CreateTrackingSpan(wordSpan.Span, SpanTrackingMode.EdgeInclusive);
                var wordCompletionSession = new WordCompletionSession(
                    wordTrackingSpan,
                    _intellisenseSessionStackMapService.GetStackForTextView(textView),
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
