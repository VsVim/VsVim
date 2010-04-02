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

namespace Vim.UI.Wpf
{
    public partial class OptionsWindow : Window
    {
        private ObservableCollection<IOptionsPage> _pages = new ObservableCollection<IOptionsPage>();

        public ObservableCollection<IOptionsPage> OptionPages
        {
            get { return _pages; }
        }

        public OptionsWindow()
        {
            InitializeComponent();
        }
    }
}
