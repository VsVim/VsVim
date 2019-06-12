using System;
using System.Globalization;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.UI.Wpf.Implementation.RelativeLineNumbers.Util;

namespace Vim.UI.Wpf.Implementation.RelativeLineNumbers
{
    internal sealed class LineNumbersTracker
    {
        private readonly IWpfTextView _textView;

        private bool _zoomChanged;
        private bool _heightChanged;
        private bool _bufferChanged;

        public event EventHandler<EventArgs> LineNumbersChanged;

        public int LinesCountWidthChars { get; private set; }

        internal LineNumbersTracker(IWpfTextView textView)
        {
            _textView = textView
                ?? throw new ArgumentNullException(nameof(textView));

            _textView.TextBuffer.Changed += OnTextBufferChanged;
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

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            var beforeCount = e.Before.LineCount;
            var afterCount = e.After.LineCount;
            if (beforeCount != afterCount)
            {
                // The number of lines changed.
                _bufferChanged = true;
            }
            else
            {
                // Detect a change in the phantom line.
                var beforeLine = e.Before.GetLineFromLineNumber(beforeCount - 1);
                var afterLine = e.After.GetLineFromLineNumber(afterCount - 1);
                var beforeEmpty = beforeLine.LengthIncludingLineBreak == 0;
                var afterEmpty = afterLine.LengthIncludingLineBreak == 0;
                if (beforeEmpty != afterEmpty)
                {
                    _bufferChanged = true;
                }
            }
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
            bool newOrReformatted = e.NewOrReformattedLines.Count > 0;
            bool linesMoved = e.TranslatedLines.Count > 0;
            bool scroll = e.VerticalTranslation;

            if (newOrReformatted || linesMoved || scroll || _bufferChanged || _heightChanged || _zoomChanged)
            {
                _bufferChanged = false;
                _zoomChanged = false;
                _heightChanged = false;

                LinesCountWidthChars = GetWidthChars();
                TryInvokeLineNumbersChanged();
            }
        }
    }
}
