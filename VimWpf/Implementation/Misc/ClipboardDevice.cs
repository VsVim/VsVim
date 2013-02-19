using System.ComponentModel.Composition;
using System.Windows;

namespace Vim.UI.Wpf.Implementation.Misc
{
    [Export(typeof(IClipboardDevice))]
    internal sealed class ClipboardDevice : IClipboardDevice
    {
        private string GetText()
        {
            try
            {
                return Clipboard.GetText();
            }
            catch
            {
                return string.Empty;
            }
        }

        private void SetText(string text)
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch
            {

            }
        }

        #region IClipboardDevice

        string IClipboardDevice.Text
        {
            get { return GetText(); }
            set { SetText(value); }
        }

        #endregion
    }
}
