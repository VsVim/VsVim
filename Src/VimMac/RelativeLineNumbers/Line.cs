using System.Diagnostics;

namespace Vim.UI.Wpf.Implementation.RelativeLineNumbers
{
    [DebuggerDisplay("{LineNumber} {DisplayNumber} {Baseline}")]
    internal readonly struct Line
    {
        public int LineNumber { get; }

        public int DisplayNumber { get; }

        public double Baseline { get; }

        public double TextTop { get; }

        public bool IsCaretLine { get; }

        public Line(int lineNumber, int displayNumber, double verticalBaseline, double textTop, bool isCaretLine)
        {
            LineNumber = lineNumber;
            DisplayNumber = displayNumber;
            Baseline = verticalBaseline;
            TextTop = textTop;
            IsCaretLine = isCaretLine;
        }
    }
}