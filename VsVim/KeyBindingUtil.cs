using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim;
using System.Windows.Input;
using EnvDTE;
using System.ComponentModel.Composition.Primitives;
using System.ComponentModel.Composition;
using System.Windows;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System.Collections.ObjectModel;
using Microsoft.Internal.VisualStudio.PlatformUI;
using System.Threading;
using VsVim.UI;

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
        /// Check for and remove conflicting key bindings
        /// </summary>
        internal CommandKeyBindingSnapshot CreateCommandKeyBindingSnapshot(IVimBuffer buffer)
        {
            var hashSet = new HashSet<KeyInput>(
                buffer.AllModes
                .Select(x => x.CommandNames)
                .SelectMany(x => x)
                .Where(x => x.KeyInputs.Length > 0)
                .Select(x => x.KeyInputs.First()));
            hashSet.Add(buffer.Settings.GlobalSettings.DisableCommand);
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
        /// Find all of the Command instances which have conflicting key bindings
        /// </summary>
        internal List<CommandKeyBinding> FindConflictingCommandKeyBindings( HashSet<KeyInput> neededInputs)
        {
            var list = new List<CommandKeyBinding>();
            var all =  _snapshot.CommandKeyBindings.Where(x => !ShouldSkip(x));
            foreach (var binding in all)
            {
                var input = binding.KeyBinding.FirstKeyInput;
                if (neededInputs.Contains(input))
                {
                    list.Add(binding);
                }
            }

            return list;
        }

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

            if (!binding.KeyBinding.KeyInputs.Any())
            {
                return true;
            }

            var first = binding.KeyBinding.FirstKeyInput;

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
            var settings = Settings.Settings.Default;
            IEnumerable<Tuple<string, string>> source = null;
            if (settings.HaveUpdatedKeyBindings)
            {
                source = settings.RemovedBindings.Select(x => Tuple.Create(x.Name, x.CommandString));
            }
            else
            {
                source = Constants.CommonlyUnboundCommands.Select(x => Tuple.Create(x.Item1, x.Item3));
            }

            var list = new List<CommandKeyBinding>();
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
