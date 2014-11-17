using System;

namespace Vim.VisualStudio
{
    /// <summary>
    /// The values in this class must match up with the IDSymbol values in VsVim.vsct
    /// </summary>
    internal static class CommandIds
    {
        internal const uint Options = 0x100;
        internal const uint DumpKeyboard = 0x101;
        internal const uint ClearTSQLBindings = 0x102;
    };
}