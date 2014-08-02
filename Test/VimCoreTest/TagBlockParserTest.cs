using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

            [Fact]
            public void SimpleMatching()
            {
                var tagBlock = Parse("<a></a>").Single();
                Assert.Equal("a", tagBlock.Text);
                Assert.Equal(0, tagBlock.Children.Count);
            }

            [Fact]
            public void SimpleMatchingWithText()
            {
                var tagBlock = Parse("<a>cat</a>").Single();
                Assert.Equal("a", tagBlock.Text);
                Assert.Equal(0, tagBlock.Children.Count);
            }

            [Fact]
            public void SimpleNested()
            {
                var tagBlock = Parse("<a><b></b></a>").Single();
                Assert.Equal("a", tagBlock.Text);
                Assert.Equal("b", tagBlock.Children.Single().Text);
            }

            [Fact]
            public void SimpleNested2()
            {
                var tagBlock = Parse("<a><b></b><c></c></a>").Single();
                Assert.Equal("a", tagBlock.Text);
                Assert.Equal("b", tagBlock.Children[0].Text);
                Assert.Equal("c", tagBlock.Children[1].Text);
            }

            [Fact]
            public void IgnoreBr()
            {
                var tagBlock = Parse("<a><br></a>").Single();
                Assert.Equal(0, tagBlock.Children.Count);
            }

            [Fact]
            public void IgnoreMeta()
            {
                var tagBlock = Parse("<a><meta></a>").Single();
                Assert.Equal(0, tagBlock.Children.Count);
            }

            [Fact]
            public void MultipleRoot()
            {
                var items = Parse("<a>cat</a><b>dog</b>");
                Assert.Equal("a", items[0].Text);
                Assert.Equal("b", items[1].Text);
            }
        }
    }
}
