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

namespace VsVim
{
    /// <summary>
    /// Responsible for dealing with the conflicting key bindings inside of Visual Studio
    /// </summary>
    [Export(typeof(KeyBindingService))]
    public sealed class KeyBindingService
    {
        private readonly IVsUIShell _vsShell;
        private bool _hasChecked;

        [ImportingConstructor]
        public KeyBindingService(SVsServiceProvider sp)
        {
            _vsShell = sp.GetService<SVsUIShell, IVsUIShell>();
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
            CheckForConflictingKeyBindings(dte, buffer);
        }

        private void Hack()
        {
            Action func = () =>
            {
                var window = new UI.ConflictingKeyBindingDialog();
                var removed = window.ConflictingKeyBindingControl.RemovedKeyBindingData;
                removed.Add(new UI.KeyBindingData() { Name = "Key Binding 1", IsChecked = true, Keys = "SomeKeys" });
                removed.Add(new UI.KeyBindingData() { Name = "Key Binding 2", IsChecked = true, Keys = "SomeKeys" });
                removed.Add(new UI.KeyBindingData() { Name = "Key Binding 3", IsChecked = true, Keys = "SomeKeys" });

                var current = window.ConflictingKeyBindingControl.ConflictingKeyBindingData;
                current.Add(new UI.KeyBindingData() { Name = "Key Binding 1", IsChecked = true, Keys = "SomeKeys" });
                current.Add(new UI.KeyBindingData() { Name = "Key Binding 2", IsChecked = false, Keys = "SomeKeys" });
                current.Add(new UI.KeyBindingData() { Name = "Key Binding 3", IsChecked = true, Keys = "SomeKeys" });

                WindowHelper.ShowModal(window);
            };

            // Hack to work around the solution loading dialog problem
            var context = SynchronizationContext.Current;
            ThreadPool.QueueUserWorkItem(x =>
                {
                    System.Threading.Thread.Sleep(TimeSpan.FromSeconds(5));
                    context.Post(unused => func(), null);
                });
        }

        /// <summary>
        /// Check for and remove conflicting key bindings
        /// </summary>
        private void CheckForConflictingKeyBindings(_DTE dte, IVimBuffer buffer)
        {
            Hack();
            var hashSet = new HashSet<KeyInput>(
                buffer.AllModes.Select(x => x.Commands).SelectMany(x => x));
            hashSet.Add(buffer.Settings.GlobalSettings.DisableCommand);
            var commands = dte.Commands.GetCommands();
            var list = FindConflictingCommands(commands, hashSet);
            if (list.Count > 0)
            {
                var msg = new StringBuilder();
                msg.AppendLine("Conflicting key bindings found.  Remove?");
                foreach (var item in list)
                {
                    const int maxLen = 50;
                    var name = item.Name.Length > maxLen ? item.Name.Substring(0, maxLen) + "..." : item.Name;
                    msg.AppendFormat("\t{0}", name);
                    msg.AppendLine();
                }

                using (var modalDisplay = _vsShell.EnableModelessDialog())
                {
                    var res = MessageBox.Show(
                        caption: "VsVim: Remove Conflicting Key Bindings",
                        messageBoxText: msg.ToString(),
                        button: MessageBoxButton.YesNo);
                    if (res == MessageBoxResult.Yes)
                    {
                        list.ForEach(x => x.SafeResetBindings());
                    }
                }
            }
        }

        /// <summary>
        /// Find all of the Command instances which have conflicting key bindings
        /// </summary>
        public static List<Command> FindConflictingCommands(
            IEnumerable<Command> commands,
            HashSet<KeyInput> neededInputs)
        {
            var list = new List<Command>();
            foreach (var cmd in commands.ToList())
            {
                foreach (var binding in cmd.GetKeyBindings())
                {
                    if (ShouldSkip(binding))
                    {
                        continue;
                    }

                    var input = binding.KeyBinding.FirstKeyInput;
                    if (neededInputs.Contains(input))
                    {
                        list.Add(cmd);
                        break;
                    }
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

        /// <summary>
        /// Find all of the key bindings which have been removed
        /// </summary>
        public static List<CommandKeyBinding> FindRemovedKeyBindings()
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
