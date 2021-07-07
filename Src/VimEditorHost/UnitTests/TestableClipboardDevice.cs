#if VS_UNIT_TEST_HOST
using System.ComponentModel.Composition;

namespace Vim.UnitTest
{
    [Export(typeof(IClipboardDevice))]
    public sealed class TestableClipboardDevice : IClipboardDevice
    {
        public bool ReportErrors { get; set; }
        public string Text { get; set; }
    }
}
#endif
