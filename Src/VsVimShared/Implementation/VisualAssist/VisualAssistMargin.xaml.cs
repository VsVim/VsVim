using System;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;

namespace Vim.VisualStudio.Implementation.VisualAssist
{
    internal partial class VisualAssistMargin : UserControl
    {
        internal VisualAssistMargin()
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
