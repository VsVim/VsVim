using System;
using System.Globalization;

using Microsoft.VisualStudio.Text.Editor;

using Vim.UI.Wpf.RelativeLineNumbers.Util;

namespace Vim.UI.Wpf.RelativeLineNumbers
{
    public sealed class LineNumbersTracker
    {
        private readonly IWpfTextView _textView;

        private bool _zoomChanged;
        private bool _heightChanged;

        public event EventHandler<EventArgs> LineNumbersChanged;

        public int LinesCountWidthChars { get; private set; }

        public LineNumbersTracker(IWpfTextView textView)
        {
            _textView = textView
                ?? throw new ArgumentNullException(nameof(textView));

            _textView.LayoutChanged += OnLayoutChanged;
            _textView.ZoomLevelChanged += OnZoomChanged;
            _textView.ViewportHeightChanged += OnViewportHeightChanged;
            _textView.Caret.PositionChanged += OnCaretPositionChanged;

            LinesCountWidthChars = GetWidthChars();
        }

        private int GetWidthChars()
        {
            var lineCount = _textView.GetLineCount();
            var widthChars = lineCount.ToString(CultureInfo.CurrentCulture).Length + 1;

            return widthChars;
        }

        private void TryInvokeLineNumbersChanged()
        {
            if (_textView.IsClosed || _textView.InLayout)
            {
                return;
            }

            LineNumbersChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            var oldLine = e.OldPosition.GetLine();
            var newLine = e.NewPosition.GetLine();

            if (newLine != oldLine)
            {
                TryInvokeLineNumbersChanged();
            }
        }

        private void OnViewportHeightChanged(object sender, EventArgs e)
        {
            _heightChanged = true;
        }

        private void OnZoomChanged(object sender, ZoomLevelChangedEventArgs e)
        {
            _zoomChanged = true;
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            bool linesMoved = e.TranslatedLines.Count > 0;
            bool scroll = e.VerticalTranslation;

            if (linesMoved || scroll || _heightChanged || _zoomChanged)
            {
                _zoomChanged = false;
                _heightChanged = false;

                LinesCountWidthChars = GetWidthChars();
                TryInvokeLineNumbersChanged();
            }
        }
    }
}