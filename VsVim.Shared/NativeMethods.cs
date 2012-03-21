using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;

namespace VsVim
{
    internal static class NativeMethods
    {
        public static int S_OK = VSConstants.S_OK;

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

        [DllImport("Oleaut32.dll", PreserveSig = false)]
        internal static extern void VariantClear(IntPtr variant);
    }
}
