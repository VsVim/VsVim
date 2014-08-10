using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Editor;
using EditorUtils;
using System.ComponentModel.Composition;

namespace Vim.UI.Wpf.Implementation.Directory
{
    [Export(typeof(ITaggerProvider))]
    [ContentType(VimWpfConstants.DirectoryContentType)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    [TagType(typeof(TextMarkerTag))]
    internal sealed class DirectoryTaggerSourceFactory : ITaggerProvider
    {
        private static object Key = new object();

        ITagger<T> ITaggerProvider.CreateTagger<T>(ITextBuffer textBuffer)
        {
            return EditorUtilsFactory.CreateBasicTagger(
                textBuffer.Properties,
                Key,
                () => new DirectoryTagger(textBuffer)) as ITagger<T>;
        }
    }
}
