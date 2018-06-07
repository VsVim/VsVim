using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf.Implementation.MarkGlyph
{
    internal sealed class MarkGlyphTag : IGlyphTag
    {
        private readonly char _char;

        internal MarkGlyphTag(char c)
        {
            _char = c;
        }
    }
}
