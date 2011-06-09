using System;
using System.Runtime.InteropServices;

namespace Vim.UI.Wpf
{
    /// <summary>
    /// Holder for all of the PInvoke and related defines for this DLL
    /// </summary>
    internal static class NativeMethods
    {
        /// <summary>
        /// Flag for LoadKeyboardLayout which will make the layout the active one for the thread
        /// </summary>
        internal const uint KLF_ACTIVATE = 0x1;

        internal const uint KL_NAMELENGTH = 9;

        /// <summary>
        /// Keyboard code for the default English QWERTY layout
        /// </summary>
        internal const string LayoutEnglish = "00000409";

        /// <summary>
        /// Keyboard code for the Dvorak layout
        /// </summary>
        internal const string LayoutDvorak = "00010409";

        /// <summary>
        /// Keyboard code for the Portuguese layout
        /// </summary>
        internal const string LayoutPortuguese = "00000816";

        /// <summary>
        /// Keyboard code for the TurkishF layout
        /// </summary>
        internal const string LayoutTurkishF = "00001055";

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
