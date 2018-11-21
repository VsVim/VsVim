using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
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
    [Order(Before = "VsTextMarker")]
    internal sealed class MarkGlyphFactoryProvider : IGlyphFactoryProvider
    {
        private readonly IVim _vim;
        private readonly IClassificationFormatMapService _classificationFormatMapService;

        [ImportingConstructor]
        internal MarkGlyphFactoryProvider(IVim vim, IClassificationFormatMapService classificationFormatMapService)
        {
            _vim = vim;
            _classificationFormatMapService = classificationFormatMapService;
        }

        public IGlyphFactory GetGlyphFactory(IWpfTextView textView, IWpfTextViewMargin margin)
        {
            var classificationFormaptMap = _classificationFormatMapService.GetClassificationFormatMap(textView);
            return new MarkGlyphFactory(_vim, classificationFormaptMap);
        }
    }
}
