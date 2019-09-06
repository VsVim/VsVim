using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using Vim.Extensions;

namespace Vim.VisualStudio.Implementation.Misc
{
    /// <summary>
    /// Sometimes performing host actions produces unwanted selections. The
    /// purpose of this class to detect and remove them.
    /// </summary>
    internal sealed class UnwantedSelectionHandler
    {
        private readonly IVim _vim;

        private List<WeakReference<ITextView>> _selected;

        internal UnwantedSelectionHandler(IVim vim)
        {
            _vim = vim;
            _selected = new List<WeakReference<ITextView>>();
        }

        internal void PreAction()
        {
            // Cautiously record which buffers have pre-existing selections.
            _selected = _vim
                .VimBuffers
                .Select(x => x.TextView)
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
            _vim
                .VimBuffers
                .Select(x => x.TextView)
                .Where(textView =>
                    !textView.Selection.IsEmpty && !HadPreExistingSelection(textView))
                .ForEach(textView => ClearSelection(textView));

            var focusedWindow = _vim.VimHost.GetFocusedTextView();
            if (focusedWindow.IsSome())
            {
                ClearSelection(focusedWindow.Value);
            }
        }

        internal void ClearSelection(ITextView textView)
        {
            // Move the caret to the beginning of the selection.
            var startPoint = textView.Selection.Start;
            textView.Selection.Clear();
            textView.Caret.MoveTo(startPoint);
            if (_vim.TryGetVimBuffer(textView, out var vimBuffer))
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
