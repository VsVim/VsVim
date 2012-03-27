using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace VsVim
{
    /// <summary>
    /// VsVim originally stored it's settings via application settings.  This is not a good way to 
    /// store settings in Visual Studio as it's not service pack safe (all settings erased in a 
    /// service pack update).  
    ///
    /// Also it's potentially not safe to migrate them between DLL locations.  Hence even though
    /// much of the logic for Visual Studio moved to a new DLL the actual setting load service 
    /// needs to remain in VsVim.  
    /// 
    /// This interface is a transition mechanism between the old application settings layer and the
    /// new IVsSettings layer.  It's exposed as a MEF compoment which is hooked into in the 
    /// VsVim layer.
    /// </summary>
    public interface ILegacySettings
    {
        bool HaveUpdatedKeyBindings { get; set; }
        bool IgnoredConflictingKeyBinding { get; set; }
        ReadOnlyCollection<CommandBindingSetting> RemovedBindings { get; set; }

        void Save();
    }

    public struct CommandBindingSetting
    {
        public readonly string Name;
        public readonly string CommandString;

        public CommandBindingSetting(string name, string commandString)
        {
            Name = name;
            CommandString = commandString;
        }
    }

    internal static class LegacySettingsExtensions
    {
        /// <summary>
        /// Find all of the key bindings which have been removed
        /// </summary>
        internal static List<CommandKeyBinding> FindKeyBindingsMarkedAsRemoved(this ILegacySettings settings)
        {
            var list = new List<CommandKeyBinding>();
            if (!settings.HaveUpdatedKeyBindings)
            {
                return list;
            }

            foreach (var commandBindingSetting in settings.RemovedBindings)
            {
                KeyBinding binding;
                if (KeyBinding.TryParse(commandBindingSetting.CommandString, out binding))
                {
                    list.Add(new CommandKeyBinding(commandBindingSetting.Name, binding));
                }
            }

            return list;
        }
    }
}
