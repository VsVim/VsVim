using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using System.Runtime.InteropServices;

namespace Vim.UI.Wpf.Implementation.Misc
{
    [Export(typeof(IClipboardDevice))]
    internal sealed class ClipboardDevice : IClipboardDevice
    {
        private readonly IProtectedOperations _protectedOperations;

        private bool _reportErrors;

        [ImportingConstructor]
        internal ClipboardDevice(IProtectedOperations protectedOperations)
        {
            _protectedOperations = protectedOperations;
            _reportErrors = false;
        }

        private string GetText()
        {
            string text = null;
            void action()
            {
                text = Clipboard.GetText();
            }

            if (!TryAccessClipboard(action))
            {
                text = string.Empty;
            }

            return text;
        }

        private void SetTextWin32(string text)
        {
            if (!NativeMethods.OpenClipboard(IntPtr.Zero))
                return;

            NativeMethods.EmptyClipboard();
            IntPtr hGlobal = Marshal.StringToHGlobalUni(text);
            const int CF_UNICODETEXT = 13;
            NativeMethods.SetClipboardData(CF_UNICODETEXT, hGlobal);
            NativeMethods.CloseClipboard();
        }

        private void SetText(string text)
        {
            void action()
            {
                SetTextWin32(text);
            }

            TryAccessClipboard(action);
        }

        /// <summary>
        /// The clipboard is a shared resource across all applications.  It can only be used 
        /// by one application at a time and has no mechanism for synchronizing access.  
        ///
        /// Use of the clipboard should be short lived though so most applications guard 
        /// against races by simply retrying the access a number of times.  This is how 
        /// WinForms handles the race.  WPF does not do this hence we have to implement it 
        /// manually here. 
        /// </summary>
        private bool TryAccessClipboard(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                if (_reportErrors)
                {
                    _protectedOperations.Report(ex);
                }
                return false;
            }
        }

        #region IClipboardDevice

        bool IClipboardDevice.ReportErrors
        {
            get { return _reportErrors; }
            set { _reportErrors = value; }
        }

        string IClipboardDevice.Text
        {
            get { return GetText(); }
            set { SetText(value); }
        }

        #endregion
    }
}
