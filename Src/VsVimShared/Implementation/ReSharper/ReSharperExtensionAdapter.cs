using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Vim;
using System.Diagnostics;

namespace Vim.VisualStudio.Implementation.ReSharper
{
    [Export(typeof(IExtensionAdapter))]
    internal sealed class ReSharperExtensionAdapter : IExtensionAdapter
    {
        private readonly IReSharperUtil _reSharperUtil;
        private readonly ITextDocumentFactoryService _textDocumentFactoryService;


        [ImportingConstructor]
        internal ReSharperExtensionAdapter(IReSharperUtil reSharperUtil, ITextDocumentFactoryService textDocumentFactoryService)
        {
            _reSharperUtil = reSharperUtil;
            _textDocumentFactoryService = textDocumentFactoryService;
        }

        /// <summary>
        /// This is a bit of a hueristic.  It is technically possible for another component to create
        /// a <see cref="ITextDocument"/> with the specified name pattern.  However it seems unlikely 
        /// that it will happen when R# is also installed.
        /// </summary>
        private bool IsRegexEditorTextBuffer(ITextView textView)
        {
            Debug.Assert(_reSharperUtil.IsInstalled);

            var textBuffer = textView.TextDataModel.DocumentBuffer;
            ITextDocument textDocument;
            if (!_textDocumentFactoryService.TryGetTextDocument(textBuffer, out textDocument))
            {
                return false;
            }

            return textDocument.FilePath.StartsWith("RegularExpressionEditor.", StringComparison.OrdinalIgnoreCase);
        }

        #region IExtensionAdapter

        bool? IExtensionAdapter.ShouldKeepSelectionAfterHostCommand(string command, string argument)
        {
            if (!_reSharperUtil.IsInstalled)
            {
                return null;
            }

            var comparer = StringComparer.OrdinalIgnoreCase;
            return comparer.Equals(command, "ReSharper.ReSharper_ExtendSelection");
        }

        bool? IExtensionAdapter.ShouldCreateVimBuffer(ITextView textView)
        {
            if (_reSharperUtil.IsInstalled && IsRegexEditorTextBuffer(textView))
            {
                return false;
            }

            return null;
        }

        bool? IExtensionAdapter.IsIncrementalSearchActive(ITextView textView)
        {
            return null;
        }

        #endregion
    }
}
