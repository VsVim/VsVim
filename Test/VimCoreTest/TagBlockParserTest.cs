using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class TagBlockParserTest : VimTestBase
    {
        public sealed class Basics : TagBlockParserTest
        {
            private List<TagBlock> Parse(params string[] lines)
            {
                var textBuffer = CreateTextBuffer(lines);
                var parser = new TagBlockParser(textBuffer.CurrentSnapshot);
                return parser.ParseTagBlocks();
            }

            [WpfFact]
            public void SimpleMatching()
            {
                var tagBlock = Parse("<a></a>").Single();
                Assert.Equal("a", tagBlock.Text);
                Assert.Equal(0, tagBlock.Children.Count);
            }

            [WpfFact]
            public void SimpleMatchingWithText()
            {
                var tagBlock = Parse("<a>cat</a>").Single();
                Assert.Equal("a", tagBlock.Text);
                Assert.Equal(0, tagBlock.Children.Count);
            }

            [WpfFact]
            public void SimpleNested()
            {
                var tagBlock = Parse("<a><b></b></a>").Single();
                Assert.Equal("a", tagBlock.Text);
                Assert.Equal("b", tagBlock.Children.Single().Text);
            }

            [WpfFact]
            public void SimpleNested2()
            {
                var tagBlock = Parse("<a><b></b><c></c></a>").Single();
                Assert.Equal("a", tagBlock.Text);
                Assert.Equal("b", tagBlock.Children[0].Text);
                Assert.Equal("c", tagBlock.Children[1].Text);
            }

            [WpfFact]
            public void IgnoreBr()
            {
                var tagBlock = Parse("<a><br></a>").Single();
                Assert.Equal(0, tagBlock.Children.Count);
            }

            [WpfFact]
            public void IgnoreMeta()
            {
                var tagBlock = Parse("<a><meta></a>").Single();
                Assert.Equal(0, tagBlock.Children.Count);
            }

            [WpfFact]
            public void MultipleRoot()
            {
                var items = Parse("<a>cat</a><b>dog</b>");
                Assert.Equal("a", items[0].Text);
                Assert.Equal("b", items[1].Text);
            }

            [WpfFact]
            public void Issue1439()
            {
                var tagBlock = Parse("<h2>cat</h2>").Single();
                Assert.Equal("h2", tagBlock.Text);
                Assert.Equal(0, tagBlock.Children.Count);
            }
            [WpfFact]
            public void OtherNameCharacters()
            {
                var tagBlock = Parse("<ns:some.tag_name>cat</ns:some.tag_name>").Single();
                Assert.Equal("ns:some.tag_name", tagBlock.Text);
                Assert.Equal(0, tagBlock.Children.Count);
            }
        }

        public sealed class SpanTest : TagBlockParserTest
        {
            private List<TagBlock> Parse(params string[] lines)
            {
                var textBuffer = CreateTextBuffer(lines);
                var parser = new TagBlockParser(textBuffer.CurrentSnapshot);
                return parser.ParseTagBlocks();
            }

            [WpfFact]
            public void Simple()
            {
                var text = @"<a> cat </a>";
                var item = Parse(text).Single();
                Assert.Equal(new Span(0, text.Length), item.FullSpan);
                Assert.Equal(new Span(3, text.Length - 7), item.InnerSpan);
            }

            [WpfFact]
            public void SimpleEmpty()
            {
                var text = @"<a></a>";
                var item = Parse(text).Single();
                Assert.Equal(new Span(0, text.Length), item.FullSpan);
                Assert.Equal(new Span(3, 0), item.InnerSpan);
            }

            [WpfFact]
            public void Nested()
            {
                var text = @"<a><b></b></a>";
                var item = Parse(text).Single();
                Assert.Equal(new Span(0, text.Length), item.FullSpan);
                Assert.Equal(new Span(3, text.Length - 7), item.InnerSpan);

                item = item.Children.Single();
                Assert.Equal(new Span(3, 7), item.FullSpan);
                Assert.Equal(new Span(6, 0), item.InnerSpan);
            }

            [WpfFact]
            public void DoubleRoot()
            {
                var text = @"<a> </a><b> </b>";
                var collection = Parse(text);

                Assert.Equal(new Span(0, 8), collection[0].FullSpan);
                Assert.Equal(new Span(8, 8), collection[1].FullSpan);
            }
        }

        public sealed class AttributeTest : TagBlockParserTest
        {
            private List<TagBlock> Parse(params string[] lines)
            {
                var textBuffer = CreateTextBuffer(lines);
                var parser = new TagBlockParser(textBuffer.CurrentSnapshot);
                return parser.ParseTagBlocks();
            }

            [WpfFact]
            public void SimpleMatching()
            {
                var tagBlock = Parse("<a name='foo'></a>").Single();
                Assert.Equal("a", tagBlock.Text);
                Assert.Equal(0, tagBlock.Children.Count);
            }

            [WpfFact]
            public void SimpleMatching2()
            {
                var tagBlock = Parse("<a name=\"foo\"></a>").Single();
                Assert.Equal("a", tagBlock.Text);
                Assert.Equal(0, tagBlock.Children.Count);
            }

            [WpfFact]
            public void MultipleAttributes()
            {
                var tagBlock = Parse("<a name1='foo' name2='bar' name-dash='abc' novalue></a>").Single();
                Assert.Equal("a", tagBlock.Text);
                Assert.Equal(0, tagBlock.Children.Count);
            }

            [WpfFact]
            public void OtherAttributeNameCharacters()
            {
                var tagBlock = Parse("<a ns:some.attr_name=\"1\">cat</a>").Single();
                Assert.Equal("a", tagBlock.Text);
                Assert.Equal(0, tagBlock.Children.Count);
            }

            [WpfFact]
            public void BadAttributes()
            {
                Action<string> action = text =>
                {
                    var items = Parse(text);
                    Assert.Equal(0, items.Count);
                };

                action("<a name1='f hello // bar");
                action("<a name1=\"f hello // bar");
                action("<a name1=> <gain");
            }

            /// <summary>
            /// Issue 1644
            /// </summary>
            [WpfFact]
            public void NamesWithDashes()
            {
                var tagBlock = Parse(@"<a name-dash=""1"">cat</a>").Single();
                Assert.Equal("a", tagBlock.Text);
                Assert.Equal(0, tagBlock.Children.Count);
            }

            [WpfFact]
            public void NoValue()
            {
                var tagBlock = Parse(@"<button disabled>search</button>").Single();
                Assert.Equal("button", tagBlock.Text);
                Assert.Equal(0, tagBlock.Children.Count);
            }

        }
    }
}
