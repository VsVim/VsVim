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

        public static void DoShow(CommandKeyBindingSnapshot snapshot)
        {
            var window = new ConflictingKeyBindingDialog();
            var removed = window.ConflictingKeyBindingControl.RemovedKeyBindingData;
            removed.AddRange(snapshot.Removed.Select(x => new KeyBindingData(x)));
            var current = window.ConflictingKeyBindingControl.ConflictingKeyBindingData;
            current.AddRange(snapshot.Conflicting.Select(x => new KeyBindingData(x)));
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
    }
}
