using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfKeyboard = System.Windows.Input.Keyboard;

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
        public static readonly DependencyProperty StatusLineProperty = DependencyProperty.Register(
            "StatusLine",
            typeof(string),
            typeof(CommandMarginControl));

        public static readonly DependencyProperty IsRecordingProperty = DependencyProperty.Register(
            "IsRecording",
            typeof(Visibility),
            typeof(CommandMarginControl));

        public static readonly DependencyProperty IsEditReadOnlyProperty = DependencyProperty.Register(
            "IsEditReadOnly",
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

        public bool IsEditReadOnly
        {
            get { return (bool)GetValue(IsEditReadOnlyProperty); }
            set { SetValue(IsEditReadOnlyProperty, value); }
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

        public bool IsEditEnabled
        {
            get { return !IsEditReadOnly; }
        }

        public TextBox CommandLineTextBox
        {
            get { return _commandLineInput; }
        }

        public event EventHandler OptionsClicked;
        public event EventHandler CommandCancelled;
        public event EventHandler<CommandMarginEventArgs> CommandCompleted;
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
        public void BeginCommandLineEdit(bool moveCaretToEnd)
        {
            WpfKeyboard.Focus(_commandLineInput);
            IsEditReadOnly = false;
            UpdateCaretPosition(moveCaretToEnd);
        }

        public void UpdateCaretPosition(bool moveCaretToEnd)
        {
            if (IsEditReadOnly)
            {
                return;
            }

            var text = _commandLineInput.Text;
            int index;
            if (String.IsNullOrEmpty(text))
            {
                // Handle the odd case of no text.  This shouldn't be possible because this control shouldn't be 
                // engaged without a : or / on the command line.  Handle it anyways to be safe
                // through vim but 
                index = 0;
            }
            else
            { 
                if (moveCaretToEnd)
                {
                    // Case where <Left> is pressed.  Put the caret before the last editable value.  Don't let the 
                    // caret get before the : character
                    index = Math.Max(1, text.Length - 1);
                }
                else
                {
                    // Case where <Home> is pressed.  Put the caret after the : character
                    index = 1;
                }
            }

            _commandLineInput.Select(start: index, length: 0);
        }

        private void OnHistoryGoPrevious()
        {
            var savedEvent = HistoryGoPrevious;
            if (savedEvent != null)
            {
                savedEvent(this, EventArgs.Empty);
            }
        }

        private void OnHistoryGoNext()
        {
            var savedEvent = HistoryGoNext;
            if (savedEvent != null)
            {
                savedEvent(this, EventArgs.Empty);
            }
        }

        private void OnCancelCommand()
        {
            var savedEvent = CommandCancelled;
            if (savedEvent != null)
            {
                savedEvent(this, EventArgs.Empty);
            }
        }

        private void OnCommandCompleted(string command)
        {
            var savedEvent = CommandCompleted;
            if (savedEvent != null)
            {
                var e = new CommandMarginEventArgs() { Command = command };
                savedEvent(this, e);
            }
        }

        private void OnBack(KeyEventArgs e)
        {
            if (_commandLineInput.Text.Trim().Length > 1)
            {
                // Prevent erasing the command prefix, unless it is the only character
                if (1 == _commandLineInput.CaretIndex)
                {
                    e.Handled = true;
                }
            }
        }

        private void OnCommandLineInputPreviewKeyDown(object sender, KeyEventArgs e)
        {
            HandleKeyEvent(e);
        }

        private void OnCommandLineInputTextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsEditEnabled && 0 == _commandLineInput.Text.Trim().Length)
            {
                OnCancelCommand();
            }
        }

        private void OnCommandLineInputSelectionChanged(object sender, RoutedEventArgs e)
        {
            if (_commandLineInput.Text.Length > 0)
            {
                // Prevent modifications to the command prefix
                var sl = _commandLineInput.SelectionLength;

                if (sl > 0)
                {
                    if (0 == _commandLineInput.SelectionStart)
                    {
                        _commandLineInput.CaretIndex = 1;
                        _commandLineInput.SelectionStart = 1;
                        _commandLineInput.SelectionLength = sl - 1;
                    }
                    else
                    {
                        if (0 == _commandLineInput.CaretIndex)
                        {
                            _commandLineInput.CaretIndex = 1;
                        }
                    }
                }
                else
                {
                    if (0 == _commandLineInput.CaretIndex)
                    {
                        _commandLineInput.CaretIndex = 1;
                    }
                }
            }
        }

        internal void HandleKeyEvent(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    OnCancelCommand();
                    break;
                case Key.Return:
                    OnCommandCompleted(_commandLineInput.Text.Trim());
                    break;
                case Key.Up:
                    OnHistoryGoPrevious();
                    break;
                case Key.Down:
                    OnHistoryGoNext();
                    break;
                case Key.Back:
                    OnBack(e);
                    break;
            }
        }
    }
}
