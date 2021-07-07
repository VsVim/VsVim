#if VS_UNIT_TEST_HOST
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;
using System.Windows;
using Vim;

namespace Vim.UnitTest
{
    [Export(typeof(IMouseDevice))]
    public sealed class TestableMouseDevice : IMouseDevice
    {
        public bool IsLeftButtonPressed { get; set; }
        public bool IsRightButtonPressed { get; set; }
        public bool InDragOperationImpl { get; set; }
        public SnapshotPoint? Point { get; set; }
        public int YOffset { get; set; }

        bool IMouseDevice.IsLeftButtonPressed
        {
            get { return IsLeftButtonPressed; }
        }

        bool IMouseDevice.IsRightButtonPressed
        {
            get { return IsRightButtonPressed; }
        }

        public FSharpOption<VimPoint> GetPosition(ITextView textView)
        {
            if (Point.HasValue)
            {
                var point = Point.Value;
                var textViewLines = textView.TextViewLines;
                if (textViewLines is null)
                {
                    return null;
                }
                var textViewLine = textViewLines.GetTextViewLineContainingBufferPosition(point);
                if (textViewLine is null)
                {
                    return null;
                }
                var bounds = textViewLine.GetCharacterBounds(point);
                var xCoordinate = bounds.Left - textView.ViewportLeft;
                var yCoordinate = (textViewLine.Top + textViewLine.Bottom) / 2 - textView.ViewportTop;
                return new VimPoint(xCoordinate, yCoordinate + YOffset);
            }
            return null;
        }

        public bool InDragOperation(ITextView textView)
        {
            return InDragOperationImpl;
        }
    }
}
#endif
