using Microsoft.VisualStudio.Text.Editor;
using System.Windows.Media;

// Disambiguate WPF TextLine with Vim.TextLine
using WpfTextLine = System.Windows.Media.TextFormatting.TextLine;

namespace Vim.UI.Wpf.Implementation.RelativeLineNumbers
{
    internal interface ILineFormatTracker
    {
        Brush Background { get; }

        WpfTextLine MakeTextLine(int number, bool isCurrentLineNumber);

        double NumberWidth { get; }

        bool TryClearReformatRequest();
    }
}
