using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text;

namespace EditorUtils.UnitTest
{
    internal static class TestUtils
    {
        internal static ITagSpan<TextMarkerTag> CreateTagSpan(SnapshotSpan span)
        {
            return new TagSpan<TextMarkerTag>(span, new TextMarkerTag("my tag"));
        }

        internal static IEnumerable<Span> GetDogSpans(string text)
        {
            var index = text.IndexOf("dog");
            while (index >= 0)
            {
                yield return new Span(index, 3);
                index = text.IndexOf("dog", index + 1);
            } 
        }

        internal static ReadOnlyCollection<ITagSpan<TextMarkerTag>> GetDogTags(SnapshotSpan span)
        {
            var text = span.GetText();
            var list = new List<ITagSpan<TextMarkerTag>>();
            foreach (var current in GetDogSpans(text))
            {
                var tagSpan = new SnapshotSpan(span.Start.Add(current.Start), current.Length);
                list.Add(CreateTagSpan(tagSpan));
            }

            return list.ToReadOnlyCollectionShallow();
        }

    }
}
