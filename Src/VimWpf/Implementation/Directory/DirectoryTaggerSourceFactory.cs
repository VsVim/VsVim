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
    [Export(typeof(IClassifierProvider))]
    [ContentType(DirectoryContentType.Name)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class DirectoryTaggerSourceFactory : IClassifierProvider
    {
        private static object Key = new object();
        private readonly IClassificationTypeRegistryService _classificationTypeRegistryService;

        [ImportingConstructor]
        internal DirectoryTaggerSourceFactory(IClassificationTypeRegistryService classificationTypeRegistryService)
        {
            _classificationTypeRegistryService = classificationTypeRegistryService;
        }

        IClassifier IClassifierProvider.GetClassifier(ITextBuffer textBuffer)
        {
            var classificationType = _classificationTypeRegistryService.GetClassificationType(DirectoryFormatDefinition.Name);
            return EditorUtilsFactory.CreateClassifier(
                textBuffer.Properties,
                Key,
                () => new DirectoryTaggerSource(textBuffer, classificationType));
        }
    }
}
