using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Vim.Extensions;

namespace Vim.UI.Wpf.Implementation.MarkGlyph
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(VimConstants.AnyContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [TagType(typeof(MarkGlyphTag))]
    internal sealed class MarkGlyphTaggerSourceFactory : IViewTaggerProvider
    {
        private readonly object _key = new object();
        private readonly IVim _vim;
        private readonly IMarkDisplayUtil _markDisplayUtil;

        [ImportingConstructor]
        internal MarkGlyphTaggerSourceFactory(IVim vim, IMarkDisplayUtil markDisplayUtil)
        {
            _vim = vim;
            _markDisplayUtil = markDisplayUtil;
        }

        private MarkGlyphTaggerSource CreateMarkGlyphTaggerSource(IVimBufferData vimBufferData)
        {
            return new MarkGlyphTaggerSource(vimBufferData, _markDisplayUtil);
        }

        #region IViewTaggerProvider

        ITagger<T> IViewTaggerProvider.CreateTagger<T>(ITextView textView, ITextBuffer textBuffer)
        {
            if (textView.TextBuffer != textBuffer)
            {
                return null;
            }

            if (!_vim.TryGetOrCreateVimBufferForHost(textView, out IVimBuffer vimBuffer))
            {
                return null;
            }
            var vimBufferData = vimBuffer.VimBufferData;

            Func<IBasicTaggerSource<MarkGlyphTag>> func =
                () => CreateMarkGlyphTaggerSource(vimBufferData);
            return TaggerUtil.CreateBasicTagger(
                textView.Properties,
                _key,
                func.ToFSharpFunc()) as ITagger<T>;
        }

        #endregion
    }
}
