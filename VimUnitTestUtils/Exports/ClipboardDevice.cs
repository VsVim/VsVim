using System.ComponentModel.Composition;

namespace Vim.UnitTest.Exports
{
    [Export(typeof(IClipboardDevice))]
    internal sealed class ClipboardDevice : IClipboardDevice
    {
        public string Text { get; set; }
    }
}
