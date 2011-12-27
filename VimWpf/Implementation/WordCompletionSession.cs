using System;
using Microsoft.FSharp.Control;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Extensions;

namespace Vim.UI.Wpf.Implementation
{
    /// <summary>
    /// Implementation of the IWordCompletionSession interface.  Wraps an ICompletionSession
    /// to provide the friendly interface the core Vim engine is expecting
    /// </summary>
    internal sealed class WordCompletionSession : IWordCompletionSession
    {
        private readonly ITextView _textView;
        private readonly ICompletionSession _completionSession;
        private readonly CompletionSet _wordCompletionSet;
        private readonly IIntellisenseSessionStack _intellisenseSessionStack;
        private readonly ITrackingSpan _wordTrackingSpan;
        private bool _isDismissed;
        private event EventHandler _dismissed;

        internal WordCompletionSession(ITrackingSpan wordTrackingSpan, IIntellisenseSessionStack intellisenseSessionStack, ICompletionSession completionSession, CompletionSet wordCompletionSet)
        {
            _textView = completionSession.TextView;
            _wordTrackingSpan = wordTrackingSpan;
            _wordCompletionSet = wordCompletionSet;
            _completionSession = completionSession;
            _completionSession.Dismissed += delegate { OnDismissed(); };
            _intellisenseSessionStack = intellisenseSessionStack;
        }

        /// <summary>
        /// Called when the session is dismissed.  Need to alert any consumers that we have been
        /// dismissed 
        /// </summary>
        private void OnDismissed()
        {
            _isDismissed = true;

            var dismissed = _dismissed;
            if (dismissed != null)
            {
                dismissed(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Send the command to the current session head
        /// </summary>
        internal bool SendCommand(IntellisenseKeyboardCommand command)
        {
            // Don't send the command if the active completion set is not the word completion
            // set
            if (_wordCompletionSet != _completionSession.SelectedCompletionSet)
            {
                return false;
            }

            var commandTarget = _intellisenseSessionStack as IIntellisenseCommandTarget;
            if (commandTarget == null)
            {
                return false;
            }

            // Send the command
            if (!commandTarget.ExecuteKeyboardCommand(command))
            {
                return false;
            }

            // Command succeeded so there is a new selection.  Put the new selection into the
            // ITextView to replace the current selection
            var wordSpan = TrackingSpanUtil.GetSpan(_textView.TextSnapshot, _wordTrackingSpan);
            if (wordSpan.IsSome() &&
                _wordCompletionSet.SelectionStatus != null &&
                _wordCompletionSet.SelectionStatus.Completion != null)
            {
                _textView.TextBuffer.Replace(wordSpan.Value, _wordCompletionSet.SelectionStatus.Completion.InsertionText);
            }

            return true;
        }

        #region IWordCompletionSession

        ITextView IWordCompletionSession.TextView
        {
            get { return _completionSession.TextView; }
        }

        bool IWordCompletionSession.IsDismissed
        {
            get { return _isDismissed; }
        }

        event EventHandler IWordCompletionSession.Dismissed
        {
            add { _dismissed += value; }
            remove { _dismissed -= value; }
        }

        void IWordCompletionSession.Dismiss()
        {
            _isDismissed = true;
            _completionSession.Dismiss();
        }

        bool IWordCompletionSession.MoveNext()
        {
            return SendCommand(IntellisenseKeyboardCommand.Down);
        }

        bool IWordCompletionSession.MovePrevious()
        {
            return SendCommand(IntellisenseKeyboardCommand.Up);
        }

        #endregion
    }
}
