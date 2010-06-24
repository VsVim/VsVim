using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.VisualStudio.PlatformUI;
using Vim;

namespace VsVim.UI
{
    /// <summary>
    /// Interaction logic for ConflictingKeyBindingDialog.xaml
    /// </summary>
    public partial class ConflictingKeyBindingDialog : DialogWindow 
    {
        public ConflictingKeyBindingControl ConflictingKeyBindingControl
        {
            get { return _bindingControl; }
        }

        public ConflictingKeyBindingDialog()
        {
            InitializeComponent();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        public static bool DoShow(CommandKeyBindingSnapshot snapshot)
        {
            var window = new ConflictingKeyBindingDialog();
            var keyBindingList = window._bindingControl.KeyBindingList;
            keyBindingList.AddRange(ComputeKeyBindingList(snapshot));
            var ret = window.ShowModal();
            if (ret.HasValue && ret.Value)
            {
                // Remove all of the removed bindings
                var keyBindingsByHandled = keyBindingList.ToLookup(data => data.HandledByVsVim);

                // For commands being handled by VsVim, we shall remove any other bindings
                var removed = keyBindingsByHandled[true].SelectMany(data => data.Bindings);
                foreach (var cur in removed)
                {
                    var tuple = snapshot.TryGetCommand(cur.Name);
                    if (tuple.Item1)
                    {
                        tuple.Item2.SafeResetBindings();
                    }
                }

                // Restore all commands we are not handling
                foreach (var cur in keyBindingsByHandled[false].SelectMany(binding => binding.Bindings))
                {
                    var tuple = snapshot.TryGetCommand(cur.Name);
                    if ( tuple.Item1 )
                    {
                        tuple.Item2.SafeSetBindings(cur.KeyBinding);
                    }
                }

                var settings = Settings.Settings.Default;
                settings.RemovedBindings = 
                    removed
                    .Select(x => new Settings.CommandBindingSetting() { Name = x.Name, CommandString = x.KeyBinding.CommandString })
                    .ToArray();
                settings.HaveUpdatedKeyBindings = true;
                settings.Save();
            }

            return ret.HasValue && ret.Value;
        }

        private static IEnumerable<KeyBindingData> ComputeKeyBindingList(CommandKeyBindingSnapshot snapshot)
        {
            // This snapshot contains a list of active keys, and keys which are still conflicting. We will group all
            // bindings by the initial character, and will consider the entire group as being handled by VsVim as long
            // as one is being handled.

            var handledByVsVim = snapshot.Removed.ToLookup(binding => binding.KeyBinding.FirstKeyInput);
            var handledByVs = snapshot.Conflicting.ToLookup(binding => binding.KeyBinding.FirstKeyInput);

            var allFirstKeys = handledByVsVim.Select(group => group.Key)
                               .Union(handledByVs.Select(group => group.Key));

            return from firstKey in allFirstKeys
                   select new KeyBindingData(handledByVsVim[firstKey].Union(handledByVs[firstKey]))
                   {
                       HandledByVsVim = handledByVsVim.Contains(firstKey)
                   };

        }
    }
}
