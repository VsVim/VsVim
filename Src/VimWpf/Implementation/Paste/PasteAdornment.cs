using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf.Implementation.Paste
{
    /// <summary>
    /// This is the actual adornment that displays when Vim enters a paste operation from 
    /// insert mode. 
    /// </summary>
    internal sealed class PasteAdornment
    {
        private readonly ITextView _textView;
        private readonly IProtectedOperations _protectedOperations;
        private readonly IClassificationFormatMap _classificationFormatMap;
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly IAdornmentLayer _adornmentLayer;
        private readonly Object _tag = new object();
        private char _pasteCharacter;
        private bool _isDisplayed;
        private bool _isAdornmentPresent;

        internal char PasteCharacter
        {
            get { return _pasteCharacter; }
            set
            {
                if (_pasteCharacter != value)
                {
                    _pasteCharacter = value;
                    Refresh();
                }
            }
        }

        internal bool IsDisplayed
        {
            get { return _isDisplayed; }
            set
            {
                if (_isDisplayed != value)
                {
                    _isDisplayed = value;
                    Refresh();
                }
            }
        }

        internal PasteAdornment(
            ITextView textView,
            IAdornmentLayer adornmentLayer,
            IProtectedOperations protectedOperations,
            IClassificationFormatMap classificationFormatMap,
            IEditorFormatMap editorFormatMap)
        {
            _textView = textView;
            _adornmentLayer = adornmentLayer;
            _protectedOperations = protectedOperations;
            _classificationFormatMap = classificationFormatMap;
            _editorFormatMap = editorFormatMap;

            _textView.Caret.PositionChanged += OnChangeEvent;
            _textView.LayoutChanged += OnChangeEvent;
        }

        internal void Destroy()
        {
            if (!_textView.IsClosed)
            {
                _textView.Caret.PositionChanged -= OnChangeEvent;
                _textView.LayoutChanged -= OnChangeEvent;
            }
        }

        /// <summary>
        /// Create the TextBlock and border which will display the paste
        /// character with the current font information
        /// </summary>
        private UIElement CreateControl()
        {
            var pasteCharacter = _pasteCharacter.ToString();
            var textViewProperties = _editorFormatMap.GetProperties("TextView Background");
            var backgroundBrush = textViewProperties.GetBackgroundBrush(SystemColors.WindowBrush);
            var properties = _editorFormatMap.GetProperties("Plain Text");
            var foregroundBrush = properties.GetForegroundBrush(SystemColors.WindowBrush);
            var textRunProperties = _classificationFormatMap.DefaultTextProperties;
            var typeface = textRunProperties.Typeface;
            var fontSize = textRunProperties.FontRenderingEmSize;
            var height = _textView.Caret.Height;
            var formattedText = new FormattedText(
                pasteCharacter,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Black);
            var width = formattedText.Width;
            var textHeight = formattedText.Height;

            var textBlock = new TextBlock
            {
                Text = pasteCharacter,
                Foreground = foregroundBrush,
                Background = backgroundBrush,
                FontFamily = typeface.FontFamily,
                FontStretch = typeface.Stretch,
                FontWeight = typeface.Weight,
                FontStyle = typeface.Style,
                FontSize = fontSize,
                Width = width,
                Height = height,
                LineHeight = textHeight != 0 ? textHeight : double.NaN,
                LineStackingStrategy = LineStackingStrategy.MaxHeight,
                BaselineOffset = double.NaN,
            };

            var border = new Border
            {
                Opacity = 100,
                Background = Brushes.White,
                Child = textBlock
            };

            Canvas.SetTop(border, _textView.Caret.Top);
            Canvas.SetLeft(border, _textView.Caret.Left);

            return border;
        }

        private void Refresh()
        {
            if (_isAdornmentPresent)
            {
                _adornmentLayer.RemoveAdornmentsByTag(_tag);
                _isAdornmentPresent = false;
            }

            if (_isDisplayed)
            {
                Display();
            }
        }

        private void Display()
        {
            try
            {
                var control = CreateControl();
                var caretPoint = _textView.Caret.Position.BufferPosition;
                _isAdornmentPresent = true;
                _adornmentLayer.AddAdornment(
                    AdornmentPositioningBehavior.TextRelative,
                    new SnapshotSpan(caretPoint, 0),
                    _tag,
                    control,
                    (x, y) => _isAdornmentPresent = false);
            }
            catch (Exception)
            {
                // In the cases where the caret is off of the screen it is possible that simply 
                // querying the caret can cause an exception to be thrown.  There is no good
                // way to query for whether the caret is visible or not hence we just guard
                // against this case and register the adornment as invisible
                _isAdornmentPresent = false;
            }
        }

        private void OnChangeEvent(object sender, EventArgs e)
        {
            Refresh();
        }
    }
}
