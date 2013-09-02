using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UnitTest.Exports
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("any")]
    [TextViewRole(PredefinedTextViewRoles.Structured)]
    [TagType(typeof(OutliningRegionTag))]
    internal sealed class OutlinerTaggerProvider : ITaggerProvider
    {
        ITagger<T> ITaggerProvider.CreateTagger<T>(ITextBuffer textBuffer)
        {
            var tagger = EditorUtilsFactory.CreateOutlinerTagger(textBuffer);
            return (ITagger<T>)(object)tagger;
        }
    }
}
