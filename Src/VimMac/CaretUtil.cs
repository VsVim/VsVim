using System;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Vim.Mac
{
    internal static class CaretUtil
    {
        public static void SetCaret(IVimBuffer vimBuffer, ITextView textView)
        {
            if (vimBuffer.Mode.ModeKind == ModeKind.Insert)
            {
                //TODO: what's the minimum caret width for accessibility?
                textView.Options.SetOptionValue(DefaultTextViewOptions.CaretWidthOptionName, 1.0);
            }
            else
            {
                var caretWidth = 10.0;
                //TODO: Is there another way to figure out the caret width?
                // TextViewLines == null when the view is first loaded
                if (textView.TextViewLines != null)
                {
                    ITextViewLine textLine = textView.GetTextViewLineContainingBufferPosition(textView.Caret.Position.BufferPosition);
                    caretWidth = textLine.VirtualSpaceWidth;
                }
                textView.Options.SetOptionValue(DefaultTextViewOptions.CaretWidthOptionName, caretWidth);
            }
        }
    }
}
