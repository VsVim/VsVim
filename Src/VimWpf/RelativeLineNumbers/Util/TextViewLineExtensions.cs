using Microsoft.VisualStudio.Text.Formatting;

namespace Vim.UI.Wpf.RelativeLineNumbers.Util
{
    internal static class TextViewLineExtensions
    {
        public static int GetLineNumber(this ITextViewLine line)
        {
            return line.Snapshot.GetLineNumberFromPosition(line.Start.Position) + 1;
        }
    }
}