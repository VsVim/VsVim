#if VS_SPECIFIC_2019
using System;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Extensions;
using System.Collections.Generic;
using Microsoft.VisualStudio.Utilities;
using Vim;

namespace VsSpecific.Implementation.WordCompletion
{
    /// <summary>
    /// Implementation of the IWordCompletionSession interface.  
    /// to provide the friendly interface the core Vim engine is expecting
    /// </summary>
    internal sealed class WordAsyncCompletionSession : IWordCompletionSession
    {
        private readonly ITextView _textView;
        private readonly IAsyncCompletionSession _asyncCompletionSession;
        private readonly ITrackingSpan _wordTrackingSpan;
        private bool _isDismissed;
        private event EventHandler _dismissed;

        internal WordAsyncCompletionSession(IAsyncCompletionSession asyncCompletionSession)
        {
            _textView = asyncCompletionSession.TextView;
            _asyncCompletionSession = asyncCompletionSession;
            _asyncCompletionSession.Dismissed += delegate { OnDismissed(); };
        }

        /// <summary>
        /// Called when the session is dismissed.  Need to alert any consumers that we have been
        /// dismissed 
        /// </summary>
        private void OnDismissed()
        {
            _isDismissed = true;

            _dismissed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Send the command to the current session head
        /// </summary>
        internal bool SendCommand(IntellisenseKeyboardCommand command)
        {
            return false;
            /*
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
            */
        }

        /// <summary>
        /// Move the selection up or down.  If we're at the end of the selection then wrap around to
        /// the other side of the list
        /// </summary>
        private bool MoveWithWrap(bool moveNext)
        {
            return false;
            /*
            var originalCompletion = _wordCompletionSet.SelectionStatus?.Completion;
            var ret = SendCommand(moveNext ? IntellisenseKeyboardCommand.Down : IntellisenseKeyboardCommand.Up);
            var currentCompletion = _wordCompletionSet.SelectionStatus?.Completion;
            if (originalCompletion != null && currentCompletion == originalCompletion)
            {
                ret = SendCommand(moveNext ? IntellisenseKeyboardCommand.TopLine : IntellisenseKeyboardCommand.BottomLine);
            }

            return ret;
            */
        }

        #region IWordCompletionSession

        ITextView IWordCompletionSession.TextView => _textView;
        bool IWordCompletionSession.IsDismissed => _isDismissed;
        event EventHandler IWordCompletionSession.Dismissed
        {
            add { _dismissed += value; }
            remove { _dismissed -= value; }
        }

        void IWordCompletionSession.Dismiss()
        {
            _isDismissed = true;
            _asyncCompletionSession.Dismiss();
        }

        bool IWordCompletionSession.MoveNext() => MoveWithWrap(moveNext: true);
        bool IWordCompletionSession.MovePrevious() => MoveWithWrap(moveNext: false);

        #endregion

        #region IPropertyOwner 

        PropertyCollection IPropertyOwner.Properties => _asyncCompletionSession.Properties;

        #endregion 
    }
}

#elif VS_SPECIFIC_2015 || VS_SPECIFIC_2017
// Nothing to do
#else
#error Unsupported configuration
#endif
