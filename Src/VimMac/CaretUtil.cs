using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Vim.Mac
{
    [Export(typeof(IVimBufferCreationListener))]
    internal class CaretUtil : IVimBufferCreationListener
    {
        private void SetCaret(IVimBuffer vimBuffer)
        {
            var textView = vimBuffer.TextView;
            if (vimBuffer.Mode.ModeKind == ModeKind.Insert || vimBuffer.Mode.ModeKind == ModeKind.ExternalEdit)
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

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            SetCaret(vimBuffer);
            vimBuffer.SwitchedMode += (_,__) => SetCaret(vimBuffer);
        }
    }
}
