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

namespace VsVim.Implementation.UpgradeNotification
{
    public partial class LinkBanner : UserControl, IWpfTextViewMargin
    {
        public const string DefaultMarginName = "Link Banner Margin";

        public static readonly DependencyProperty MarginNameProperty = DependencyProperty.Register(
            "MarginName",
            typeof(string),
            typeof(LinkBanner),
            new PropertyMetadata(DefaultMarginName));

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

        public string MarginName
        {
            get { return (string)GetValue(MarginNameProperty); }
            set { SetValue(MarginNameProperty, value); }
        }

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

        public event EventHandler CloseClicked;

        public LinkBanner()
        {
            InitializeComponent();
        }

        private void OnCloseClick(object sender, EventArgs e)
        {
            Visibility = Visibility.Collapsed;

            var handler = CloseClicked;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void OnRequestNavigate(object sender, RoutedEventArgs e)
        {
            var uri = _faqHyperlink.NavigateUri;
            Process.Start(uri.ToString());
            e.Handled = true;
        }

        #region IWpfTextViewMargin

        FrameworkElement IWpfTextViewMargin.VisualElement
        {
            get { return this; }
        }

        bool ITextViewMargin.Enabled
        {
            get { return Visibility.Visible == Visibility; }
        }

        ITextViewMargin ITextViewMargin.GetTextViewMargin(string marginName)
        {
            return marginName == MarginName ? this : null;
        }

        double ITextViewMargin.MarginSize
        {
            get { return 25; }
        }

        void IDisposable.Dispose()
        {
            
        }

        #endregion
    }
}
