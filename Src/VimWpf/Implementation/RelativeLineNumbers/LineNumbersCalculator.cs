﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Vim.UI.Wpf.Implementation.RelativeLineNumbers.Util;

namespace Vim.UI.Wpf.Implementation.RelativeLineNumbers
{
    internal sealed class LineNumbersCalculator
    {
        private static readonly ICollection<ITextViewLine> s_empty = new ITextViewLine[0];

        private readonly IWpfTextView _textView;
        private readonly IVimLocalSettings _localSettings;

        internal LineNumbersCalculator(IWpfTextView textView, IVimLocalSettings localSettings)
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

        private ICollection<ITextViewLine> TextViewLines
        {
            get
            {
                if (!_textView.IsClosed && !_textView.InLayout)
                {
                    var textViewLines = _textView.TextViewLines;
                    if (textViewLines != null && textViewLines.IsValid)
                    {
                        return textViewLines;
                    }
                }
                return s_empty;
            }
        }

        private IEnumerable<ITextViewLine> GetLinesWithNumbers()
        {
            return TextViewLines.Where(x => x.IsValid && x.IsFirstTextViewLineForSnapshotLine);
        }

        private bool TryGetCaretIndex(out int caretIndex)
        {
            var caretLine = _textView.Caret.Position.BufferPosition.GetContainingLine();

            var firstVisibleLine =
                TextViewLines.FirstOrDefault(x => x.IsValid && x.IsFirstTextViewLineForSnapshotLine);

            if (firstVisibleLine != null &&
                TryGetVisualLineNumber(caretLine.Start, out int caretVisualLineNumber) &&
                TryGetVisualLineNumber(firstVisibleLine.Start, out int referenceVisualLineNumber))
            {
                caretIndex = caretVisualLineNumber - referenceVisualLineNumber;
                return true;
            }

            caretIndex = -1;
            return false;
        }

        private bool TryGetVisualLineNumber(SnapshotPoint point, out int visualLineNumber)
        {
            var visualSnapshot = _textView.VisualSnapshot;

            var position = _textView.BufferGraph.MapUpToSnapshot(
                point,
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

            bool caretLineStyle = isCaretLine;
            return new Line(numberToDisplay, verticalBaseline, caretLineStyle);
        }

        private int GetNumberToDisplay(ITextViewLine wpfLine, int distanceToCaret, bool hasValidCaret)
        {
            // Detect the phantom line.
            if (wpfLine.Start.Position == wpfLine.End.Position &&
                wpfLine.Start.Position == wpfLine.Snapshot.Length)
            {
                return -1;
            }

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
