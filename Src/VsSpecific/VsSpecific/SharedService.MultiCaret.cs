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
            var span = textView.Selection.StreamSelectionSpan;
            return new[] { new SelectedSpan(caretPoint, span) };
        }

        private void SetSelectedSpans(ITextView textView, IEnumerable<SelectedSpan> selectedSpans)
        {
            var selectedSpan = selectedSpans.First();
            textView.Caret.MoveTo(selectedSpan.CaretPoint);
            if (selectedSpan.Length != 0)
            {
                textView.Selection.Select(selectedSpan.StartPoint, selectedSpan.EndPoint);
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

        private IEnumerable<SelectedSpan> GetSelectedSpansCommon(ITextView textView)
        {
            var broker = textView.GetMultiSelectionBroker();
            var primaryCaretPoint = textView.Caret.Position.VirtualBufferPosition;
            var secondarySelections = broker.AllSelections
                .Select(selection => GetSelectedSpan(selection))
                .Where(span => span.CaretPoint != primaryCaretPoint);
            return new[] { GetSelectedSpan(broker.PrimarySelection) }.Concat(secondarySelections);
        }

        private void SetSelectedSpansCommon(ITextView textView, SelectedSpan[] selectedSpans)
        {
            if (selectedSpans.Length == 1)
            {
                var selectedSpan = selectedSpans[0];
                textView.Caret.MoveTo(selectedSpan.CaretPoint);
                if (selectedSpan.Length != 0)
                {
                    textView.Selection.Select(selectedSpan.StartPoint, selectedSpan.EndPoint);
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
            return new SelectedSpan(selection.InsertionPoint, selection.Start, selection.End);
        }

        private static Microsoft.VisualStudio.Text.Selection GetSelection(SelectedSpan span)
        {
            return new Microsoft.VisualStudio.Text.Selection(span.CaretPoint, span.StartPoint, span.EndPoint);
        }
    }

#endif
}
