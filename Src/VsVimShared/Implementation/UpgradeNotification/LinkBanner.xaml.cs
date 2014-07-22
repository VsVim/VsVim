using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public partial class LinkBanner : UserControl
    {
        public static readonly DependencyProperty BannerTextProperty = DependencyProperty.Register(
            "BannerText",
            typeof(string),
            typeof(LinkBanner));

        public static readonly DependencyProperty LinkTextProperty = DependencyProperty.Register(
            "LinkText",
            typeof(string),
            typeof(LinkBanner));

        public static readonly DependencyProperty LinkAddressProperty = DependencyProperty.Register(
            "LinkAddress",
            typeof(string),
            typeof(LinkBanner));

        public string BannerText
        {
            get { return (string)GetValue(BannerTextProperty); }
            set { SetValue(BannerTextProperty, value); }
        }

        public string LinkText
        {
            get { return (string)GetValue(LinkTextProperty); }
            set { SetValue(LinkTextProperty, value); }
        }

        public string LinkAddress
        {
            get { return (string)GetValue(LinkAddressProperty); }
            set { SetValue(LinkAddressProperty, value); }
        }

        public LinkBanner()
        {
            InitializeComponent();
        }

        private void OnRequestNavigate(object sender, RoutedEventArgs e)
        {
            var uri = _faqHyperlink.NavigateUri;
            Process.Start(uri.ToString());
            e.Handled = true;
        }
    }
}
