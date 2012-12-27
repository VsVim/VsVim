
using System.Collections.ObjectModel;
namespace VsVim
{
    /// <summary>
    /// Settings specific to the VsVim application.  These specifically don't include Vim specific
    /// settings but instead have items like first usage, first import, etc ... 
    /// </summary>
    internal interface IVimApplicationSettings
    {
        bool HaveUpdatedKeyBindings { get; set; }
        bool IgnoredConflictingKeyBinding { get; set; }
        ReadOnlyCollection<CommandKeyBinding> RemovedBindings { get; set; }
    }
}
