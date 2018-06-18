using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;

namespace Vim.UI.Wpf.Implementation.Misc
{
    [Export(typeof(IClipboardDevice))]
    internal sealed class ClipboardDevice : IClipboardDevice
    {
        private readonly IProtectedOperations _protectedOperations;

        /// <summary>
        /// The WPF clipboard can get into a bad state which causes it to throw on every
        /// usage of GetText.  When that happens we fall back to GetData instead.  
        /// </summary>
        private bool _useTextMethods;

        [ImportingConstructor]
        internal ClipboardDevice(IProtectedOperations protectedOperations)
        {
            _protectedOperations = protectedOperations;
            _useTextMethods = true;
        }

        private string GetText()
        {
            string text = null;
            void action()
            {
                text = _useTextMethods
                    ? Clipboard.GetText()
                    : (string)Clipboard.GetData(DataFormats.UnicodeText);
            }

            if (!TryAccessClipboard(action))
            {
                text = string.Empty;
            }

            return text;
        }

        private void SetText(string text)
        {
            void action()
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
        private bool TryAccessClipboard(Action action, int retryCount = 5, int pauseMilliseconds = 100)
        {
            var i = retryCount;
            do
            {
                try
                {
                    action();
                    return true;
                }
                catch (Exception ex)
                {
                    i--;
                    if (i == 0)
                    {
                        _protectedOperations.Report(ex);
                        _useTextMethods = false;
                        return false;
                    }
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(pauseMilliseconds));
            } while (true);
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
