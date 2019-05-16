using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Vim.Implementation.WordCompletion.Legacy;

#if VS_SPECIFIC_2019
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Vim.Implementation.WordCompletion.Async;
#endif

namespace Vim.Implementation.WordCompletion
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
    internal sealed class WordCompletionSessionFactoryService : IWordCompletionSessionFactoryService
    {
#if VS_SPECIFIC_2019
        private readonly IAsyncCompletionBroker _asyncCompletionBroker;
        private readonly WordAsyncCompletionSessionFactoryService _asyncFactory;
        private readonly WordLegacyCompletionSessionFactoryService _legacyFactory;

        [ImportingConstructor]
        internal WordCompletionSessionFactoryService(
            IAsyncCompletionBroker asyncCompletionBroker,
            WordAsyncCompletionSessionFactoryService asyncFactory,
            WordLegacyCompletionSessionFactoryService legacyFactory)
        {
            _asyncCompletionBroker = asyncCompletionBroker;
            _asyncFactory = asyncFactory;
            _legacyFactory = legacyFactory;
        }

        private IWordCompletionSession CreateWordCompletionSession(ITextView textView, SnapshotSpan wordSpan, IEnumerable<string> wordCollection, bool isForward)
        {
            IWordCompletionSession session = _asyncCompletionBroker.IsCompletionSupported(textView.TextBuffer.ContentType)
                ? _asyncFactory.CreateWordCompletionSession(textView, wordSpan, wordCollection, isForward)
                : _legacyFactory.CreateWordCompletionSession(textView, wordSpan, wordCollection, isForward);
            RaiseCreated(session);
            return session;
        }

#elif VS_SPECIFIC_2015 || VS_SPECIFIC_2017

        private readonly WordLegacyCompletionSessionFactoryService _legacyFactory;

        [ImportingConstructor]
        internal WordCompletionSessionFactoryService(WordLegacyCompletionSessionFactoryService legacyFactory)
        {
            _legacyFactory = legacyFactory;
        }

        private IWordCompletionSession CreateWordCompletionSession(ITextView textView, SnapshotSpan wordSpan, IEnumerable<string> wordCollection, bool isForward)
        {
            IWordCompletionSession session = _legacyFactory.CreateWordCompletionSession(textView, wordSpan, wordCollection, isForward);
            RaiseCreated(session);
            return session;
        }

#else
#error Unsupported configuration
#endif

        private event EventHandler<WordCompletionSessionEventArgs> _createdEvent = delegate { };

        private void RaiseCreated(IWordCompletionSession wordCompletionSession)
        {
            var args = new WordCompletionSessionEventArgs(wordCompletionSession);
            _createdEvent(this, args);
        }

        internal static void AddCompositionTypes(List<Type> list)
        {
            list.Add(typeof(WordCompletionSessionFactoryService));
            list.Add(typeof(WordLegacyCompletionSessionFactoryService));

#if VS_SPECIFIC_2019
            list.Add(typeof(WordAsyncCompletionSessionFactoryService));
#endif
        }

        #region IWordCompletionSessionFactoryService

        IWordCompletionSession IWordCompletionSessionFactoryService.CreateWordCompletionSession(ITextView textView, SnapshotSpan wordSpan, IEnumerable<string> wordCollection, bool isForward)
        {
            return CreateWordCompletionSession(textView, wordSpan, wordCollection, isForward);
        }

        event EventHandler<WordCompletionSessionEventArgs> IWordCompletionSessionFactoryService.Created
        {
            add { _createdEvent += value; }
            remove { _createdEvent -= value; }
        }

        #endregion
    }
}

