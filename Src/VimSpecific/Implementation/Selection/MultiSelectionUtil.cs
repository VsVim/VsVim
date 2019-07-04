#if VS_SPECIFIC_2015 || VS_SPECIFIC_2017
#else

using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Vim;
using Vim.VisualStudio.Specific;

namespace Vim.Specific.Implementation.Selection
{
    [Export(typeof(ISelectionUtil))]
    [Export(typeof(IVimSpecificService))]
    class MultiSelectionUtil : VimSpecificService, ISelectionUtil
    {
        [ImportingConstructor]
        internal MultiSelectionUtil(Lazy<IVimHost> vimHost)
            : base(vimHost)
        {
        }

        bool ISelectionUtil.IsMultiSelectionSupported => true;

        IEnumerable<SelectedSpan> ISelectionUtil.GetSelectedSpans(ITextView textView)
        {
            var broker = textView.GetMultiSelectionBroker();
            var primarySelection = broker.PrimarySelection;
            if (textView.Selection.Mode != TextSelectionMode.Stream)
            {
                return new[] { GetSelectedSpan(primarySelection) };
            }
            var secondarySelections = broker.AllSelections
                .Where(span => span != primarySelection)
                .Select(selection => GetSelectedSpan(selection));
            return new[] { GetSelectedSpan(primarySelection) }.Concat(secondarySelections);
        }

        void ISelectionUtil.SetSelectedSpans(ITextView textView, IEnumerable<SelectedSpan> selectedSpans)
        {
            SetSelectedSpansCore(textView, selectedSpans.ToArray());
        }

        private void SetSelectedSpansCore(ITextView textView, SelectedSpan[] selectedSpans)
        {
            if (selectedSpans.Length == 1 || textView.Selection.Mode != TextSelectionMode.Stream)
            {
                var selectedSpan = selectedSpans[0];
                textView.Caret.MoveTo(selectedSpan.CaretPoint);
                if (selectedSpan.Length == 0)
                {
                    textView.Selection.Clear();
                }
                else
                {
                    textView.Selection.Select(selectedSpan.AnchorPoint, selectedSpan.ActivePoint);
                }
                return;
            }

            var selections = new Microsoft.VisualStudio.Text.Selection[selectedSpans.Length];
            for (var caretIndex = 0; caretIndex < selectedSpans.Length; caretIndex++)
            {
                selections[caretIndex] = GetSelection(selectedSpans[caretIndex]);
            }
            var broker = textView.GetMultiSelectionBroker();
            broker.SetSelectionRange(selections, selections[0]);
        }

        private static SelectedSpan GetSelectedSpan(Microsoft.VisualStudio.Text.Selection selection)
        {
            return new SelectedSpan(selection.InsertionPoint, selection.AnchorPoint, selection.ActivePoint);
        }

        private static Microsoft.VisualStudio.Text.Selection GetSelection(SelectedSpan span)
        {
            return new Microsoft.VisualStudio.Text.Selection(span.CaretPoint, span.AnchorPoint, span.ActivePoint);
        }
    }
}

#endif
