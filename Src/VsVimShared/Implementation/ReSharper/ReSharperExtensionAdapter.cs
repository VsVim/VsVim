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
        internal const string FilePathPrefixRegexEditor = "RegularExpressionEditor";
        internal const string FilePathPrefixUnitTestSessionOutput = "StackTraceExplorerEditor";

        private readonly IReSharperUtil _reSharperUtil;
        private readonly ITextDocumentFactoryService _textDocumentFactoryService;

        [ImportingConstructor]
        internal ReSharperExtensionAdapter(IReSharperUtil reSharperUtil, ITextDocumentFactoryService textDocumentFactoryService)
        {
            _reSharperUtil = reSharperUtil;
            _textDocumentFactoryService = textDocumentFactoryService;
        }

        internal bool? ShouldCreateVimBuffer(ITextView textView)
        {
            if (!_reSharperUtil.IsInstalled)
            {
                return null;
            }

            var textBuffer = textView.TextDataModel.DocumentBuffer;
            if (!_textDocumentFactoryService.TryGetTextDocument(textBuffer, out ITextDocument textDocument))
            {
                return null;
            }

            // This is a bit of a heuristic.  It is technically possible for another component to create
            // a <see cref="ITextDocument"/> with the specified name pattern.  However it seems unlikely 
            // that it will happen when R# is also installed.
            if (textDocument.FilePath.StartsWith(FilePathPrefixRegexEditor, StringComparison.OrdinalIgnoreCase) ||
                textDocument.FilePath.StartsWith(FilePathPrefixUnitTestSessionOutput, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return null;
        }

        #region IExtensionAdapter

        bool? IExtensionAdapter.IsUndoRedoExpected
        {
            get { return null; }
        }

        bool? IExtensionAdapter.ShouldKeepSelectionAfterHostCommand(string command, string argument)
        {
            if (!_reSharperUtil.IsInstalled)
            {
                return null;
            }

            var comparer = StringComparer.OrdinalIgnoreCase;
            if (comparer.Equals(command, "ReSharper.ReSharper_ExtendSelection") ||
                comparer.Equals(command, "ReSharper.ReSharper_SurroundWith"))
            {
                return true;
            }

            return null;
        }

        bool? IExtensionAdapter.ShouldCreateVimBuffer(ITextView textView)
        {
            return ShouldCreateVimBuffer(textView);
        }

        bool? IExtensionAdapter.IsIncrementalSearchActive(ITextView textView)
        {
            return null;
        }

        bool? IExtensionAdapter.UseDefaultCaret
        {
            get { return null; }
        }

        #endregion
    }
}
