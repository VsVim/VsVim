using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf.Implementation.Misc
{
    /// <summary>
    /// Standard implementation of the IDisplayWindowBroker interface.  This acts as a single
    /// interface for the various completion window possibilities
    /// </summary>
    internal sealed class DisplayWindowBroker : IDisplayWindowBroker
    {
        private readonly ITextView _textView;
        private readonly ICompletionBroker _completionBroker;
        private readonly ISignatureHelpBroker _signatureHelpBroker;
        private readonly ISmartTagBroker _smartTagBroker;
        private readonly IQuickInfoBroker _quickInfoBroker;

        internal DisplayWindowBroker(
            ITextView textView,
            ICompletionBroker completionBroker,
            ISignatureHelpBroker signatureHelpBroker,
            ISmartTagBroker smartTagBroker,
            IQuickInfoBroker quickInfoBroker)
        {
            _textView = textView;
            _completionBroker = completionBroker;
            _signatureHelpBroker = signatureHelpBroker;
            _smartTagBroker = smartTagBroker;
            _quickInfoBroker = quickInfoBroker;
        }

        #region IDisplayWindowBroker

        bool IDisplayWindowBroker.IsCompletionActive
        {
            get { return _completionBroker.IsCompletionActive(_textView); }
        }

        bool IDisplayWindowBroker.IsQuickInfoActive
        {
            get { return _quickInfoBroker.IsQuickInfoActive(_textView); }
        }

        bool IDisplayWindowBroker.IsSignatureHelpActive
        {
            get { return _signatureHelpBroker.IsSignatureHelpActive(_textView); }
        }

        bool IDisplayWindowBroker.IsSmartTagSessionActive
        {
            get
            {
                if (_smartTagBroker.IsSmartTagActive(_textView))
                {
                    foreach (var session in _smartTagBroker.GetSessions(_textView))
                    {
                        if (session.State == SmartTagState.Expanded)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        ITextView IDisplayWindowBroker.TextView
        {
            get { return _textView; }
        }

        void IDisplayWindowBroker.DismissDisplayWindows()
        {
            if (_completionBroker.IsCompletionActive(_textView))
            {
                _completionBroker.DismissAllSessions(_textView);
            }

            if (_signatureHelpBroker.IsSignatureHelpActive(_textView))
            {
                _signatureHelpBroker.DismissAllSessions(_textView);
            }

            if (_quickInfoBroker.IsQuickInfoActive(_textView))
            {
                foreach (var session in _quickInfoBroker.GetSessions(_textView))
                {
                    session.Dismiss();
                }
            }
        }

        #endregion
    }

    [Export(typeof(IDisplayWindowBrokerFactoryService))]
    internal sealed class DisplayWindowBrokerFactoryService : IDisplayWindowBrokerFactoryService
    {
        private static readonly object Key = new object();

        private readonly ICompletionBroker _completionBroker;
        private readonly ISignatureHelpBroker _signatureHelpBroker;
        private readonly ISmartTagBroker _smartTagBroker;
        private readonly IQuickInfoBroker _quickInfoBroker;

        [ImportingConstructor]
        internal DisplayWindowBrokerFactoryService(
            ICompletionBroker completionBroker,
            ISignatureHelpBroker signatureHelpBroker,
            ISmartTagBroker smartTagBroker,
            IQuickInfoBroker quickInfoBroker)
        {
            _completionBroker = completionBroker;
            _signatureHelpBroker = signatureHelpBroker;
            _smartTagBroker = smartTagBroker;
            _quickInfoBroker = quickInfoBroker;
        }

        IDisplayWindowBroker IDisplayWindowBrokerFactoryService.GetDisplayWindowBroker(ITextView textView)
        {
            return textView.Properties.GetOrCreateSingletonProperty(
                Key,
                () => new DisplayWindowBroker(
                        textView,
                        _completionBroker,
                        _signatureHelpBroker,
                        _smartTagBroker,
                        _quickInfoBroker));
        }
    }

}
