using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

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

        internal const uint VK_DECIMAL = 0x6E;

        internal const uint VK_SHIFT = 0x10;

        internal const uint VK_CONTROL = 0x11;

        internal const uint VK_MENU = 0x12;

        internal const uint VK_CAPITAL = 0x14;

        internal const uint INFINITE = 0xffffffff;

        internal const uint VK_LSHIFT = 0xA0;

        internal const uint VK_LCONTROL = 0xA2;

        internal const uint VK_LMENU = 0xA4;

        internal const uint MAPVK_VK_TO_VSC = 0x0;

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

        /// <summary>
        /// Keyboard code for the French layout
        /// </summary>
        internal const string LayoutFrench = "0000040C";

        [DllImport("user32.dll")]
        public static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        public static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        public static extern bool EmptyClipboard();

        [DllImport("user32.dll")]
        public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll")]
        internal static extern uint GetCaretBlinkTime();

        [DllImport("user32.dll")]
        internal static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        internal static extern short VkKeyScanEx(char ch, IntPtr hkl);

        [DllImport("user32.dll")]
        internal static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr keyboardId);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        internal static extern bool GetKeyboardLayoutName(char[] name);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        internal static extern uint GetKeyboardLayoutList(int count, [Out, MarshalAs(UnmanagedType.LPArray)] IntPtr[] list);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetKeyboardState(byte[] keyState);

        [DllImport("user32.dll")]
        internal static extern short GetKeyState(uint virtualKey);

        [DllImport("user32.dll")]
        internal static extern IntPtr LoadKeyboardLayout([In] string id, uint flags);

        [DllImport("user32.dll")]
        internal static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint Flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int ToUnicodeEx(
            uint virtualKey,
            uint scanCode,
            byte[] keyState,
            [Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 4)]
            StringBuilder buffer,
            int bufferSize,
            uint flags,
            IntPtr keyboardLayout);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnloadKeyboardLayout(IntPtr keyboardId);

        /// <summary>
        /// Load the IntPtr for the given keyboard layout.  Returns a bool as to whether or not the 
        /// layout needs to be unloaded when you are through with it 
        /// </summary>
        internal static IntPtr LoadKeyboardLayout(string id, uint flags, out bool needUnload)
        {
            var previous = GetKeyboardLayoutList();
            var layout = LoadKeyboardLayout(id, flags);

            needUnload = layout != IntPtr.Zero && !previous.Contains(layout);
            return layout;
        }

        internal static IntPtr[] GetKeyboardLayoutList()
        {
            var count = (int)GetKeyboardLayoutList(0, null);
            if (count == 0)
            {
                return new IntPtr[] { };
            }

            var array = new IntPtr[count];
            if (GetKeyboardLayoutList(count, array) == 0)
            {
                return new IntPtr[] { };
            }

            return array;
        }

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
