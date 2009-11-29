using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VimCore;
using System.Windows.Input;
using EnvDTE;
using System.ComponentModel.Composition.Primitives;
using System.ComponentModel.Composition;
using System.Windows;

namespace VsVim
{
    /// <summary>
    /// Responsible for dealing with the conflicting key bindings inside of Visual Studio
    /// </summary>
    [Export(typeof(KeyBindingService))]
    public sealed class KeyBindingService
    {
        private bool m_hasChecked;

        public void OneTimeCheckForConflictingKeyBindings(_DTE dte, IVimBuffer buffer)
        {
            if (dte == null)
            {
                throw new ArgumentNullException("dte");
            }

            if (m_hasChecked)
            {
                return;
            }
            m_hasChecked = true;
            CheckForConflictingKeyBindings(dte, buffer);
        }

        /// <summary>
        /// Check for and remove conflicting key bindings
        /// </summary>
        private void CheckForConflictingKeyBindings(_DTE dte, IVimBuffer buffer)
        {
            var hashSet = new HashSet<KeyInput>(
                buffer.Modes.Select(x => x.Commands).SelectMany(x => x));
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

                var res = MessageBox.Show(
                    caption: "Remove Conflicting Key Bindings",
                    messageBoxText: msg.ToString(),
                    button: MessageBoxButton.YesNo);
                if (res == MessageBoxResult.Yes)
                {
                    list.ForEach(x => { x.Bindings = new object[] { }; });
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

    }
}
