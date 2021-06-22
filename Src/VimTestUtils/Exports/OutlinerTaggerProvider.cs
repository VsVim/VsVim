using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UnitTest.Exports
{
    // TODO_SHARED move to editor host and make this internal again
    [Export(typeof(ITaggerProvider))]
    [ContentType("any")]
    [TextViewRole(PredefinedTextViewRoles.Structured)]
    [TagType(typeof(OutliningRegionTag))]
    public sealed class OutlinerTaggerProvider : ITaggerProvider
    {
        ITagger<T> ITaggerProvider.CreateTagger<T>(ITextBuffer textBuffer)
        {
            var tagger = TaggerUtil.CreateOutlinerTagger(textBuffer);
            return (ITagger<T>)(object)tagger;
        }
    }
}
