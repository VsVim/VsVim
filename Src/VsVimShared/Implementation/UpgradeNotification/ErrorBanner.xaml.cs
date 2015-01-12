using EnvDTE;
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

namespace Vim.VisualStudio.Implementation.UpgradeNotification
{
    public partial class ErrorBanner : UserControl
    {
        public static readonly DependencyProperty BannerTextProperty = DependencyProperty.Register(
            "BannerText",
            typeof(string),
            typeof(ErrorBanner));

        public event EventHandler ViewClick = delegate { };

        public ErrorBanner()
        {
            InitializeComponent();
        }

        private void OnViewClick(object sender, RoutedEventArgs e)
        {
            ViewClick(this, EventArgs.Empty);
        }
    }
}
