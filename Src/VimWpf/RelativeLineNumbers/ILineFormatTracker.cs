using System;
using System.Windows.Media;

namespace Vim.UI.Wpf.RelativeLineNumbers
{
    public interface ILineFormatTracker
    {
        Brush Background { get; }

        System.Windows.Media.TextFormatting.TextLine MakeTextLine(int number);

        double NumberWidth { get; }

        bool Numbers { get; }

        bool RelativeNumbers { get; }

        bool TryClearReformatRequest();

        event EventHandler<EventArgs> VimNumbersFormatChanged;
    }
}