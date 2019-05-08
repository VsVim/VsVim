using System.ComponentModel.Composition;

namespace Vim.UnitTest.Exports
{
    [Export(typeof(IClipboardDevice))]
    public sealed class TestableClipboardDevice : IClipboardDevice
    {
        public bool ReportErrors { get; set; }
        public string Text { get; set; }
    }
}
