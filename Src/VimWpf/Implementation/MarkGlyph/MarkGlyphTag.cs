using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf.Implementation.MarkGlyph
{
    internal class MarkGlyphTag : IGlyphTag
    {
        private readonly char _char;

        public MarkGlyphTag(char c)
        {
            _char = c;
        }
    }
}
