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

namespace VsVim.Implementation.ConflictingKey
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

        /// <summary>
        /// Raised when the Ignore button is clicked
        /// </summary>
        public event EventHandler IgnoreClick;

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

        private void OnIgnoreClick(object sender, RoutedEventArgs e)
        {
            var list = IgnoreClick;
            if (list != null)
            {
                list(this, RoutedEventArgs.Empty);
            }
        }

    }
}
