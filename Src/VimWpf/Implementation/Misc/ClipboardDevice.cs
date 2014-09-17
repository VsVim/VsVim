using System;
using System.ComponentModel.Composition;
using System.Windows;

namespace Vim.UI.Wpf.Implementation.Misc
{
    [Export(typeof(IClipboardDevice))]
    internal sealed class ClipboardDevice : IClipboardDevice
    {
        private readonly IVimProtectedOperations _protectedOperations;

        /// <summary>
        /// The WPF clipboard can get into a bad state which causes it to throw on every
        /// usage of GetText.  When that happens we fall back to GetData instead.  
        /// </summary>
        private bool _useTextMethods;

        [ImportingConstructor]
        internal ClipboardDevice(IVimProtectedOperations protectedOperations)
        {
            _protectedOperations = protectedOperations;
            _useTextMethods = true;
        }

        private string GetText()
        {
            try
            {
                var text = _useTextMethods
                    ? Clipboard.GetText()
                    : (string)Clipboard.GetData(DataFormats.UnicodeText);

                return text ?? string.Empty;
            }
            catch (Exception ex)
            {
                _protectedOperations.Report(ex);
                _useTextMethods = false;
                return string.Empty;
            }
        }

        private void SetText(string text)
        {
            try
            {
                if (_useTextMethods)
                {
                    Clipboard.SetText(text);
                }
                else
                {
                    Clipboard.SetDataObject(text);
                }
            }
            catch (Exception ex)
            {
                _protectedOperations.Report(ex);
                _useTextMethods = false;
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
