using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf.Implementation.MarkGlyph
{
    internal sealed class MarkGlyphTag : IGlyphTag
    {
        private readonly string _chars;

        internal string Chars {  get { return _chars; } }

        internal MarkGlyphTag(string chars)
        {
            _chars = chars;
        }
    }
}
