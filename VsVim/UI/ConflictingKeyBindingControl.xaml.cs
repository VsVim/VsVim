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

namespace VsVim.UI
{
    /// <summary>
    /// Interaction logic for ConflictingKeyBindingControl.xaml
    /// </summary>
    public partial class ConflictingKeyBindingControl : UserControl
    {
        private ObservableCollection<KeyBindingData> _removedKeyBindingData = new ObservableCollection<KeyBindingData>();
        private ObservableCollection<KeyBindingData> _conflictingKeyBindingData = new ObservableCollection<KeyBindingData>();

        public ObservableCollection<KeyBindingData> RemovedKeyBindingData
        {
            get { return _removedKeyBindingData; }
        }

        public ObservableCollection<KeyBindingData> ConflictingKeyBindingData
        {
            get { return _conflictingKeyBindingData; }
        }
        
        public ConflictingKeyBindingControl()
        {
            InitializeComponent();
            _removedListBox.DataContext = _removedKeyBindingData;
            _conflictingListBox.DataContext = _conflictingKeyBindingData;
        }
    }
}
