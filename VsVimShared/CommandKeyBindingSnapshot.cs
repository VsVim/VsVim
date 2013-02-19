using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using EnvDTE;

namespace VsVim
{
    /// <summary>
    /// Snapshot of the current key binding state
    /// </summary>
    public sealed class CommandKeyBindingSnapshot
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

        public bool TryGetCommand(CommandId id, out Command command)
        {
            return _snapshot.TryGetCommand(id, out command);
        }
    }
}
