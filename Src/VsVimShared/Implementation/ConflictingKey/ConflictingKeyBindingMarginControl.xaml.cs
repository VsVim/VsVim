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

namespace Vim.VisualStudio.Implementation.ConflictingKey
{
    /// <summary>
    /// Interaction logic for ConflictingKeyBindingsMargin.xaml
    /// </summary>
    public partial class ConflictingKeyBindingMarginControl : UserControl
    {
        /// <summary>
        /// Raised when the Configure button is clicked
        /// </summary>
        public event EventHandler ConfigureClick;

        public ConflictingKeyBindingMarginControl()
        {
            InitializeComponent();
        }

        private void OnConfigureClick(object sender, RoutedEventArgs e)
        {
            var list = ConfigureClick;
            if (list != null)
            {
                list(this, RoutedEventArgs.Empty);
            }
        }
    }
}
