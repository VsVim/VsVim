#if VS_SPECIFIC_2015 || VS_SPECIFIC_2017

using Microsoft.VisualStudio.Text.Editor;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Vim;

namespace Vim.Specific.Implementation.Selection
{
    [Export(typeof(ISelectionUtil))]
    class SingleSelectionUtil : ISelectionUtil
    {
        IEnumerable<SelectedSpan> ISelectionUtil.GetSelectedSpans(ITextView textView)
        {
            var caretPoint = textView.Caret.Position.VirtualBufferPosition;
            var anchorPoint = textView.Selection.AnchorPoint;
            var activePoint = textView.Selection.ActivePoint;
            return new[] { new SelectedSpan(caretPoint, anchorPoint, activePoint) };
        }

        void ISelectionUtil.SetSelectedSpans(ITextView textView, IEnumerable<SelectedSpan> selectedSpans)
        {
            var selectedSpan = selectedSpans.First();
            textView.Caret.MoveTo(selectedSpan.CaretPoint);
            if (selectedSpan.Length != 0)
            {
                textView.Selection.Select(selectedSpan.AnchorPoint, selectedSpan.ActivePoint);
            }
        }
    }
}

#endif
