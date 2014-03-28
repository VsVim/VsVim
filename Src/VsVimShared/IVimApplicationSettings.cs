using System;
using System.Collections.ObjectModel;
using Vim;

namespace VsVim
{
    /// <summary>
    /// Arguments passed to application settings changed event handlers
    /// </summary>
    public class ApplicationSettingsEventArgs : EventArgs
    {
    }

    /// <summary>
    /// Settings specific to the VsVim application.  These specifically don't include Vim specific
    /// settings but instead have items like first usage, first import, etc ... 
    /// </summary>
    public interface IVimApplicationSettings
    {
        /// <summary>
        /// The default settings that should be used 
        /// </summary>
        DefaultSettings DefaultSettings { get; set; }

        /// <summary>
        /// Do we want to track events like external edits in R#, snippets, etc ...
        /// </summary>
        bool EnableExternalEditMonitoring { get; set; }

        /// <summary>
        /// The key bindings were updated 
        /// </summary>
        bool HaveUpdatedKeyBindings { get; set; }

        /// <summary>
        /// Have the settings for 'backspace' and 'whichwrap'
        /// </summary>
        bool HaveNotifiedBackspaceSetting { get; set; }

        /// <summary>
        /// The conflicting key binding margin was ignored
        /// </summary>
        bool IgnoredConflictingKeyBinding { get; set; }

        /// <summary>
        /// Is the bad key mapping issue introduced in 1.4.0 fixed on this installation
        /// </summary>
        bool KeyMappingIssueFixed { get; set; }

        /// <summary>
        /// The set of CommandKeyBinding that VsVim unbound in the conflicting key dialog
        /// </summary>
        ReadOnlyCollection<CommandKeyBinding> RemovedBindings { get; set; }

        /// <summary>
        /// Raised when a settings changes
        /// </summary>
        event EventHandler<ApplicationSettingsEventArgs> SettingsChanged;
    }
}
