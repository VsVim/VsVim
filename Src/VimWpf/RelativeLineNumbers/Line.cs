using System.Diagnostics;

namespace Vim.UI.Wpf.RelativeLineNumbers
{
    [DebuggerDisplay("{Number} {Baseline}")]
    public sealed class Line
    {
        public int Number { get; }

        public double Baseline { get; }

        public bool IsCaretLine { get; }

        public Line(int number, double verticalBaseline, bool isCaretLine)
        {
            Number = number;
            Baseline = verticalBaseline;
            IsCaretLine = isCaretLine;
        }
    }
}