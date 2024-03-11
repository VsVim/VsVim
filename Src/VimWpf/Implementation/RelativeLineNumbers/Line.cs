using System.Diagnostics;

namespace Vim.UI.Wpf.Implementation.RelativeLineNumbers
{
    [DebuggerDisplay("{Number} {Baseline}")]
    internal readonly struct Line
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

        public override bool Equals(object obj)
        {
            return obj is Line line &&
                   Number == line.Number &&
                   IsCaretLine == line.IsCaretLine;
        }

        public override int GetHashCode()
        {
            int hashCode = 17;
            hashCode = hashCode * 23 + Number.GetHashCode();
            hashCode = hashCode * 23 + IsCaretLine.GetHashCode();
            return hashCode;
        }
    }
}
