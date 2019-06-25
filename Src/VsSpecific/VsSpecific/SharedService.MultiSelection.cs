using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Platform.WindowManagement;
using Microsoft.VisualStudio.PlatformUI.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;

namespace Vim.VisualStudio.Specific
{
#if VS_SPECIFIC_2015 || VS_SPECIFIC_2017

    internal partial class SharedService
    {
        private IEnumerable<SelectedSpan> GetSelectedSpans(ITextView textView)
        {
            var caretPoint = textView.Caret.Position.VirtualBufferPosition;
            var anchorPoint = textView.Selection.AnchorPoint;
            var activePoint = textView.Selection.ActivePoint;
            return new[] { new SelectedSpan(caretPoint, anchorPoint, activePoint) };
        }

        private void SetSelectedSpans(ITextView textView, IEnumerable<SelectedSpan> selectedSpans)
        {
            var selectedSpan = selectedSpans.First();
            textView.Caret.MoveTo(selectedSpan.CaretPoint);
            if (selectedSpan.Length != 0)
            {
                textView.Selection.Select(selectedSpan.AnchorPoint, selectedSpan.ActivePoint);
            }
        }
    }

#else

    internal partial class SharedService
    {
        private IEnumerable<SelectedSpan> GetSelectedSpans(ITextView textView)
        {
            return GetSelectedSpansCommon(textView);
        }

        private void SetSelectedSpans(ITextView textView, IEnumerable<SelectedSpan> selectedSpans)
        {
            SetSelectedSpansCommon(textView, selectedSpans.ToArray());
        }

        // TODO: duplicated code start
        private IEnumerable<SelectedSpan> GetSelectedSpansCommon(ITextView textView)
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

        private void SetSelectedSpansCommon(ITextView textView, SelectedSpan[] selectedSpans)
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
        // TODO: duplicated code end
    }

#endif
}
