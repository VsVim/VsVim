using System;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using Vim;

namespace VsVim
{
    internal sealed class KeyBindingUtil
    {
        private readonly CommandsSnapshot _snapshot;
        private readonly HashSet<string> _importantScopeSet;

        internal KeyBindingUtil(CommandsSnapshot snapshot, HashSet<string> importantScopeSet)
        {
            _snapshot = snapshot;
            _importantScopeSet = importantScopeSet;
        }

        internal KeyBindingUtil(_DTE dte, HashSet<string> importantScopeSet)
            : this(new CommandsSnapshot(dte), importantScopeSet)
        {

        }

        /// <summary>
        /// Compute the set of keys that conflict with and have been already been removed.
        /// </summary>
        internal CommandKeyBindingSnapshot CreateCommandKeyBindingSnapshot(IVimBuffer buffer)
        {
            // Get the list of all KeyInputs that are the first key of a VsVim command
            var hashSet = new HashSet<KeyInput>(
                buffer.AllModes
                .Select(x => x.CommandNames)
                .SelectMany(x => x)
                .Where(x => x.KeyInputs.Length > 0)
                .Select(x => x.KeyInputs.First()));

            // Need to get the custom key bindings in the list.  It's very common for users 
            // to use for example function keys (<F2>, <F3>, etc ...) in their mappings which
            // are often bound to other Visual Studio commands.
            var keyMap = buffer.Vim.KeyMap;
            foreach (var keyRemapMode in KeyRemapMode.All)
            {
                foreach (var keyMapping in keyMap.GetKeyMappingsForMode(keyRemapMode))
                {
                    keyMapping.Left.KeyInputs.ForEach(keyInput => hashSet.Add(keyInput));
                }
            }

            // Include the key used to disable VsVim
            hashSet.Add(buffer.LocalSettings.GlobalSettings.DisableAllCommand);

            return CreateCommandKeyBindingSnapshot(hashSet);
        }

        internal CommandKeyBindingSnapshot CreateCommandKeyBindingSnapshot(HashSet<KeyInput> needed)
        {
            var conflicting = FindConflictingCommandKeyBindings(needed);
            var removed = FindRemovedKeyBindings();
            return new CommandKeyBindingSnapshot(
                _snapshot,
                removed,
                conflicting);
        }

        /// <summary>
        /// Find all of the Command instances (which represent Visual Studio commands) which would conflict with any
        /// VsVim commands that use the keys in neededInputs.
        /// </summary>
        internal List<CommandKeyBinding> FindConflictingCommandKeyBindings(HashSet<KeyInput> neededInputs)
        {
            var list = new List<CommandKeyBinding>();
            var all = _snapshot.CommandKeyBindings.Where(x => !ShouldSkip(x));
            foreach (var binding in all)
            {
                var input = binding.KeyBinding.FirstKeyStroke.AggregateKeyInput;
                if (neededInputs.Contains(input))
                {
                    list.Add(binding);
                }
            }

            return list;
        }

        /// <summary>
        /// Returns the list of commands that were previously removed by the user and are no longer currently active.
        /// </summary>
        internal List<CommandKeyBinding> FindRemovedKeyBindings()
        {
            return FindKeyBindingsMarkedAsRemoved().Where(x => !_snapshot.IsKeyBindingActive(x.KeyBinding)).ToList();
        }

        /// <summary>
        /// Should this be skipped when removing conflicting bindings?
        /// </summary>
        internal bool ShouldSkip(CommandKeyBinding binding)
        {
            var scope = binding.KeyBinding.Scope;
            if (!_importantScopeSet.Contains(scope))
            {
                return true;
            }

            if (!binding.KeyBinding.KeyStrokes.Any())
            {
                return true;
            }

            var first = binding.KeyBinding.FirstKeyStroke;

            // We don't want to remove any mappings which don't include a modifier key 
            // because it removes too many mappings.  Without this check we would for
            // example remove Delete in insert mode, arrow keys for intellisense and 
            // general navigation, space bar for completion, etc ...
            //
            // One exception is function keys.  They are only bound in Vim to key 
            // mappings and should win over VS commands since users explicitly 
            // want them to occur
            if (first.KeyModifiers == KeyModifiers.None && !first.KeyInput.IsFunctionKey)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Find all of the key bindings which have been removed
        /// </summary>
        internal static List<CommandKeyBinding> FindKeyBindingsMarkedAsRemoved()
        {
            var list = new List<CommandKeyBinding>();
            var settings = Settings.Settings.Default;
            if (!settings.HaveUpdatedKeyBindings)
            {
                return list;
            }

            var source = settings.RemovedBindings.Select(x => Tuple.Create(x.Name, x.CommandString));
            foreach (var tuple in source)
            {
                KeyBinding binding;
                if (KeyBinding.TryParse(tuple.Item2, out binding))
                {
                    list.Add(new CommandKeyBinding(tuple.Item1, binding));
                }
            }

            return list;
        }
    }
}
