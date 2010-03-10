using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Vim.UI.Wpf
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern int GetCaretBlinkTime();
    }
}
