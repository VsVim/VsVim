using System;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf.RelativeLineNumbers.Util
{
    public static class TextViewExtensions
    {
        public static int GetLineCount(this ITextView textView)
        {
            textView = textView
                ?? throw new ArgumentNullException(nameof(textView));

            return textView.TextSnapshot.LineCount;
        }

        public static ITextSnapshotLine GetLine(this CaretPosition caretPosition)
        {
            return caretPosition.BufferPosition.GetContainingLine();
        }
    }
}