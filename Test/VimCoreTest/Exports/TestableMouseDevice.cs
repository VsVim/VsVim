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

        bool IMouseDevice.IsLeftButtonPressed
        {
            get { return IsLeftButtonPressed; }
        }

        public Point? GetPosition(ITextView textView)
        {
            return null;
        }
    }
}
