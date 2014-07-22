using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Vim;
using EditorUtils;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Text;

namespace Vim.VisualStudio.UnitTest.Utils
{
    /// <summary>
    /// Simulation of the R# command target.  This is intended to implement the most basic of 
    /// R# functionality for the purpose of testing
    /// </summary>
    internal sealed class ReSharperCommandTargetSimulation : IOleCommandTarget
    {
        private readonly ITextView _textView;
        private readonly IOleCommandTarget _nextCommandTarget;

        internal bool IntellisenseDisplayed { get; set; }
        internal int ExecEscapeCount { get; set; }
        internal int ExecBackCount { get; set; }

        internal ReSharperCommandTargetSimulation(ITextView textView, IOleCommandTarget nextCommandTarget)
        {
            _textView = textView;
            _nextCommandTarget = nextCommandTarget;
        }

        /// <summary>
        /// Try and simulate the execution of the few KeyInput values we care about
        /// </summary>
        private bool TryExec(KeyInput keyInput)
        {
            if (keyInput.Key == VimKey.Back)
            {
                return TryExecBack();
            }

            if (keyInput.Key == VimKey.Escape)
            {
                return TryExecEscape();
            }

            return false;
        }

        /// <summary>
        /// R# will delete both parens when the Back key is used on the closing paren
        /// </summary>
        private bool TryExecBack()
        {
            var caretPoint = _textView.GetCaretPoint();
            if (caretPoint.Position < 2 ||
                caretPoint.GetChar() != ')' ||
                caretPoint.Subtract(1).GetChar() != '(')
            {
                return false;
            }

            var span = new Span(caretPoint.Position - 1, 2);
            _textView.TextBuffer.Delete(span);
            ExecBackCount++;
            return true;
        }

        private bool TryExecEscape()
        {
            if (!IntellisenseDisplayed)
            {
                return false;
            }

            IntellisenseDisplayed = false;
            ExecEscapeCount++;
            return true;
        }

        int IOleCommandTarget.Exec(ref Guid commandGroup, uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            KeyInput keyInput;
            EditCommandKind editCommandKind;
            if (!OleCommandUtil.TryConvert(commandGroup, commandId, variantIn, out keyInput, out editCommandKind) ||
                !TryExec(keyInput))
            {
                return _nextCommandTarget.Exec(ref commandGroup, commandId, commandExecOpt, variantIn, variantOut);
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// R# just forwards it's QueryStatus call onto the next target
        /// </summary>
        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            return _nextCommandTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }
    }

}
