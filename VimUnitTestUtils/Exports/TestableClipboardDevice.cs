using System.ComponentModel.Composition;

namespace Vim.UnitTest.Exports
{
    [Export(typeof(IClipboardDevice))]
    public sealed class TestableClipboardDevice : IClipboardDevice
    {
        public string Text { get; set; }
    }
}
