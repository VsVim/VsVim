using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.Mac
{
    //TODO: This file is identical to Vim.UI.Wpf.Implementation.Misc.DisplayWindoBroker.cs

    /// <summary>
    /// Standard implementation of the IDisplayWindowBroker interface.  This acts as a single
    /// interface for the various completion window possibilities
    /// </summary>
    internal sealed class DisplayWindowBroker : IDisplayWindowBroker
    {
        private readonly ITextView _textView;
        private readonly ICompletionBroker _completionBroker;
        private readonly ISignatureHelpBroker _signatureHelpBroker;
        private readonly IQuickInfoBroker _quickInfoBroker;

        internal DisplayWindowBroker(
            ITextView textView,
            ICompletionBroker completionBroker,
            ISignatureHelpBroker signatureHelpBroker,
            IQuickInfoBroker quickInfoBroker)
        {
            _textView = textView;
            _completionBroker = completionBroker;
            _signatureHelpBroker = signatureHelpBroker;
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
        private static readonly object s_key = new object();

        private readonly ICompletionBroker _completionBroker;
        private readonly ISignatureHelpBroker _signatureHelpBroker;
        private readonly IQuickInfoBroker _quickInfoBroker;

        [ImportingConstructor]
        internal DisplayWindowBrokerFactoryService(
            ICompletionBroker completionBroker,
            ISignatureHelpBroker signatureHelpBroker,
            IQuickInfoBroker quickInfoBroker)
        {
            _completionBroker = completionBroker;
            _signatureHelpBroker = signatureHelpBroker;
            _quickInfoBroker = quickInfoBroker;
        }

        IDisplayWindowBroker IDisplayWindowBrokerFactoryService.GetDisplayWindowBroker(ITextView textView)
        {
            return textView.Properties.GetOrCreateSingletonProperty(
                s_key,
                () => new DisplayWindowBroker(
                        textView,
                        _completionBroker,
                        _signatureHelpBroker,
                        _quickInfoBroker));
        }
    }
}
