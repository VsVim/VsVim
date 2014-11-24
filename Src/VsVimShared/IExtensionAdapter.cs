using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Vim.VisualStudio
{
    /// <summary>
    /// Interface for adding support for other extensions that interact with VsVim
    /// </summary>
    internal interface IExtensionAdapter
    {
        bool ShouldKeepSelectionAfterHostCommand(string command, string argument);
    }

    /// <summary>
    /// MEF importable interface which aggregates all of the IExtensionAdapter values in 
    /// the system
    /// </summary>
    internal interface IExtensionAdapterBroker : IExtensionAdapter
    {
        ReadOnlyCollection<IExtensionAdapter> ExtensionAdapters
        {
            get;
        }
    }
}
