using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Vim.UI.Wpf.Implementation.CommandMargin
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

        public Button OptionsButton
        {
            get { return _optionsButton; }
        }

        public CommandMarginControl()
        {
            InitializeComponent();
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
    }
}
