using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim;

namespace VsVim
{
    public enum ConflictingKeyBindingState
    {
        HasNotChecked,
        FoundConflicts,
        NoConflicts
    }

    /// <summary>
    /// Service for Key Binding information in Visual Studio
    /// </summary>
    public interface IKeyBindingService
    {
        ConflictingKeyBindingState ConflictingKeyBindingState { get; }

        event EventHandler ConflictingKeyBindingStateChanged;

        /// <summary>
        /// Start the check for conflicting key bindings.  This method will return immediately but can
        /// operate asynchrounously.  If the check has already run this method will exit immediately
        /// </summary>
        void RunConflictingKeyBindingStateCheck(IVimBuffer buffer, Action<ConflictingKeyBindingState, CommandKeyBindingSnapshot> onComplete);

        /// <summary>
        /// Reset the checked state of the key binding service
        /// </summary>
        void ResetConflictingKeyBindingState();

        /// <summary>
        /// Resolve any conflicts which may exist
        /// </summary>
        void ResolveAnyConflicts();
    }
}
