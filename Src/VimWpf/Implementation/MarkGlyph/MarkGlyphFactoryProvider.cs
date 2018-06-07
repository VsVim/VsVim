using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation.MarkGlyph
{
    [Export(typeof(IGlyphFactoryProvider))]
    [ContentType(VimConstants.AnyContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [TagType(typeof(MarkGlyphTag))]
    [Name("MarkGlyph")]
    [Order(After = "VsTextMarker")]
    class MarkGlyphFactoryProvider : IGlyphFactoryProvider
    {
        private IVim _vim;

        [ImportingConstructor]
        internal MarkGlyphFactoryProvider(IVim vim)
        {
            _vim = vim;
        }

        public IGlyphFactory GetGlyphFactory(IWpfTextView view, IWpfTextViewMargin margin)
        {
            return new MarkGlyphFactory(_vim);
        }
    }
}
