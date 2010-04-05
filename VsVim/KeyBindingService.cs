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
    /// <summary>
    /// Responsible for dealing with the conflicting key bindings inside of Visual Studio
    /// </summary>
    [Export(typeof(KeyBindingService))]
    public sealed class KeyBindingService
    {
        private readonly IVsUIShell _vsShell;
        private readonly _DTE _dte;
        private bool _hasChecked;

        [ImportingConstructor]
        public KeyBindingService(SVsServiceProvider sp)
        {
            _vsShell = sp.GetService<SVsUIShell, IVsUIShell>();
            _dte = sp.GetService<SDTE, _DTE>();
        }

        public void OneTimeCheckForConflictingKeyBindings(_DTE dte, IVimBuffer buffer)
        {
            if (dte == null)
            {
                throw new ArgumentNullException("dte");
            }

            if (_hasChecked)
            {
                return;
            }
            _hasChecked = true;
            CheckForConflictingKeyBindings(buffer);
        }

        /// <summary>
        /// Check for and remove conflicting key bindings
        /// </summary>
        private void CheckForConflictingKeyBindings(IVimBuffer buffer)
        {
            var hashSet = new HashSet<KeyInput>(
                buffer.AllModes.Select(x => x.Commands).SelectMany(x => x));
            hashSet.Add(buffer.Settings.GlobalSettings.DisableCommand);
            var snapshot = new CommandsSnapshot(_dte);
            var list = FindConflictingCommandKeyBindings(snapshot, hashSet);
            if (list.Count > 0)
            {
                var msg = new StringBuilder();
                msg.AppendLine("Conflicting key bindings found.  Would you like to inspect and remove?");
                using (var modalDisplay = _vsShell.EnableModelessDialog())
                {
                    var res = MessageBox.Show(
                        caption: "VsVim",
                        messageBoxText: msg.ToString(),
                        button: MessageBoxButton.YesNo);
                    if (res == MessageBoxResult.Yes)
                    {
                        DoShowOptionsDialog(snapshot, FindKeyBindingsMarkedAsRemoved(), list);
                    }
                }
            }
        }

        private void DoShowOptionsDialog(
            CommandsSnapshot snapshot,
            IEnumerable<CommandKeyBinding> previouslyRemovedKeyBindings,
            IEnumerable<CommandKeyBinding> conflictingKeyBindings)
        {
            var window = new UI.ConflictingKeyBindingDialog();
            var removed = window.ConflictingKeyBindingControl.RemovedKeyBindingData;
            removed.AddRange(previouslyRemovedKeyBindings.Select(x => new KeyBindingData(x)));
            var current = window.ConflictingKeyBindingControl.ConflictingKeyBindingData;
            current.AddRange(conflictingKeyBindings.Select(x => new KeyBindingData(x)));
            var ret = window.ShowModal();
            if (ret.HasValue && ret.Value)
            {
                // Remove all of the removed bindings
                foreach (var cur in removed)
                {
                    var tuple = snapshot.TryGetCommand(cur.Name);
                    if ( tuple.Item1 )
                    {
                        tuple.Item2.SafeResetBindings();
                    }
                }

                // Restore all of the conflicting ones
                foreach (var cur in current)
                {
                    KeyBinding binding;
                    var tuple = snapshot.TryGetCommand(cur.Name);
                    if ( tuple.Item1 && KeyBinding.TryParse(cur.Keys, out binding))
                    {
                        tuple.Item2.SafeSetBindings(binding);
                    }
                }

                var settings = Settings.Settings.Default;
                settings.RemovedBindings = 
                    removed
                    .Select(x => new Settings.CommandBindingSetting() { Name = x.Name, CommandString = x.Keys })
                    .ToArray();
                settings.HaveUpdatedKeyBindings = true;
                settings.Save();
            }
        }

        /// <summary>
        /// Find all of the Command instances which have conflicting key bindings
        /// </summary>
        public static List<CommandKeyBinding> FindConflictingCommandKeyBindings(
            CommandsSnapshot snapshot,
            HashSet<KeyInput> neededInputs)
        {
            var list = new List<CommandKeyBinding>();
            foreach (var binding in snapshot.CommandKeyBindings.Where(x => !ShouldSkip(x)))
            {
                var input = binding.KeyBinding.FirstKeyInput;
                if (neededInputs.Contains(input))
                {
                    list.Add(binding);
                    break;
                }
            }

            return list;
        }

        /// <summary>
        /// Should this be skipped when removing conflicting bindings?
        /// </summary>
        public static bool ShouldSkip(CommandKeyBinding binding)
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

        public static bool IsImportantScope(string scope)
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

        public static List<CommandKeyBinding> FindRemovedKeyBindings(CommandsSnapshot snapshot)
        {
            return FindKeyBindingsMarkedAsRemoved().Where(x => !snapshot.IsKeyBindingActive(x.KeyBinding)).ToList();
        }

        /// <summary>
        /// Find all of the key bindings which have been removed
        /// </summary>
        public static List<CommandKeyBinding> FindKeyBindingsMarkedAsRemoved()
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
