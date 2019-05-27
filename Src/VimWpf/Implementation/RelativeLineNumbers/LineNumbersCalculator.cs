using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

using Vim.UI.Wpf.Implementation.RelativeLineNumbers.Util;

namespace Vim.UI.Wpf.Implementation.RelativeLineNumbers
{
    public class LineNumbersCalculator
    {
        private readonly IWpfTextView _textView;
        private readonly IVimLocalSettings _localSettings;

        public LineNumbersCalculator(IWpfTextView textView, IVimLocalSettings localSettings)
        {
            _textView = textView
                ?? throw new ArgumentNullException(nameof(textView));
            
            _localSettings = localSettings
                ?? throw new ArgumentNullException(nameof(localSettings));
        }

        public ICollection<Line> CalculateLineNumbers()
        {
            bool hasValidCaret = TryGetCaretIndex(out int caretIndex);

            var result = GetLinesWithNumbers()
                .Select((line, idx) =>
                        {
                            var distanceToCaret = Math.Abs(idx - caretIndex);
                            return MakeLine(line, distanceToCaret, hasValidCaret);
                        })
                .ToList();

            return result;
        }

        private IEnumerable<ITextViewLine> GetLinesWithNumbers()
        {
            var allLines = _textView.TextViewLines;

            return allLines.Where(x => x.IsFirstTextViewLineForSnapshotLine);
        }

        private bool TryGetCaretIndex(out int caretIndex)
        {
            var caretLine = _textView.Caret.ContainingTextViewLine;

            var firstVisibleLine =
                _textView.TextViewLines.First(x => x.IsFirstTextViewLineForSnapshotLine);

            if (TryGetVisualLineNumber(caretLine, out int caretVisualLineNumber) &&
                TryGetVisualLineNumber(firstVisibleLine, out int referenceVisualLineNumber))
            {
                caretIndex = caretVisualLineNumber - referenceVisualLineNumber;
                return true;
            }

            caretIndex = -1;
            return false;
        }

        private bool TryGetVisualLineNumber(ITextViewLine line, out int visualLineNumber)
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

        private Line MakeLine(ITextViewLine wpfLine, int distanceToCaret, bool hasValidCaret)
        {
            int numberToDisplay = GetNumberToDisplay(wpfLine, distanceToCaret, hasValidCaret);

            double verticalBaseline = wpfLine.TextTop - _textView.ViewportTop + wpfLine.Baseline;

            bool isCaretLine = hasValidCaret && distanceToCaret == 0;

            bool caretLineStyle = isCaretLine && _localSettings.RelativeNumber;
            return new Line(numberToDisplay, verticalBaseline, caretLineStyle);
        }

        private int GetNumberToDisplay(ITextViewLine wpfLine, int distanceToCaret, bool hasValidCaret)
        {
            var absoluteCaretLineNumber =
                _localSettings.Number && hasValidCaret && distanceToCaret == 0;

            var absoluteLineNumbers =
                !hasValidCaret || !_localSettings.RelativeNumber;

            if (absoluteCaretLineNumber || absoluteLineNumbers)
            {
                return wpfLine.GetLineNumber();
            }

            return distanceToCaret;
        }
    }
}
