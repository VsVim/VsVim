using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Xunit;
using EditorUtils;

namespace Vim.UnitTest
{
    public abstract class SnapshotOverlapSpanTest : VimTestBase
    {
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
        }

        public abstract class GetTextTest : SnapshotOverlapSpanTest
        {
            public sealed class TabTest : GetTextTest
            {
                [Fact]
                public void BeforeComplete()
                {
                    Create("\tcat\t");
                    var span = new SnapshotOverlapSpan(
                        new SnapshotOverlapPoint(_textBuffer.GetPoint(0), before: 0, width: 4),
                        new SnapshotOverlapPoint(_textBuffer.GetPoint(2), before: 0, width: 1));
                    Assert.Equal("\tc", span.GetText());
                }

                [Fact]
                public void BeforePartial()
                {
                    Create("\tcat\t");
                    var span = new SnapshotOverlapSpan(
                        new SnapshotOverlapPoint(_textBuffer.GetPoint(0), before: 1, width: 4),
                        new SnapshotOverlapPoint(_textBuffer.GetPoint(2), before: 0, width: 1));
                    Assert.Equal("   c", span.GetText());
                }

                [Fact]
                public void AfterComplete()
                {
                    Create("\tcat\t");
                    var span = new SnapshotOverlapSpan(
                        new SnapshotOverlapPoint(_textBuffer.GetPoint(3), before: 0, width: 1),
                        new SnapshotOverlapPoint(_textBuffer.GetPoint(5), before: 0, width: 1));
                    Assert.Equal("t\t", span.GetText());
                }

                [Fact]
                public void AfterPartial()
                {
                    Create("\tcat\t");
                    var span = new SnapshotOverlapSpan(
                        new SnapshotOverlapPoint(_textBuffer.GetPoint(3), before: 0, width: 1),
                        new SnapshotOverlapPoint(_textBuffer.GetPoint(4), before: 3, width: 4));
                    Assert.Equal("t   ", span.GetText());
                }
            }

            public sealed class WideCharacterTest : GetTextTest
            {
                [Fact]
                public void Complete()
                {
                    Create("あいうえお");
                    var span = new SnapshotOverlapSpan(_textBuffer.GetSpan(0, 2));
                    Assert.Equal("あい", span.GetText());
                }

                [Fact]
                public void Partial()
                {
                    Create("あいうえお");
                    var span = ColumnWiseUtil.GetSpanFromSpaceAndCount(_textBuffer.GetLine(0), start: 1, count: 4, tabStop: 4);
                    Assert.Equal(" い ", span.GetText());
                }
            }
        }

        public sealed class OverarchingEndTest : SnapshotOverlapSpanTest
        {
            [Fact]
            public void Simple()
            {
                Create("cat");
                var span = new SnapshotOverlapSpan(
                    new SnapshotOverlapPoint(_textBuffer.GetPoint(0), before: 0, width: 1),
                    new SnapshotOverlapPoint(_textBuffer.GetPoint(1), before: 0, width: 1));
                Assert.Equal(_textBuffer.GetPoint(1), span.OverarchingEnd);
            }

            /// <summary>
            /// When the SnapshotOverlapPoint for End contains at least one character then the
            /// overarching end is the next SnapshotPoint.  It must be so to encompass the 
            /// partial text
            /// </summary>
            [Fact]
            public void EndPartial()
            {
                Create("c\tt");
                var span = new SnapshotOverlapSpan(
                    new SnapshotOverlapPoint(_textBuffer.GetPoint(0), before: 0, width: 1),
                    new SnapshotOverlapPoint(_textBuffer.GetPoint(1), before: 1, width: 4));
                Assert.Equal(_textBuffer.GetPoint(2), span.OverarchingEnd);
            }
        }

        public sealed class OverarchingSpanTest : SnapshotOverlapSpanTest
        {
            /// <summary>
            /// When the Start and End are in the same SnapshotPoint then the overarching span should 
            /// be a single character
            /// </summary>
            [Fact]
            public void Single()
            {
                Create("\tcat");
                var span = new SnapshotOverlapSpan(
                    new SnapshotOverlapPoint(_textBuffer.GetPoint(0), before: 0, width: 8),
                    new SnapshotOverlapPoint(_textBuffer.GetPoint(0), before: 2, width: 8));
                Assert.Equal(1, span.OverarchingSpan.Length);
            }
        }

        public sealed class InnerSpanTest : SnapshotOverlapSpanTest
        {
            /// <summary>
            /// When the Start and End are in the same SnapshotPoint then the InnerSpan should be 
            /// empty 
            /// </summary>
            [Fact]
            public void Empty()
            {
                Create("\t");
                var span = new SnapshotOverlapSpan(
                    new SnapshotOverlapPoint(_textBuffer.GetPoint(0), before: 0, width: 8),
                    new SnapshotOverlapPoint(_textBuffer.GetPoint(0), before: 2, width: 8));
                Assert.Equal(0, span.InnerSpan.Length);
            }
        }
    }
}
