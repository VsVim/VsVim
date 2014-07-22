using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;

namespace Vim.VisualStudio
{
    internal static class NativeMethods
    {
        public static int S_OK = VSConstants.S_OK;
        public static int GWLP_WNDPROC = -4;
        public static uint WM_KEYDOWN = 0x100;
        public static uint WM_KEYUP = 0x101;
        public static uint VK_ESCAPE = 0x1b;

        internal static bool IsSameComObject(object left, object right)
        {
            var leftPtr = IntPtr.Zero;
            var rightPtr = IntPtr.Zero;
            try
            {
                if (left == null || right == null)
                {
                    return false;
                }

                leftPtr = Marshal.GetIUnknownForObject(left);
                rightPtr = Marshal.GetIUnknownForObject(right);
                return leftPtr == rightPtr;
            }
            finally
            {
                if (leftPtr != IntPtr.Zero)
                {
                    Marshal.Release(leftPtr);
                }
                if (rightPtr != IntPtr.Zero)
                {
                    Marshal.Release(rightPtr);
                }
            }
        }

        internal delegate int WindowProcCallback(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("Oleaut32.dll", PreserveSig = false)]
        internal static extern void VariantClear(IntPtr variant);

        [DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr hWnd, int index, int newLong);

        [DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr hWnd, int index, WindowProcCallback del);

        [DllImport("user32.dll")]
        internal static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll")]
        internal static extern int CallWindowProc(IntPtr previousWindowProc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    }
}
