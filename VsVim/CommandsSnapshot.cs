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
        private readonly Dictionary<string, Command> _commandMap = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);
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
                _commandMap[command.Name] = command;
                list.AddRange(command.GetCommandKeyBindings());
            }
            _commandKeyBindings = list.AsReadOnly();
            _keyBindings = new HashSet<KeyBinding>(KeyBindings);
        }

        public bool IsKeyBindingActive(KeyBinding binding)
        {
            return _keyBindings.Contains(binding);
        }

        public Tuple<bool, Command> TryGetCommand(string name)
        {
            Command command;
            var ret = _commandMap.TryGetValue(name, out command);
            return Tuple.Create(ret, command);
        }
    }
}
