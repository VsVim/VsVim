using System;
using System.Runtime.InteropServices;

namespace Vim.UI.Wpf
{
    internal static class NativeMethods
    {
        /// <summary>
        /// Flag for LoadKeyboardLayout which will make the layout the active one for the thread
        /// </summary>
        public const uint KLF_ACTIVATE = 0x1;

        public const uint KL_NAMELENGTH = 9;

        public const string LayoutEnglish = "00000409";

        public const string LayoutDvorak = "00010409";

        public const string LayoutPortuguese = "00000816";

        public const string LayoutTurkishF = "00001055";

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

        [DllImport("user32.dll")]
        internal static extern bool GetKeyboardLayoutName(char[] name);

        [DllImport("user32.dll")]
        internal static extern IntPtr LoadKeyboardLayout([In] string id, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnloadKeyboardLayout(IntPtr keyboardId);

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
