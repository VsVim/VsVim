using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Microsoft.VisualStudio.PlatformUI;
using Vim;

namespace VsVim.UI
{
    /// <summary>
    /// Interaction logic for ConflictingKeyBindingDialog.xaml
    /// </summary>
    public partial class ConflictingKeyBindingDialog : DialogWindow
    {
        private readonly ObservableCollection<KeyBindingData> _keyBindingList = new ObservableCollection<KeyBindingData>();
        private readonly CommandKeyBindingSnapshot _snapshot;
        private readonly HashSet<KeyBindingData> _advancedSet = new HashSet<KeyBindingData>();

        public ConflictingKeyBindingDialog(CommandKeyBindingSnapshot snapshot)
        {
            InitializeComponent();

            _snapshot = snapshot;
            ComputeKeyBindings();

            BindingsListBox.ItemsSource = _keyBindingList;
            BindingsListBox.Items.SortDescriptions.Add(new SortDescription("KeyName", ListSortDirection.Ascending));
        }

        private void ComputeKeyBindings()
        {
            // This snapshot contains a list of active keys, and keys which are still conflicting. We will group all
            // bindings by the initial character, and will consider the entire group as being handled by VsVim as long
            // as one is being handled.

            var handledByVsVim = _snapshot.Removed.ToLookup(binding => binding.KeyBinding.FirstKeyStroke);
            var handledByVs = _snapshot.Conflicting.ToLookup(binding => binding.KeyBinding.FirstKeyStroke);

            var allFirstKeys = handledByVsVim.Select(group => group.Key)
                               .Union(handledByVs.Select(group => group.Key));

            foreach (var firstKey in allFirstKeys)
            {
                var data = new KeyBindingData(handledByVsVim[firstKey].Union(handledByVs[firstKey]));
                data.HandledByVsVim = handledByVsVim.Contains(firstKey);
                _keyBindingList.Add(data);

                if (IsAdvanced(firstKey))
                {
                    _advancedSet.Add(data);
                }
            }
        }

        /// <summary>
        /// Switch all of the bindings to VsVim except for the advanced ones
        /// </summary>
        private void OnEnableAllVimKeysClick(object sender, RoutedEventArgs e)
        {
            _keyBindingList
                .Where(x => !_advancedSet.Contains(x))
                .ForEach(x => x.HandledByVsVim = true);
        }

        /// <summary>
        /// Switch all of the bindings to Visual Studio
        /// </summary>
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
            UpdateKeyBindings();
        }

        /// <summary>
        /// Is this an advanced key stroke like CTRL+V which we don't want to automatically 
        /// convert.  
        /// </summary>
        private bool IsAdvanced(KeyStroke keyStroke)
        {
            // Look for paste
            if (keyStroke.KeyModifiers == KeyModifiers.Control &&
                keyStroke.KeyInput.Key == VimKey.LowerV &&
                keyStroke.KeyInput.KeyModifiers == KeyModifiers.None)
            {
                return true;
            }

            // Look for cut
            if (keyStroke.KeyModifiers == KeyModifiers.Control &&
                keyStroke.KeyInput.Key == VimKey.LowerX &&
                keyStroke.KeyInput.KeyModifiers == KeyModifiers.None)
            {
                return true;
            }

            return false;
        }

        private void UpdateKeyBindings()
        {
            var keyBindingsByHandled = _keyBindingList.ToLookup(data => data.HandledByVsVim);

            // For commands being handled by VsVim, we shall remove any other bindings
            foreach (var cur in _keyBindingList.Where(binding => binding.HandledByVsVim).SelectMany(data => data.Bindings))
            {
                var tuple = _snapshot.TryGetCommand(cur.Name);
                if (tuple.Item1)
                {
                    tuple.Item2.SafeResetBindings();
                }
            }

            // Restore all commands we are not handling
            foreach (var cur in _keyBindingList.Where(binding => !binding.HandledByVsVim).SelectMany(data => data.Bindings))
            {
                var tuple = _snapshot.TryGetCommand(cur.Name);
                if (tuple.Item1)
                {
                    tuple.Item2.SafeSetBindings(cur.KeyBinding);
                }
            }

            var settings = Settings.Settings.Default;
            settings.RemovedBindings =
                _keyBindingList.Where(binding => binding.HandledByVsVim).SelectMany(data => data.Bindings)
                .Select(x => new Settings.CommandBindingSetting() { Name = x.Name, CommandString = x.KeyBinding.CommandString })
                .ToArray();
            settings.HaveUpdatedKeyBindings = true;
            settings.Save();
        }
    }
}
