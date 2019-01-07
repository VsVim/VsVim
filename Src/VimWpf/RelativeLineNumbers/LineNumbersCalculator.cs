using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

using Vim.UI.Wpf.RelativeLineNumbers.Util;

namespace Vim.UI.Wpf.RelativeLineNumbers
{
    public class LineNumbersCalculator
    {
        private readonly IWpfTextView _textView;

        private readonly ILineFormatTracker _formatTracker;

        public LineNumbersCalculator(IWpfTextView textView, ILineFormatTracker formatTracker)
        {
            _textView = textView;
            _formatTracker = formatTracker;
        }

        public ICollection<Line> CalculateLineNumbers()
        {
            int caretIndex = GetCaretIndex();

            var result = GetLinesWithNumbers()
                .Select((line, idx) => MakeLine(line, idx, caretIndex))
                .ToList();

            return result;
        }

        private IEnumerable<ITextViewLine> GetLinesWithNumbers()
        {
            var allLines = _textView.TextViewLines;

            return allLines.Where(x => x.IsFirstTextViewLineForSnapshotLine);
        }

        private int GetCaretIndex()
        {
            var caretLine = _textView.Caret.ContainingTextViewLine;

            var firstVisibleLine =
                _textView.TextViewLines.First(x => x.IsFirstTextViewLineForSnapshotLine);

            if (GetVisualLineNumber(caretLine, out int caretVisualLineNumber) &&
                GetVisualLineNumber(firstVisibleLine, out int referenceVisualLineNumber))
            {
                return caretVisualLineNumber - referenceVisualLineNumber;
            }

            return -1;
        }

        private bool GetVisualLineNumber(ITextViewLine line, out int visualLineNumber)
        {
            var visualSnapshot = _textView.VisualSnapshot;

            var position = _textView.BufferGraph.MapUpToSnapshot(
                line.Start,
                PointTrackingMode.Negative,
                PositionAffinity.Successor,
                visualSnapshot)?.Position;

            visualLineNumber = position.HasValue
                                   ? visualSnapshot.GetLineNumberFromPosition(position.Value)
                                   : 0;

            return position.HasValue;
        }

        private Line MakeLine(ITextViewLine wpfLine, int lineIndex, int caretIndex)
        {
            int numberToDisplay = GetNumberToDisplay(wpfLine, lineIndex, caretIndex);

            double verticalBaseline = wpfLine.TextTop - _textView.ViewportTop + wpfLine.Baseline;

            bool isCaretLine = caretIndex == lineIndex;

            bool caretLineStyle = isCaretLine && _formatTracker.RelativeNumbers;
            return new Line(numberToDisplay, verticalBaseline, caretLineStyle);
        }

        private int GetNumberToDisplay(ITextViewLine wpfLine, int lineIndex, int caretIndex)
        {
            var isCaretLine = lineIndex == caretIndex;

            var absoluteCaretLineNumber =
                _formatTracker.Numbers && isCaretLine;

            var absoluteLineNumbers =
                !_formatTracker.RelativeNumbers;

            if (absoluteCaretLineNumber || absoluteLineNumbers)
            {
                return wpfLine.GetLineNumber();
            }

            return Math.Abs(caretIndex - lineIndex);
        }
    }
}