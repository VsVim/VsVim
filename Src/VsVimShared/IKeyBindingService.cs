using System;
using System.Collections.Generic;
using Vim;

namespace Vim.VisualStudio
{
    public enum ConflictingKeyBindingState
    {
        HasNotChecked,
        FoundConflicts,
        ConflictsIgnoredOrResolved
    }

    /// <summary>
    /// Service for Key Binding information in Visual Studio
    /// </summary>
    public interface IKeyBindingService
    {
        ConflictingKeyBindingState ConflictingKeyBindingState { get; }

        event EventHandler ConflictingKeyBindingStateChanged;

        /// <summary>
        /// Create a CommandKeyBindingSnapshot for the given IVimBuffer
        /// </summary>
        CommandKeyBindingSnapshot CreateCommandKeyBindingSnapshot(IVimBuffer buffer);

        /// <summary>
        /// Start the check for conflicting key bindings.  This method will return immediately but can
        /// operate asynchrounously.  If the check has already run this method will exit immediately
        /// </summary>
        void RunConflictingKeyBindingStateCheck(IVimBuffer buffer);

        /// <summary>
        /// Reset the checked state of the key binding service
        /// </summary>
        void ResetConflictingKeyBindingState();

        /// <summary>
        /// Resolve any conflicts which may exist
        /// </summary>
        void ResolveAnyConflicts();

        /// <summary>
        /// Ignore the conflicts
        /// </summary>
        void IgnoreAnyConflicts(); 
    }
}
