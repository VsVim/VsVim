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
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace VsVim.UI
{
    /// <summary>
    /// Interaction logic for ConflictingKeyBindingDialog.xaml
    /// </summary>
    public partial class ConflictingKeyBindingDialog : DialogWindow 
    {
        private ObservableCollection<KeyBindingData> _keyBindingList = new ObservableCollection<KeyBindingData>();

        public ConflictingKeyBindingDialog(CommandKeyBindingSnapshot snapshot)
        {
            InitializeComponent();
            ComputeKeyBindings(snapshot);

            BindingsListBox.ItemsSource = _keyBindingList;
            BindingsListBox.Items.SortDescriptions.Add(new SortDescription("KeyName", ListSortDirection.Ascending));
        }

        private void ComputeKeyBindings(CommandKeyBindingSnapshot snapshot)
        {
            // This snapshot contains a list of active keys, and keys which are still conflicting. We will group all
            // bindings by the initial character, and will consider the entire group as being handled by VsVim as long
            // as one is being handled.

            var handledByVsVim = snapshot.Removed.ToLookup(binding => binding.KeyBinding.FirstKeyInput);
            var handledByVs = snapshot.Conflicting.ToLookup(binding => binding.KeyBinding.FirstKeyInput);

            var allFirstKeys = handledByVsVim.Select(group => group.Key)
                               .Union(handledByVs.Select(group => group.Key));

            foreach (var firstKey in allFirstKeys)
            {
                KeyBindingData data = new KeyBindingData(handledByVsVim[firstKey].Union(handledByVs[firstKey]));
                data.HandledByVsVim = handledByVsVim.Contains(firstKey);
                _keyBindingList.Add(data);
            }
        }

        private void OnEnableAllVimKeysClick(object sender, RoutedEventArgs e)
        {
            _keyBindingList.ForEach(x => x.HandledByVsVim = true);
        }

        private void OnDisableAllVimKeysClick(object sender, RoutedEventArgs e)
        {
            _keyBindingList.ForEach(x => x.HandledByVsVim = false);
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        /* Remove all of the removed bindings
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

            return ret.HasValue && ret.Value; } */

    }
}
