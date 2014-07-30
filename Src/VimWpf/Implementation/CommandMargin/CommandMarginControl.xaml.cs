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

        public static readonly DependencyProperty TextFontFamilyProperty = DependencyProperty.Register(
            "TextFontFamily",
            typeof(FontFamily),
            typeof(CommandMarginControl),
            new PropertyMetadata(new FontFamily("Courier New")));

        public static readonly DependencyProperty TextFontSizeProperty = DependencyProperty.Register(
            "TextFontSize",
            typeof(double),
            typeof(CommandMarginControl),
            new PropertyMetadata(10.0));

        public static readonly DependencyProperty IsStatuslineVisibleProperty = DependencyProperty.Register(
            "IsStatuslineVisible", 
            typeof (Visibility), 
            typeof (CommandMarginControl), 
            new PropertyMetadata(default(Visibility)));


        /// <summary>
        /// The user defined status in extra line
        /// </summary>
        public string StatusLine
        {
            get { return (string) GetValue(StatusLineProperty); }
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

        public double TextFontSize
        {
            get { return (double)GetValue(TextFontSizeProperty); }
            set { SetValue(TextFontSizeProperty, value); }
        }

        public FontFamily TextFontFamily
        {
            get { return (FontFamily)GetValue(TextFontFamilyProperty); }
            set { SetValue(TextFontFamilyProperty, value); }
        }

        public bool IsEditEnabled
        {
            get { return !IsEditReadOnly; }
        }

        public TextBox CommandLineTextBox
        {
            get { return _commandLineInput; }
        }


        public Visibility IsStatuslineVisible
        {
            get { return (Visibility) GetValue(IsStatuslineVisibleProperty); }
            set { SetValue(IsStatuslineVisibleProperty, value); }
        }

        public CommandMarginControl()
        {
            InitializeComponent();
        }

        internal void UpdateCaretPosition(EditPosition editPosition)
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
                switch (editPosition)
                {
                    case EditPosition.Start:
                        // Case where <Home> is pressed.  Put the caret after the : character
                        index = 1;
                        break;
                    case EditPosition.BeforeLastCharacter:
                        // Case where <Left> is pressed.  Put the caret before the last editable value.  Don't let the 
                        // caret get before the : character
                        index = Math.Max(1, text.Length - 1);
                        break;
                    case EditPosition.End:
                        index = text.Length;
                        break;
                    default:
                        Contract.FailEnumValue(editPosition);
                        index = 0;
                        break;
                }
            }

            _commandLineInput.Select(start: index, length: 0);
        }
    }
}
