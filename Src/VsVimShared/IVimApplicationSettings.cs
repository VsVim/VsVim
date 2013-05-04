
using System.Collections.ObjectModel;
namespace VsVim
{
    /// <summary>
    /// Settings specific to the VsVim application.  These specifically don't include Vim specific
    /// settings but instead have items like first usage, first import, etc ... 
    /// </summary>
    public interface IVimApplicationSettings
    {
        /// <summary>
        /// The key bindings were updated 
        /// </summary>
        bool HaveUpdatedKeyBindings { get; set; }

        /// <summary>
        /// The conflicting key binding margin was ignored
        /// </summary>
        bool IgnoredConflictingKeyBinding { get; set; }

        /// <summary>
        /// The legacy settings have been migrated to this usage
        /// </summary>
        bool LegacySettingsMigrated { get; set; } 

        /// <summary>
        /// The set of CommandKeyBinding that VsVim unbound in the conflicting key dialog
        /// </summary>
        ReadOnlyCollection<CommandKeyBinding> RemovedBindings { get; set; }
    }
}
