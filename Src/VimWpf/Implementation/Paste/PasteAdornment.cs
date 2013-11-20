using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EditorUtils;
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
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly IAdornmentLayer _adornmentLayer;
        private readonly Object _tag = new object();
        private bool _isDisplayed;
        private bool _isAdornmentPresent;

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
            IEditorFormatMap editorFormatMap)
        {
            _textView = textView;
            _protectedOperations = protectedOperations;
            _editorFormatMap = editorFormatMap;
            _adornmentLayer = adornmentLayer;

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
        /// Create the TextBlock and border which will display the " character with the current font
        /// information
        /// </summary>
        private UIElement CreateControl()
        {
            var textViewProperties = _editorFormatMap.GetProperties("TextView Background");
            var backgroundBrush = textViewProperties.GetBackgroundBrush(SystemColors.WindowBrush);
            var properties = _editorFormatMap.GetProperties("Plain Text");

            var textBlock = new TextBlock();
            textBlock.Text = "\"";
            textBlock.Foreground = properties.GetForegroundBrush(SystemColors.WindowTextBrush);
            textBlock.Background = backgroundBrush;

            var typeface = properties["Typeface"] as Typeface;
            if (typeface != null)
            {
                textBlock.FontFamily = typeface.FontFamily;
                textBlock.FontStretch = typeface.Stretch;
                textBlock.FontWeight = typeface.Weight;
                textBlock.FontStyle = typeface.Style;
            }

            var obj = properties["FontRenderingSize"];
            if (obj is double)
            {
                textBlock.FontSize = (double)obj;
            }

            var border = new Border();
            border.Opacity = 100;
            border.Background = Brushes.White;
            border.Child = textBlock;

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
