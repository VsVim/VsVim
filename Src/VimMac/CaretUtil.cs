using System;
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
        private IVimBuffer _vimBuffer;

        [ImportingConstructor]
        public CaretUtil(InlineRenameListenerFactory inlineRenameListenerFactory)
        {
            _inlineRenameListenerFactory = inlineRenameListenerFactory;
        }


        private void SetCaret()
        {
            var textView = _vimBuffer.TextView;

            if (textView.IsClosed)
                return;

            if (_vimBuffer.Mode.ModeKind == ModeKind.Insert || _inlineRenameListenerFactory.InRename)
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
            _vimBuffer = vimBuffer;
            SetCaret();
            vimBuffer.SwitchedMode += VimBuffer_SwitchedMode;
            vimBuffer.TextView.Options.GlobalOptions.OptionChanged += GlobalOptions_OptionChanged;
            _inlineRenameListenerFactory.RenameUtil.IsRenameActiveChanged += RenameUtil_IsRenameActiveChanged;
            vimBuffer.Closed += VimBuffer_Closed;
        }

        void RenameUtil_IsRenameActiveChanged(object sender, EventArgs e)
        {
            SetCaret();
        }

        void VimBuffer_SwitchedMode(object sender, SwitchModeEventArgs e)
        {
            SetCaret();
        }

        void GlobalOptions_OptionChanged(object sender, EditorOptionChangedEventArgs e)
        {
            if(e.OptionId == DefaultCocoaViewOptions.ZoomLevelId.Name)
            {
                SetCaret();
            }
        }

        void VimBuffer_Closed(object sender, EventArgs e)
        {
            _vimBuffer.SwitchedMode -= VimBuffer_SwitchedMode;
            _vimBuffer.TextView.Options.GlobalOptions.OptionChanged -= GlobalOptions_OptionChanged;
            _inlineRenameListenerFactory.RenameUtil.IsRenameActiveChanged -= RenameUtil_IsRenameActiveChanged;
            _vimBuffer.Closed -= VimBuffer_Closed;
        }

    }
}
