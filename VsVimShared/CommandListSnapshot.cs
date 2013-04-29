using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using EnvDTE;

namespace VsVim
{
    /// <summary>
    /// Snapshot of the state of the DTE.Commands and their KeyBindings
    /// </summary>
    public sealed class CommandsSnapshot
    {
        private readonly Dictionary<CommandId, Command> _commandMap = new Dictionary<CommandId, Command>();
        private readonly ReadOnlyCollection<CommandKeyBinding> _commandKeyBindings;
        private readonly HashSet<KeyBinding> _keyBindings;

        public ReadOnlyCollection<CommandKeyBinding> CommandKeyBindings
        {
            get { return _commandKeyBindings; }
        }

        public IEnumerable<KeyBinding> KeyBindings
        {
            get { return _commandKeyBindings.Select(x =>x.KeyBinding); }
        }

        public CommandsSnapshot(_DTE dte) : this(dte.Commands.GetCommands())
        {
        }

        public CommandsSnapshot(IEnumerable<Command> commands)
        {
            var list = new List<CommandKeyBinding>();
            foreach (var command in commands)
            {
                CommandId commandId;
                if (!command.TryGetCommandId(out commandId))
                {
                    continue;
                }

                _commandMap[commandId] = command;
                list.AddRange(command.GetCommandKeyBindings());
            }
            _commandKeyBindings = list.AsReadOnly();
            _keyBindings = new HashSet<KeyBinding>(KeyBindings);
        }

        public bool IsKeyBindingActive(KeyBinding binding)
        {
            return _keyBindings.Contains(binding);
        }

        public bool TryGetCommand(CommandId id, out Command command)
        {
            return _commandMap.TryGetValue(id, out command);
        }
    }
}
