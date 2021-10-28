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
namespace Vim.UI.Wpf.Implementation.Misc
{
    /// <summary>
    /// This type exists purely to help during active debugging of VsVim.
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(VimConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class DebugUtil : IWpfTextViewCreationListener
    {
        private readonly object _trackedKey = new object();
        private readonly ICompletionBroker _completionBroker;
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

            textView.Selection.SelectionChanged += delegate { OnSelectionChanged(textView); };
            textView.TextBuffer.Changed += delegate { OnTextViewChanged(textView); };
            textView.Closed += delegate { _trackedTextViews.Remove(textView); };
        }

        private void OnTextViewChanged(IWpfTextView textView)
        {
            foreach (var session in _completionBroker.GetSessions(textView))
            {
                if (session.Properties.ContainsProperty(_trackedKey))
                {
                    continue;
                }

                session.Properties.AddProperty(_trackedKey, null);
                session.Dismissed += delegate { OnCompletionSessionDismissed(session); };
            }
        }

        private void Use<T>(T p)
        {

        }

        private void OnSelectionChanged(IWpfTextView textView)
        {
            var text = textView
                .Selection
                .SelectedSpans
                .Select(x => x.GetText())
                .Aggregate((x, y) => x + y);
            Use(text);
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
