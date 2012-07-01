using System.ComponentModel.Composition;
using System.Windows;

namespace Vim.UI.Wpf.Implementation.Misc
{
    [Export(typeof(IClipboardDevice))]
    internal sealed class ClipboardDevice : IClipboardDevice
    {
        public string Text
        {
            get { return Clipboard.GetText(); }
            set { Clipboard.SetText(value); }
        }
    }
}
