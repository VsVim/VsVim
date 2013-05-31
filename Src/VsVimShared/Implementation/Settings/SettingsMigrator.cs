using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Windows.Threading;
using EditorUtils;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Vim;

namespace VsVim.Implementation.Settings
{
    /// <summary>
    /// This class is used to migrate the old ILegacySettings information into the IVimApplicationSettings
    /// storage.  It should only ever run once on an installation
    /// </summary>
    internal sealed class SettingsMigrator 
    {
        private readonly _DTE _dte;
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly ILegacySettings _legacySettings;

        internal SettingsMigrator(_DTE dte, IVimApplicationSettings vimApplicationSettings, ILegacySettings legacySettings)
        {
            _dte = dte;
            _vimApplicationSettings = vimApplicationSettings;
            _legacySettings = legacySettings;
        }

        internal void DoMigration()
        {
            var removedBindings = FindRemovedBindings();
            DoMigration(removedBindings);
        }

        internal void DoMigration(ReadOnlyCollection<CommandKeyBinding> removedBindings)
        {
            _vimApplicationSettings.HaveUpdatedKeyBindings = _legacySettings.HaveUpdatedKeyBindings;
            _vimApplicationSettings.IgnoredConflictingKeyBinding = _legacySettings.IgnoredConflictingKeyBinding;
            _vimApplicationSettings.RemovedBindings = removedBindings;
        }

        /// <summary>
        /// The ILegacySettings implementation incorrectly believed that every VS command had a unique
        /// name associated with it (this is one of the primary problems with the implementation).  This is
        /// not true as commands can, and do, duplicate names.  They are unique based on the combination
        /// of Command ID and Group GUID (today this is captured in CommandId).
        /// 
        /// Hence the migration code is working with incomplete data and must use a heuristic.  We simply
        /// use name matching to determine what the correct CommandId is for a given setting
        /// </summary>
        private Dictionary<string, List<CommandId>> GetCommandNameToIdMap()
        {
            var map = new Dictionary<string, List<CommandId>>();
            foreach (var command in _dte.Commands.GetCommands())
            {
                string name;
                CommandId commandId;
                if (!command.TryGetCommandId(out commandId) || !command.TryGetName(out name))
                {
                    continue;
                }

                List<CommandId> list;
                if (!map.TryGetValue(name, out list))
                {
                    list = new List<CommandId>(1);
                    map[name] = list;
                }

                list.Add(commandId);
            }

            return map;
        }

        private ReadOnlyCollection<CommandKeyBinding> FindRemovedBindings()
        {
            var nameToIdMap = GetCommandNameToIdMap();
            var list = new List<CommandKeyBinding>();
            foreach (var commandSetting in _legacySettings.RemovedBindings)
            {
                List<CommandId> idList;
                KeyBinding keyBinding;
                if (nameToIdMap.TryGetValue(commandSetting.Name, out idList) &&
                    KeyBinding.TryParse(commandSetting.CommandString, out keyBinding))
                {
                    foreach (var id in idList)
                    {
                        var binding = new CommandKeyBinding(id, commandSetting.Name, keyBinding);
                        list.Add(binding);
                    }
                }
            }

            return list.ToReadOnlyCollectionShallow();
        }
    }
}
