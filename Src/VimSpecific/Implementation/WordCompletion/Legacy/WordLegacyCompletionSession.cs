using System;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Extensions;
using System.Collections.Generic;
using Microsoft.VisualStudio.Utilities;

namespace Vim.VisualStudio.Specific.Implementation.WordCompletion.Legacy
{
    /// <summary>
    /// Implementation of the IWordCompletionSession interface.  Wraps an ICompletionSession
    /// to provide the friendly interface the core Vim engine is expecting
    /// </summary>
    internal sealed class WordLegacyCompletionSession : IWordCompletionSession
    {
        private readonly ITextView _textView;
        private readonly ICompletionSession _completionSession;
        private readonly IIntellisenseSessionStack _intellisenseSessionStack;
        private readonly ITrackingSpan _wordTrackingSpan;
        private readonly WordLegacyCompletionSet _wordCompletionSet;
        private bool _isDismissed;
        private event EventHandler _dismissed;

        internal WordLegacyCompletionSession(ITrackingSpan wordTrackingSpan, IIntellisenseSessionStack intellisenseSessionStack, ICompletionSession completionSession, WordLegacyCompletionSet wordCompletionSet)
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
            _dismissed?.Invoke(this, EventArgs.Empty);
            _textView.ClearWordCompletionData();
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

        /// <summary>
        /// Move the selection up or down.  If we're at the end of the selection then wrap around to
        /// the other side of the list
        /// </summary>
        private bool MoveWithWrap(bool moveNext)
        {
            var originalCompletion = _wordCompletionSet.SelectionStatus?.Completion;
            var ret = SendCommand(moveNext ? IntellisenseKeyboardCommand.Down : IntellisenseKeyboardCommand.Up);
            var currentCompletion = _wordCompletionSet.SelectionStatus?.Completion;
            if (originalCompletion != null && currentCompletion == originalCompletion)
            {
                ret = SendCommand(moveNext ? IntellisenseKeyboardCommand.TopLine : IntellisenseKeyboardCommand.BottomLine);
            }

            return ret;
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
            return MoveWithWrap(moveNext: true);
        }

        bool IWordCompletionSession.MovePrevious()
        {
            return MoveWithWrap(moveNext: false);
        }

        void IWordCompletionSession.Commit()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IPropertyOwner 

        PropertyCollection IPropertyOwner.Properties
        {
            get { return _completionSession.Properties; }
        }

        #endregion 
    }
}
