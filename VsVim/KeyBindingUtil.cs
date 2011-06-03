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

        internal KeyBindingUtil(CommandsSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        internal KeyBindingUtil(_DTE dte)
            : this(new CommandsSnapshot(dte))
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

            // Include the key used to disable VsVim
            hashSet.Add(buffer.LocalSettings.GlobalSettings.DisableCommand);

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
        internal static bool ShouldSkip(CommandKeyBinding binding)
        {
            if (!IsImportantScope(binding.KeyBinding.Scope))
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
            if (first.KeyModifiers == KeyModifiers.None)
            {
                return true;
            }

            return false;
        }

        internal static bool IsImportantScope(string scope)
        {
            var comp = StringComparer.OrdinalIgnoreCase;
            if (comp.Equals("Global", scope))
            {
                return true;
            }

            if (comp.Equals("Text Editor", scope))
            {
                return true;
            }

            if (comp.Equals(String.Empty, scope))
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
