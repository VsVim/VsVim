using System;
using System.Runtime.InteropServices;

namespace Vim.UI.Wpf
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern int GetCaretBlinkTime();

        [DllImport("user32.dll")]
        internal static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        internal static extern short VkKeyScanEx(char ch, IntPtr hkl);

        [DllImport("user32.dll")]
        internal static extern uint MapVirtualKey(uint code, uint mapType);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetKeyboardLayout(uint idThread);

        internal static int HiWord(int number)
        {
            return (number >> 16) & 0xffff;
        }

        internal static int LoWord(int number)
        {
            return number & 0xffff;
        }
    }
}
