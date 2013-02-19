using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Vim.UI.Wpf.Implementation.CommandMargin
{
    /// <summary>
    /// CommandMargin event data 
    /// </summary>
    public sealed class CommandMarginEventArgs : EventArgs
    {
        public string Command { get; set; }
    }

    /// <summary>
    /// Interaction logic for CommandMarginControl.xaml
    /// </summary>
    public partial class CommandMarginControl : UserControl
    {
        private bool _isEditDisable = true;

        public static readonly DependencyProperty StatusLineProperty = DependencyProperty.Register(
            "StatusLine",
            typeof(string),
            typeof(CommandMarginControl));

        public static readonly DependencyProperty IsRecordingProperty = DependencyProperty.Register(
            "IsRecording",
            typeof(Visibility),
            typeof(CommandMarginControl));

        public static readonly DependencyProperty IsCommandEditionDisableProperty = DependencyProperty.Register(
            "IsCommandEditionDisable",
            typeof(bool),
            typeof(CommandMarginControl),
            new PropertyMetadata(true));

        public static readonly DependencyProperty TextForegroundProperty = DependencyProperty.Register(
            "TextForeground",
            typeof(Brush),
            typeof(CommandMarginControl),
            new PropertyMetadata(Brushes.Black));

        public static readonly DependencyProperty TextBackgroundProperty = DependencyProperty.Register(
            "TextBackground",
            typeof(Brush),
            typeof(CommandMarginControl),
            new PropertyMetadata(Brushes.White));

        /// <summary>
        /// The primary status line for Vim
        /// </summary>
        public string StatusLine
        {
            get { return (string)GetValue(StatusLineProperty); }
            set { SetValue(StatusLineProperty, value); }
        }

        public bool IsCommandEditionDisable
        {
            get { return (bool)GetValue(IsCommandEditionDisableProperty); }
            set { _isEditDisable = value; SetValue(IsCommandEditionDisableProperty, value); }
        }

        public Visibility IsRecording
        {
            get { return (Visibility)GetValue(IsRecordingProperty); }
            set { SetValue(IsRecordingProperty, value); }
        }

        public Brush TextForeground
        {
            get { return (Brush)GetValue(TextForegroundProperty); }
            set { SetValue(TextForegroundProperty, value); }
        }

        public Brush TextBackground
        {
            get { return (Brush)GetValue(TextBackgroundProperty); }
            set { SetValue(TextBackgroundProperty, value); }
        }

        public event EventHandler OptionsClicked;
        public event EventHandler CancelCommandEdition;
        public event EventHandler RunCommandEdition;
        public event EventHandler HistoryGoPrevious;
        public event EventHandler HistoryGoNext;

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

        /// <summary>
        /// Give the command line edit control focus
        /// </summary>
        public void FocusCommandLine(bool moveCaretToEnd)
        {
            commandLineInput.Focus();

            UpdateCaretPosition(moveCaretToEnd);
        }

        public void UpdateCaretPosition(bool moveToEnd)
        {
            if (commandLineInput.IsFocused) // We also use this when navigation through history
            {
                var l = commandLineInput.Text.Length;
                commandLineInput.Select(
                    moveToEnd ?
                        l :		            // Move caret to the last character 
                        Math.Min(l, 1),     // Move caret after the command prefix
                    0);
            }
        }

        private void DoCancelCommandEdition()
        {
            var savedEvent = CancelCommandEdition;
            if (savedEvent != null)
            {
                savedEvent(this, EventArgs.Empty);
            }
        }

        private void DoHistoryGoPrevious()
        {
            var savedEvent = HistoryGoPrevious;
            if (savedEvent != null)
            {
                savedEvent(this, EventArgs.Empty);
            }
        }

        private void DoHistoryGoNext()
        {
            var savedEvent = HistoryGoNext;
            if (savedEvent != null)
            {
                savedEvent(this, EventArgs.Empty);
            }
        }

        private void DoRunCommandEdition(string command)
        {
            var savedEvent = RunCommandEdition;
            if (savedEvent != null)
            {
                savedEvent(this, new CommandMarginEventArgs() { Command = commandLineInput.Text });
            }
        }

        private void commandLineInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    DoCancelCommandEdition();
                    break;

                case Key.Return:
                    DoRunCommandEdition(commandLineInput.Text.Trim());
                    break;

                case Key.Up:
                    DoHistoryGoPrevious();
                    break;

                case Key.Down:
                    DoHistoryGoNext();
                    break;

                case Key.Back:
                    if (commandLineInput.Text.Trim().Length > 1)
                    {
                        // Prevent erasing the command prefix, unless it is the only character
                        if (1 == commandLineInput.CaretIndex)
                        {
                            e.Handled = true;
                        }
                    }
                    break;
            }
        }

        private void commandLineInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isEditDisable && 0 == commandLineInput.Text.Trim().Length)
            {
                DoCancelCommandEdition();
            }
        }

        private void commandLineInput_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (commandLineInput.Text.Length > 0)
            {
                // Prevent modifications to the command prefix
                var sl = commandLineInput.SelectionLength;

                if (sl > 0)
                {
                    if (0 == commandLineInput.SelectionStart)
                    {
                        commandLineInput.CaretIndex = 1;
                        commandLineInput.SelectionStart = 1;
                        commandLineInput.SelectionLength = sl - 1;
                    }
                    else
                    {
                        if (0 == commandLineInput.CaretIndex)
                        {
                            commandLineInput.CaretIndex = 1;
                        }
                    }
                }
                else
                {
                    if (0 == commandLineInput.CaretIndex)
                    {
                        commandLineInput.CaretIndex = 1;
                    }
                }
            }
        }
    }
}
