using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UnitTest.Exports
{
    /// <summary>
    /// IVimErrorDetector MEF component.  Useful in tracking down errors which are silently
    /// swallowed by the editor infrastructure
    /// </summary>
    [Export(typeof(IExtensionErrorHandler))]
    [Export(typeof(IVimErrorDetector))]
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("any")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    public sealed class VimErrorDetector : IVimErrorDetector, IWpfTextViewCreationListener
    {
        private readonly List<Exception> _errorList = new List<Exception>();
        private readonly List<ITextView> _activeTextViewList = new List<ITextView>();

        internal VimErrorDetector()
        {

        }

        private void CheckForOrphanedUndoHistory()
        {
            foreach (var textView in _activeTextViewList)
            {
                CheckForOrphanedUndoHistory(textView);
            }
        }

        /// <summary>
        /// Make sure that on tear down we don't have a current transaction.  Having one indicates
        /// we didn't close it and hence are killing undo in the ITextBuffer
        /// </summary>
        private void CheckForOrphanedUndoHistory(ITextView textView)
        {
            var history = EditorUtil.GetUndoHistory(textView.TextBuffer);
            if (history.CurrentTransaction != null)
            {
                _errorList.Add(new Exception("Failed to close an undo transaction"));
            }
        }

        void IExtensionErrorHandler.HandleError(object sender, Exception exception)
        {
            _errorList.Add(exception);
        }

        bool IVimErrorDetector.HasErrors()
        {
            CheckForOrphanedUndoHistory();
            return _errorList.Count > 0;
        }

        IEnumerable<Exception> IVimErrorDetector.GetErrors()
        {
            CheckForOrphanedUndoHistory();
            return _errorList;
        }

        void IVimErrorDetector.Clear()
        {
            _errorList.Clear();
            _activeTextViewList.Clear();
        }

        void IWpfTextViewCreationListener.TextViewCreated(IWpfTextView textView)
        {
            _activeTextViewList.Add(textView);
            textView.Closed +=
                (sender, e) =>
                {
                    _activeTextViewList.Remove(textView);
                    CheckForOrphanedUndoHistory(textView);
                };
        }
    }
}
