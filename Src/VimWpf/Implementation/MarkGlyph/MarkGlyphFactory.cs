using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Vim.UI.Wpf.Implementation.MarkGlyph
{
    internal class MarkGlyphFactory : IGlyphFactory
    {
        private readonly IVim _vim;
        private readonly IClassificationFormatMap _classificationFormatMap;

        internal MarkGlyphFactory(IVim vim, IClassificationFormatMap classificationFormatMap)
        {
            _vim = vim;
            _classificationFormatMap = classificationFormatMap;
        }

        public UIElement GenerateGlyph(IWpfTextViewLine line, IGlyphTag tag)
        {
            // Ensure we can draw a glyph for this marker.
            if (tag is MarkGlyphTag markTag)
            {
                var chars = markTag.Chars;
                var textRunProperties = _classificationFormatMap.DefaultTextProperties;
                var foregroundBrush = textRunProperties.ForegroundBrush;
                var typeface = textRunProperties.Typeface;
                var fontSize = textRunProperties.FontRenderingEmSize;
                var tooltip = $"VsVim Marks {chars}";

                // Don't display any more than three mark characters.
                if (chars.Length > 3)
                {
                    chars = chars.Substring(0, 3);
                }

                // If necessary, descrease the font size.
                if (chars.Length > 2)
                {
                    fontSize = fontSize * 2 / chars.Length;
                }

                // Create the UI element for the mark characters.
                var textBlock = new TextBlock
                {
                    Text = chars,
                    Foreground = foregroundBrush,
                    Background = Brushes.Transparent,
                    FontFamily = typeface.FontFamily,
                    FontStretch = typeface.Stretch,
                    FontWeight = typeface.Weight,
                    FontStyle = typeface.Style,
                    FontSize = fontSize,
                    ToolTip = tooltip,
                };

                return textBlock;
            }
            else
            {
                return null;
            }
        }
    }
}
