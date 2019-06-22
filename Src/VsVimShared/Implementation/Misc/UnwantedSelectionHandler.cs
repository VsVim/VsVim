using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Vim.VisualStudio.Implementation.Misc
{
    /// <summary>
    /// Sometimes performing host actions produces unwanted selections. The
    /// purpose of this class to detect and remove them.
    /// </summary>
    internal sealed class UnwantedSelectionHandler
    {
        private readonly IVim _vim;
        private readonly ITextManager _textManager;

        private List<WeakReference<ITextView>> _selected;

        internal UnwantedSelectionHandler(IVim vim, ITextManager textManager)
        {
            _vim = vim;
            _textManager = textManager;
            _selected = new List<WeakReference<ITextView>>();
        }

        internal void PreAction()
        {
            // Cautiously record which buffers have pre-existing selections.
            _selected = _textManager
                .GetDocumentTextViews(DocumentLoad.RespectLazy)
                .Where(x => !x.Selection.IsEmpty)
                .Select(x => new WeakReference<ITextView>(x))
                .ToList();
        }

        internal void PostAction()
        {
            // Once the host action is stopped, clear out all of the new
            // selections in active buffers.  Leaving the  selection puts us
            // into Visual Mode.  Don't force any document loads here.  If the
            // document isn't loaded then it can't have a selection which would
            // interfere with this.
            _textManager.GetDocumentTextViews(DocumentLoad.RespectLazy)
                .Where(textView =>
                    !textView.Selection.IsEmpty && !HadPreExistingSelection(textView))
                .ForEach(textView => ClearSelection(textView));
        }

        internal void ClearSelection(ITextView textView)
        {
            // Move the caret to the beginning of the selection.
            var startPoint = textView.Selection.Start;
            textView.Selection.Clear();
            textView.Caret.MoveTo(startPoint);

            // This window might not have the focus yet. If not the selection
            // change tracker will ignore the selection changed event. To guard
            // against that case, force the buffer to normal mode.  See PR
            // #2205 for context related to this problem.
            if (!_vim.VimHost.IsFocused(textView)
                && _vim.TryGetVimBuffer(textView, out var vimBuffer))
            {
                vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            }
        }

        private bool HadPreExistingSelection(ITextView textView)
        {
            return _selected.Where(weakReference =>
                weakReference.TryGetTarget(out var target) && target == textView).Any();
        }
    }
}
