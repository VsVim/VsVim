using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Vim.UI.Wpf
{
    /// <summary>
    /// Interaction logic for CommandMarginControl.xaml
    /// </summary>
    public partial class CommandMarginControl : UserControl
    {
        public static readonly DependencyProperty StatusLineProperty = DependencyProperty.Register(
            "StatusLine", 
            typeof(string),
            typeof(CommandMarginControl));

        public static readonly DependencyProperty IsRecordingProperty = DependencyProperty.Register(
            "IsRecording", 
            typeof(Visibility),
            typeof(CommandMarginControl));

        /// <summary>
        /// The primary status line for Vim
        /// </summary>
        public string StatusLine
        {
            get { return (string)GetValue(StatusLineProperty); }
            set { SetValue(StatusLineProperty, value); }
        }

        public Visibility IsRecording
        {
            get { return (Visibility) GetValue(IsRecordingProperty); }
            set { SetValue(IsRecordingProperty, value); }
        }

        public event EventHandler OptionsClicked;

        public CommandMarginControl()
        {
            InitializeComponent();
        }

        private void PropertiesCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void PropertiesCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var savedEvent = OptionsClicked;
            if (savedEvent != null)
            {
                savedEvent(this, EventArgs.Empty);
            }
        }
    }
}
