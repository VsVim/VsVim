using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using Vim;

namespace Vim.Implementation.WordCompletion
{
    /// <summary>
    /// An IWordCompletionSession which starts out dismissed.
    /// </summary>
    internal sealed class DismissedWordCompletionSession : IWordCompletionSession
    {
        private readonly ITextView _textView;
        private readonly PropertyCollection _properties = new PropertyCollection();

        internal DismissedWordCompletionSession(ITextView textView)
        {
            _textView = textView;
        }

        void IWordCompletionSession.Dismiss()
        {
        }

        event EventHandler IWordCompletionSession.Dismissed
        {
            add { }
            remove { }
        }

        bool IWordCompletionSession.IsDismissed
        {
            get { return true; }
        }

        bool IWordCompletionSession.MoveNext()
        {
            return false;
        }

        bool IWordCompletionSession.MovePrevious()
        {
            return false;
        }

        void IWordCompletionSession.Commit()
        {
        }

        ITextView IWordCompletionSession.TextView
        {
            get { return _textView; }
        }

        PropertyCollection IPropertyOwner.Properties
        {
            get { return _properties; }
        }
    }
}
