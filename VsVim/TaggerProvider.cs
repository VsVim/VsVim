using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Microsoft.VisualStudio.Text;

namespace VsVim
{
    [Export(typeof(ITaggerProvider))]
    [ContentType(Constants.ContentType)]
    [TagType(typeof(TextMarkerTag))]
    public sealed class TaggerProvider : ITaggerProvider
    {
        [Import]
        private IVimFactoryService _vimFactory = null;

        public ITagger<T> CreateTagger<T>(ITextBuffer textBuffer) where T : ITag
        {
            VsVimBuffer buffer;
            if ( textBuffer.TryGetVimBuffer(out buffer))
            {
                return _vimFactory.CreateTagger(buffer.VimBuffer) as ITagger<T>;
            }

            return null;
        }
    }
}
