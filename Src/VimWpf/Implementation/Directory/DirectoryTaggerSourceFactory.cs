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
using Microsoft.VisualStudio.Text.Classification;

namespace Vim.UI.Wpf.Implementation.Directory
{
    [Export(typeof(ITaggerProvider))]
    [ContentType(VimWpfConstants.DirectoryContentType)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    [TagType(typeof(TextMarkerTag))]
    internal sealed class DirectoryTaggerSourceFactory : ITaggerProvider
    {
        private static object Key = new object();
        private readonly IClassificationTypeRegistryService _classificationTypeRegistryService;

        [ImportingConstructor]
        internal DirectoryTaggerSourceFactory(IClassificationTypeRegistryService classificationTypeRegistryService)
        {
            _classificationTypeRegistryService = classificationTypeRegistryService;
        }

        ITagger<T> ITaggerProvider.CreateTagger<T>(ITextBuffer textBuffer)
        {
            var classificationType = _classificationTypeRegistryService.GetClassificationType(DirectoryFormatDefinition.Name);
            return EditorUtilsFactory.CreateBasicTagger(
                textBuffer.Properties,
                Key,
                () => new DirectoryTagger(textBuffer, classificationType)) as ITagger<T>;
        }
    }
}
