using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;

namespace VsVim
{
    /// <summary>
    /// Container for the 4 common pieces of data which are needed for an OLE
    /// command.  Makes it easy to pass them around between functions
    /// </summary>
    internal struct OleCommandData
    {
        internal readonly uint CommandId;
        internal readonly uint CommandExecOpt;
        internal readonly IntPtr VariantIn;
        internal readonly IntPtr VariantOut;

        internal OleCommandData(VSConstants.VSStd2KCmdID id)
            : this((uint)id)
        {

        }

        internal OleCommandData(
            uint commandId,
            uint commandExecOpt = 0u)
        {
            CommandId = commandId;
            CommandExecOpt = commandExecOpt;
            VariantIn = IntPtr.Zero;
            VariantOut = IntPtr.Zero;
        }

        internal OleCommandData(
            uint commandId,
            uint commandExecOpt,
            IntPtr variantIn,
            IntPtr variantOut)
        {
            CommandId = commandId;
            CommandExecOpt = commandExecOpt;
            VariantIn = variantIn;
            VariantOut = variantOut;
        }

        internal static OleCommandData Empty
        {
            get { return new OleCommandData(); }
        }

        /// <summary>
        /// Create an OleCommandData for typing the given character.  This causes a native resource
        /// allocation and must be freed at a later time with Release
        /// </summary>
        internal static OleCommandData Allocate(char c)
        {
            var variantIn = Marshal.AllocCoTaskMem(32); // size of(VARIANT), 16 may be enough
            Marshal.GetNativeVariantForObject(c, variantIn);
            return new OleCommandData(
                (uint)VSConstants.VSStd2KCmdID.TYPECHAR,
                0,
                variantIn,
                IntPtr.Zero);
        }

        /// <summary>
        /// Release the contents of the OleCommandData.  If no allocation was performed then this 
        /// will be a no-op
        ///
        /// Do no call this one OleCommandData instances that you don't own.  Calling this on 
        /// parameters created by Visual Studio for example could easily lead to memory corruption
        /// issues
        /// </summary>
        internal static void Release(ref OleCommandData oleCommandData)
        {
            if (oleCommandData.VariantIn != IntPtr.Zero)
            {
                NativeMethods.VariantClear(oleCommandData.VariantIn);
                Marshal.FreeCoTaskMem(oleCommandData.VariantIn);
            }

            if (oleCommandData.VariantOut != IntPtr.Zero)
            {
                NativeMethods.VariantClear(oleCommandData.VariantOut);
                Marshal.FreeCoTaskMem(oleCommandData.VariantOut);
            }

            oleCommandData = new OleCommandData();
        }
    }
}