using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VsVim
{
    /// <summary>
    /// MEF importable interface for creating an options dialog
    /// </summary>
    public interface IOptionsDialogService
    {
        /// <summary>
        /// Show the conflicting key bindings dialog.  Returns true if the dialog was accepted
        /// and false if it was cancelled
        /// </summary>
        bool ShowConflictingKeyBindingsDialog(CommandKeyBindingSnapshot snapshot);   
    }
}
