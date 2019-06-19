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
        private IEnumerable<VirtualSnapshotPoint> GetCaretPoints(ITextView textView)
        {
            return new[] { textView.Caret.Position.VirtualBufferPosition };
        }

        private void SetCaretPoints(ITextView textView, IEnumerable<VirtualSnapshotPoint> caretPoints)
        {
            var caretPoint = caretPoints.First();
            textView.Caret.MoveTo(caretPoint);
        }
    }

#else

    internal partial class SharedService
    {
        private IEnumerable<VirtualSnapshotPoint> GetCaretPoints(ITextView textView)
        {
            return
                textView
                .GetMultiSelectionBroker()
                .AllSelections
                .Select(selection => selection.InsertionPoint);
        }

        private void SetCaretPoints(ITextView textView, IEnumerable<VirtualSnapshotPoint> caretPoints)
        {
            SetCaretPointsCommon(textView, caretPoints.ToArray());
        }

        private void SetCaretPointsCommon(ITextView textView, VirtualSnapshotPoint[] caretPoints)
        {
            if (caretPoints.Length == 1)
            {
                textView.Caret.MoveTo(caretPoints[0]);
                return;
            }

            var selections = new Microsoft.VisualStudio.Text.Selection[caretPoints.Length];
            for (var caretIndex = 0; caretIndex < caretPoints.Length; caretIndex++)
            {
                selections[caretIndex] = new Microsoft.VisualStudio.Text.Selection(caretPoints[caretIndex]);
            }
            var broker = textView.GetMultiSelectionBroker();
            broker.SetSelectionRange(selections, selections[0]);
        }
    }

#endif
}
