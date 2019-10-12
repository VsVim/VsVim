using System;
using System.ComponentModel.Composition;

namespace Vim.Mac
{
    [Export(typeof(IClipboardDevice))]
    internal sealed class ClipboardDevice : IClipboardDevice
    {
        public bool ReportErrors { get; set; }

        public string Text { get => OsxClipboard.GetText(); set => OsxClipboard.SetText(value); }
    }
}
