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
    internal sealed class MarkGlyphTaggerFactory : IViewTaggerProvider
    {
        private readonly object _key = new object();
        private readonly IVim _vim;

        [ImportingConstructor]
        internal MarkGlyphTaggerFactory(IVim vim)
        {
            _vim = vim;
        }

        private MarkGlyphTagger CreateMarkGlyphTagger(IVimBufferData vimBufferData)
        {
            return new MarkGlyphTagger(vimBufferData);
        }

        #region IViewTaggerProvider

        ITagger<T> IViewTaggerProvider.CreateTagger<T>(ITextView textView, ITextBuffer textBuffer)
        {
            if (textView.VisualSnapshot.TextBuffer != textBuffer)
            {
                return null;
            }

            if (!_vim.TryGetOrCreateVimBufferForHost(textView, out IVimBuffer vimBuffer))
            {
                return null;
            }
            var vimBufferData = vimBuffer.VimBufferData;

            var tagger = CreateMarkGlyphTagger(vimBufferData) as ITagger<T>;
            return tagger;
        }

        #endregion
    }
}
