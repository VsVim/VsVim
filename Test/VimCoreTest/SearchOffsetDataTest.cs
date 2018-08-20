using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class SearchOffsetDataTest
    {
        public sealed class ParseTest : SearchOffsetDataTest
        {
            private static void AssertLine(string offset, int count)
            {
                var data = SearchOffsetData.Parse(offset);
                Assert.True(data.IsLine);
                Assert.Equal(count, ((SearchOffsetData.Line)data).Line);
            }

            private static void AssertEnd(string offset, int count)
            {
                var data = SearchOffsetData.Parse(offset);
                Assert.True(data.IsEnd);
                Assert.Equal(count, ((SearchOffsetData.End)data).End);
            }

            private static void AssertStart(string offset, int count)
            {
                var data = SearchOffsetData.Parse(offset);
                Assert.True(data.IsStart);
                Assert.Equal(count, ((SearchOffsetData.Start)data).Start);
            }

            private static void AssertSearch(string offset, string search, SearchPath direction = null)
            {
                direction = direction ?? SearchPath.Forward;
                var data = SearchOffsetData.Parse(offset);
                Assert.True(data.IsSearch);
                Assert.Equal(search, ((SearchOffsetData.Search)data).PatternData.Pattern);
                Assert.Equal(direction, ((SearchOffsetData.Search)data).PatternData.Path);
            }

            [Fact]
            public void Line()
            {
                AssertLine("42", 42);
                AssertLine("-1", -1);
                AssertLine("1", 1);
                AssertLine("+", 1);
                AssertLine("-", -1);
            }

            [Fact]
            public void None()
            {
                var data = SearchOffsetData.Parse("");
                Assert.True(data.IsNone);
            }

            [Fact]
            public void End()
            {
                AssertEnd("e1", 1);
                AssertEnd("e+1", 1);
                AssertEnd("e-1", -1);
                AssertEnd("e42", 42);
                AssertEnd("e+", 1);
            }

            [Fact]
            public void Start()
            {
                AssertStart("s1", 1);
                AssertStart("s+1", 1);
                AssertStart("s-1", -1);
                AssertStart("b1", 1);
                AssertStart("b+1", 1);
                AssertStart("b-1", -1);
            }

            [Fact]
            public void Search()
            {
                AssertSearch(";/foo", "foo", SearchPath.Forward);
                AssertSearch(";?foo", "foo", SearchPath.Backward);
                AssertSearch(";/hello", "hello", SearchPath.Forward);
            }
        }
    }
}
