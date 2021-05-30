using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Vim.UI.Cocoa.Implementation.InlineRename;

namespace Vim.Mac
{
    [Export(typeof(IVimBufferCreationListener))]
    internal class CaretUtil : IVimBufferCreationListener
    {
        private readonly InlineRenameListenerFactory _inlineRenameListenerFactory;

        [ImportingConstructor]
        public CaretUtil(InlineRenameListenerFactory inlineRenameListenerFactory)
        {
            _inlineRenameListenerFactory = inlineRenameListenerFactory;
        }

        private void SetCaret(IVimBuffer vimBuffer)
        {
            var textView = vimBuffer.TextView;

            if (textView.IsClosed)
                return;

            if (vimBuffer.Mode.ModeKind == ModeKind.Insert || _inlineRenameListenerFactory.InRename)
            {
                textView.Options.SetOptionValue(DefaultTextViewOptions.CaretWidthOptionName, 1.0);
            }
            else
            {
                var caretWidth = 10.0;
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
            vimBuffer.SwitchedMode += (s, e) => SetCaret(vimBuffer);
            vimBuffer.TextView.Options.GlobalOptions.OptionChanged += (sender, e) => GlobalOptions_OptionChanged(e.OptionId, vimBuffer);
            _inlineRenameListenerFactory.RenameUtil.IsRenameActiveChanged += (s, e) => SetCaret(vimBuffer);
        }

        void GlobalOptions_OptionChanged(string optionId, IVimBuffer vimBuffer)
        {
            if(optionId == DefaultCocoaViewOptions.ZoomLevelId.Name)
            {
                SetCaret(vimBuffer);
            }
        }
    }
}
