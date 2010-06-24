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
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace VsVim.UI
{
    /// <summary>
    /// Interaction logic for ConflictingKeyBindingControl.xaml
    /// </summary>
    public partial class ConflictingKeyBindingControl : UserControl
    {
        private ObservableCollection<KeyBindingData> _keyBindingList = new ObservableCollection<KeyBindingData>();

        public ObservableCollection<KeyBindingData> KeyBindingList
        {
            get { return _keyBindingList; }
        }

        public ConflictingKeyBindingControl()
        {
            InitializeComponent();
            _bindingsListBox.DataContext = _keyBindingList;
            _bindingsListBox.Items.SortDescriptions.Add(new SortDescription(KeyBindingData.KeyNameProperty.Name, ListSortDirection.Ascending));
        }

        private void OnEnableAllVimKeysClick(object sender, RoutedEventArgs e)
        {
            _keyBindingList.ForEach(x => x.HandledByVsVim = true);
        }

        private void OnDisableAllVimKeysClick(object sender, RoutedEventArgs e)
        {
            _keyBindingList.ForEach(x => x.HandledByVsVim = false);
        }
    }
}
