using System;
using System.Windows;
using WPFClipboard = System.Windows.Clipboard;

namespace Vim.UI.Wpf.Implementation.Misc
{
    static class Clipboard
    {
        public static string GetText()
        {
            IDataObject dataObj = WPFClipboard.GetDataObject();

            if (dataObj == null || !dataObj.GetDataPresent(typeof(string)))
            {
                return null;
            }

            return (string)dataObj.GetData(DataFormats.UnicodeText)
                ?? (string)dataObj.GetData(DataFormats.Text);
        }

        public static void SetText(string text)
        {
            DataObject dataObject = new DataObject();
            dataObject.SetText(text);

            WPFClipboard.SetDataObject(dataObject, false);
        }
    }
}
