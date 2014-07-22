using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.OLE.Interop;

namespace Vim.VisualStudio
{
    internal enum CommandStatus
    {
        /// <summary>
        /// Command is enabled
        /// </summary>
        Enable,

        /// <summary>
        /// Command is disabled
        /// </summary>
        Disable,

        /// <summary>
        /// VsVim isn't concerned about the command and it's left to the next IOleCommandTarget
        /// to determine if it's enabled or not
        /// </summary>
        PassOn,
    }

    internal interface ICommandTarget
    {
        CommandStatus QueryStatus(EditCommand editCommand);

        bool Exec(EditCommand editCommand, out Action action);
    }

    internal interface ICommandTargetFactory
    {
        ICommandTarget CreateCommandTarget(IOleCommandTarget nextCommandTarget, IVimBufferCoordinator vimBufferCoordinator);
    }
}
