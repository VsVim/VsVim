using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;
using System.Windows;
using Vim;

namespace Vim.UnitTest.Exports
{
    [Export(typeof(IMouseDevice))]
    public sealed class TestableMouseDevice : IMouseDevice
    {
        public bool IsLeftButtonPressed { get; set; }
        public bool InDragOperationImpl { get; set; }
        public VimPoint? Position { get; set; }

        bool IMouseDevice.IsLeftButtonPressed
        {
            get { return IsLeftButtonPressed; }
        }

        public FSharpOption<VimPoint> GetPosition(ITextView textView)
        {
            return Position;
        }

        public bool InDragOperation(ITextView textView)
        {
            return InDragOperationImpl;
        }

        public void SetPosition(ITextView textView, SnapshotPoint point)
        {
            var textViewLine = textView.TextViewLines.GetTextViewLineContainingBufferPosition(point);
            var bounds = textViewLine.GetCharacterBounds(point);
            var xCoordinate = (bounds.Left + bounds.Right) / 2;
            var yCoordinate = (textViewLine.Top + textViewLine.Bottom) / 2;
            Position = new VimPoint(xCoordinate, yCoordinate);
        }
    }
}
