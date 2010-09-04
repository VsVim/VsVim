using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace VsVim.UnitTest.Utils
{
    public sealed class CharPointer : IDisposable
    {
        public readonly Char Char;
        public IntPtr IntPtr;

        private CharPointer(char c, IntPtr ptr)
        {
            Char = c;
            IntPtr = ptr;
        }

        void IDisposable.Dispose()
        {
            if (IntPtr.Zero != IntPtr)
            {
                Marshal.FreeCoTaskMem(IntPtr);
                IntPtr = IntPtr.Zero;
            }
        }

        public static CharPointer Create(char c)
        {
            var ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(Char)));
            Marshal.GetNativeVariantForObject(c, ptr);
            return new CharPointer(c, ptr);
        }
    }
}
