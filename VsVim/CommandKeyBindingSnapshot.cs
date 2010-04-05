using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using EnvDTE;

namespace VsVim
{
    /// <summary>
    /// Snapshot of the current key binding state
    /// </summary>
    public class CommandKeyBindingSnapshot
    {
        private readonly CommandsSnapshot _snapshot;
        private readonly ReadOnlyCollection<CommandKeyBinding> _removedBindings;
        private readonly ReadOnlyCollection<CommandKeyBinding> _conflictingBindings;

        public CommandsSnapshot CommandsSnapshot
        {
            get { return _snapshot; }
        }

        public ReadOnlyCollection<CommandKeyBinding> Removed
        {
            get { return _removedBindings; }
        }

        public ReadOnlyCollection<CommandKeyBinding> Conflicting
        {
            get { return _conflictingBindings; }
        }

        public CommandKeyBindingSnapshot(
            CommandsSnapshot snapshot,
            IEnumerable<CommandKeyBinding> removed,
            IEnumerable<CommandKeyBinding> conflicting)
        {
            _snapshot = snapshot;
            _removedBindings = removed.ToList().AsReadOnly();
            _conflictingBindings = conflicting.ToList().AsReadOnly();
        }

        public Tuple<bool, Command> TryGetCommand(string name)
        {
            return _snapshot.TryGetCommand(name);
        }
    }
}
