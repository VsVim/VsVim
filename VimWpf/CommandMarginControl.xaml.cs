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

namespace Vim.UI.Wpf
{
    /// <summary>
    /// Interaction logic for CommandMarginControl.xaml
    /// </summary>
    public partial class CommandMarginControl : UserControl
    {
        public static readonly DependencyProperty CommandLineProperty = DependencyProperty.Register(
            "CommandLine", 
            typeof(string),
            typeof(CommandMarginControl));

        public string CommandLine
        {
            get { return (string)GetValue(CommandLineProperty); }
            set { SetValue(CommandLineProperty, value); }
        }

        public CommandMarginControl()
        {
            InitializeComponent();
        }
    }
}
