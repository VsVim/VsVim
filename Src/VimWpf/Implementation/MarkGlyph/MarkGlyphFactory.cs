using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Vim.UI.Wpf.Implementation.MarkGlyph
{
    internal class MarkGlyphFactory : IGlyphFactory
    {
        private IVim _vim;

        internal MarkGlyphFactory(IVim vim)
        {
            _vim = vim;
        }

        const double m_glyphSize = 16.0;

        public UIElement GenerateGlyph(IWpfTextViewLine line, IGlyphTag tag)
        {
            // Ensure we can draw a glyph for this marker.
            if (tag is MarkGlyphTag markTag)
            {
                var ellipse = new Ellipse();
                ellipse.Fill = Brushes.LightBlue;
                ellipse.StrokeThickness = 2;
                ellipse.Stroke = Brushes.DarkBlue;
                ellipse.Height = m_glyphSize;
                ellipse.Width = m_glyphSize;
                return ellipse;
            }
            else
            {
                return null;
            }
        }
    }
}
