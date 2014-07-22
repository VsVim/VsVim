using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;

namespace Vim.VisualStudio
{
    /// <summary>
    /// Container for the 4 common pieces of data which are needed for an OLE
    /// command.  Makes it easy to pass them around between functions
    /// </summary>
    internal sealed class OleCommandData : IDisposable
    {
        internal readonly CommandId _commandId;
        internal readonly uint _commandExecOpt;
        internal IntPtr _variantIn;
        internal IntPtr _variantOut;

        internal CommandId CommandId
        {
            get { return _commandId; }
        }

        internal Guid Group
        {
            get { return _commandId.Group; }
        }

        internal uint Id
        {
            get { return _commandId.Id; }
        }

        internal uint CommandExecOpt
        {
            get { return _commandExecOpt; }
        }

        internal IntPtr VariantIn
        {
            get { return _variantIn; }
        }

        internal IntPtr VariantOut
        {
            get { return _variantOut; }
        }

        internal OleCommandData(VSConstants.VSStd97CmdID id)
            : this(VSConstants.GUID_VSStandardCommandSet97, (uint)id)
        {

        }

        internal OleCommandData(VSConstants.VSStd2KCmdID id)
            : this(VSConstants.VSStd2K, (uint)id)
        {

        }

        internal OleCommandData(
            Guid commandGroup,
            uint commandId,
            uint commandExecOpt = 0u)
            : this(commandGroup, commandId, commandExecOpt, IntPtr.Zero, IntPtr.Zero)
        {

        }

        internal void Dispose()
        {
            Dispose(true);
        }

        private OleCommandData(
            Guid commandGroup,
            uint commandId,
            uint commandExecOpt,
            IntPtr variantIn,
            IntPtr variantOut)
        {
            _commandId = new CommandId(commandGroup, commandId);
            _commandExecOpt = commandExecOpt;
            _variantIn = variantIn;
            _variantOut = variantOut;
        }

        ~OleCommandData()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            try
            {
                if (_variantIn != IntPtr.Zero)
                {
                    NativeMethods.VariantClear(_variantIn);
                    Marshal.FreeCoTaskMem(_variantIn);
                }

                if (_variantOut != IntPtr.Zero)
                {
                    NativeMethods.VariantClear(_variantOut);
                    Marshal.FreeCoTaskMem(_variantOut);
                }
            }
            finally
            {
                _variantIn = IntPtr.Zero;
                _variantOut = IntPtr.Zero;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose();
        }

        internal static OleCommandData Empty
        {
            get { return new OleCommandData(Guid.Empty, 0); }
        }

        /// <summary>
        /// Create an OleCommandData for typing the given character.  This causes a native resource
        /// allocation and must be freed at a later time with Release
        /// </summary>
        internal static OleCommandData CreateTypeChar(char c)
        {
            var variantIn = Marshal.AllocCoTaskMem(32); // size of(VARIANT), 16 may be enough
            Marshal.GetNativeVariantForObject(c, variantIn);
            return new OleCommandData(
                VSConstants.VSStd2K,
                (uint)VSConstants.VSStd2KCmdID.TYPECHAR,
                0,
                variantIn,
                IntPtr.Zero);
        }
    }
}