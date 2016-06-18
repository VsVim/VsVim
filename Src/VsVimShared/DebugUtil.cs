using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

#if DEBUG
namespace Vim.VisualStudio
{
    /// <summary>
    /// This type exists purely to help during active debugging of VsVim.
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(VimConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class DebugUtil : IWpfTextViewCreationListener
    {
        private readonly ICompletionBroker _completionBroker;
        private readonly HashSet<ICompletionSession> _trackedSessions = new HashSet<ICompletionSession>();
        private readonly HashSet<IWpfTextView> _trackedTextViews = new HashSet<IWpfTextView>();

        [ImportingConstructor]
        internal DebugUtil(ICompletionBroker completionBroker)
        {
            _completionBroker = completionBroker;
        }

        private void OnTextViewCreated(IWpfTextView textView)
        {
            if (!_trackedTextViews.Add(textView))
            {
                return;
            }

            EventHandler<TextContentChangedEventArgs> textViewChanged = delegate { OnTextViewChanged(textView); };
            EventHandler textViewClosed = null;

            textViewClosed = delegate
            {
                textView.Closed -= textViewClosed;
                textView.TextBuffer.Changed -= textViewChanged;
                _trackedTextViews.Remove(textView);
            };

            textView.TextBuffer.Changed += textViewChanged;
            textView.Closed += textViewClosed;
        }

        private void OnTextViewChanged(IWpfTextView textView)
        {
            foreach (var session in _completionBroker.GetSessions(textView))
            {
                if (!_trackedSessions.Add(session))
                {
                    continue;
                }

                EventHandler dismissed = null;
                EventHandler committed = null;

                committed = delegate
                {
                    _trackedSessions.Remove(session);
                    session.Dismissed -= dismissed;
                    session.Committed -= committed;
                };
                
                dismissed = delegate 
                {
                    OnCompletionSessionDismissed(session);
                    committed(this, EventArgs.Empty);
                };

                session.Dismissed += dismissed;
                session.Committed += committed;
            }
        }

        private void OnCompletionSessionDismissed(ICompletionSession session)
        {

        }

        #region IWfTextViewCreationListener

        void IWpfTextViewCreationListener.TextViewCreated(IWpfTextView textView)
        {
            OnTextViewCreated(textView);
        }

        #endregion
    }
}
#endif
