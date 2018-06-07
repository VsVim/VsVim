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
                var textRunProperties = _classificationFormatMap.DefaultTextProperties;
                var foregroundBrush = textRunProperties.ForegroundBrush;
                var typeface = textRunProperties.Typeface;
                var fontSize = textRunProperties.FontRenderingEmSize;

                var textBlock = new TextBlock
                {
                    Text = markTag.Char.ToString(),
                    Foreground = foregroundBrush,
                    Background = Brushes.Transparent,
                    FontFamily = typeface.FontFamily,
                    FontStretch = typeface.Stretch,
                    FontWeight = typeface.Weight,
                    FontStyle = typeface.Style,
                    FontSize = fontSize,
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
