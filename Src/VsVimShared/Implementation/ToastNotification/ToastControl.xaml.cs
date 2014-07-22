using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace Vim.VisualStudio.Implementation.ToastNotification
{
    public partial class ToastControl : UserControl
    {
        private readonly ObservableCollection<FrameworkElement> _toastNotificationCollection = new ObservableCollection<FrameworkElement>();

        public ObservableCollection<FrameworkElement> ToastNotificationCollection
        {
            get { return _toastNotificationCollection; }
        }

        public ToastControl()
        {
            InitializeComponent();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            var button = e.Source as Button;
            if (button == null)
            {
                return;
            }

            var toastNotification = button.Tag as FrameworkElement;
            if (toastNotification == null)
            {
                return;
            }

            _toastNotificationCollection.Remove(toastNotification);
        }
    }
}
