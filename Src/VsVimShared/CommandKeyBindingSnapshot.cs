using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using EnvDTE;
using Vim;
using EditorUtils;

namespace Vim.VisualStudio
{
    /// <summary>
    /// Snapshot of the current key binding state
    /// </summary>
    public sealed class CommandKeyBindingSnapshot
    {
        private readonly CommandListSnapshot _commandListSnapshot;
        private readonly ReadOnlyCollection<CommandKeyBinding> _removedBindings;
        private readonly ReadOnlyCollection<CommandKeyBinding> _conflictingBindings;
        private readonly ReadOnlyCollection<KeyInput> _vimFirstKeyInputs;

        public CommandListSnapshot CommandListSnapshot
        {
            get { return _commandListSnapshot; }
        }

        public ReadOnlyCollection<CommandKeyBinding> Removed
        {
            get { return _removedBindings; }
        }

        public ReadOnlyCollection<CommandKeyBinding> Conflicting
        {
            get { return _conflictingBindings; }
        }

        public ReadOnlyCollection<KeyInput> VimFirstKeyInputs
        {
            get { return _vimFirstKeyInputs; }
        }

        public CommandKeyBindingSnapshot(
            CommandListSnapshot snapshot,
            IEnumerable<KeyInput> vimFirstKeyInputs,
            IEnumerable<CommandKeyBinding> removed,
            IEnumerable<CommandKeyBinding> conflicting)
        {
            _commandListSnapshot = snapshot;
            _vimFirstKeyInputs = vimFirstKeyInputs.ToReadOnlyCollection();
            _removedBindings = removed.ToReadOnlyCollection();
            _conflictingBindings = conflicting.ToReadOnlyCollection();
        }

        public bool TryGetCommand(CommandId id, out EnvDTE.Command command)
        {
            return _commandListSnapshot.TryGetCommand(id, out command);
        }

        public bool TryGetCommandKeyBindings(CommandId id, out ReadOnlyCollection<CommandKeyBinding> bindings)
        {
            return _commandListSnapshot.TryGetCommandKeyBindings(id, out bindings);
        }

        public bool TryGetCommandData(CommandId id, out EnvDTE.Command command, out ReadOnlyCollection<CommandKeyBinding> bindings)
        {
            return _commandListSnapshot.TryGetCommandData(id, out command, out bindings);
        }
    }
}
