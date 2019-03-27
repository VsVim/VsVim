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
using System.Threading;

namespace VsSpecific.Implementation.WordCompletion.Async
{
    /// <summary>
    /// Implementation of the IWordCompletionSession interface.  
    /// to provide the friendly interface the core Vim engine is expecting
    /// </summary>
    internal sealed class WordAsyncCompletionSession : IWordCompletionSession
    {
        private readonly ITextView _textView;
        private readonly IAsyncCompletionSession _asyncCompletionSession;
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
            _textView.ClearWordCompletionData();
            _dismissed?.Invoke(this, EventArgs.Empty);
        }

        private bool WithOperations(Action<IAsyncCompletionSessionOperations> action)
        {
            if (_asyncCompletionSession is IAsyncCompletionSessionOperations operations)
            {
                action(operations);
                return true;
            }

            return false;
        }

        private bool MoveDown() => WithOperations(operations => operations.SelectDown());
        private bool MoveUp() => WithOperations(operations => operations.SelectUp());
        private void Commit() => _asyncCompletionSession.Commit(KeyInputUtil.EnterKey.Char, CancellationToken.None);

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

        bool IWordCompletionSession.MoveNext() => MoveDown();
        bool IWordCompletionSession.MovePrevious() => MoveUp();
        void IWordCompletionSession.Commit() => Commit();

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
