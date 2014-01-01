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
                Assert.Equal(count, ((SearchOffsetData.Line)data).Item);
            }

            private static void AssertEnd(string offset, int count)
            {
                var data = SearchOffsetData.Parse(offset);
                Assert.True(data.IsEnd);
                Assert.Equal(count, ((SearchOffsetData.End)data).Item);
            }

            private static void AssertStart(string offset, int count)
            {
                var data = SearchOffsetData.Parse(offset);
                Assert.True(data.IsStart);
                Assert.Equal(count, ((SearchOffsetData.Start)data).Item);
            }

            private static void AssertSearch(string offset, string search, Path direction = null)
            {
                direction = direction ?? Path.Forward;
                var data = SearchOffsetData.Parse(offset);
                Assert.True(data.IsSearch);
                Assert.Equal(search, ((SearchOffsetData.Search)data).Item.Pattern);
                Assert.Equal(direction, ((SearchOffsetData.Search)data).Item.Path);
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
                AssertSearch(";/foo", "foo", Path.Forward);
                AssertSearch(";?foo", "foo", Path.Backward);
                AssertSearch(";/hello", "hello", Path.Forward);
            }
        }
    }
}
