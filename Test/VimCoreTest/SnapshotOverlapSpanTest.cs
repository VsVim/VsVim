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

        SnapshotOverlapSpan GetSpanFromSpaceAndCount(ITextSnapshotLine line, int start, int count, int tabStop)
        {
            var startPoint = SnapshotLineUtil.GetSpaceWithOverlapOrEnd(line, start, tabStop);
            var endPoint = SnapshotLineUtil.GetSpaceWithOverlapOrEnd(line, start + count, tabStop);
            return new SnapshotOverlapSpan(startPoint, endPoint);
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

                /// <summary>
                /// Make sure the text calculation is correct when the Start and End are within the 
                /// same point 
                /// </summary>
                [Fact]
                public void WithinSingle()
                {
                    Create("\tcat");
                    var span = GetSpanFromSpaceAndCount(_textBuffer.GetLine(0), start: 1, count: 3, tabStop: 12);
                    Assert.Equal("   ", span.GetText());
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
                    var span = GetSpanFromSpaceAndCount(_textBuffer.GetLine(0), start: 1, count: 4, tabStop: 4);
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

        public abstract class HasOverlapTest : SnapshotOverlapSpanTest
        {
            public sealed class WideCharacterTest : HasOverlapTest
            {
                [Fact]
                public void Complete()
                {
                    Create("あいうえお");
                    var span = GetSpanFromSpaceAndCount(_textBuffer.GetLine(0), start: 0, count: 2, tabStop: 4);
                    Assert.False(span.HasOverlap);
                    Assert.Equal(span.OverarchingSpan, span.InnerSpan);
                }

                /// <summary>
                /// This will overlap partially through the い character
                /// </summary>
                [Fact]
                public void PartialInEnd()
                {
                    Create("あいうえお");
                    var span = GetSpanFromSpaceAndCount(_textBuffer.GetLine(0), start: 0, count: 3, tabStop: 4);
                    Assert.True(span.HasOverlap);
                }

                /// <summary>
                /// This will overlap partially through the あ character
                /// </summary>
                [Fact]
                public void PartialInStart()
                {
                    Create("あいうえお");
                    var span = GetSpanFromSpaceAndCount(_textBuffer.GetLine(0), start: 1, count: 3, tabStop: 4);
                    Assert.True(span.HasOverlap);
                }
            }

            public sealed class TabTest : HasOverlapTest
            {
                /// <summary>
                /// When the span is completely within a single character then there is definitely an 
                /// overlap 
                /// </summary>
                [Fact]
                public void WithinSingleCharacter()
                {
                    Create("\tcat");
                    var span = GetSpanFromSpaceAndCount(_textBuffer.GetLine(0), start: 1, count: 3, tabStop: 12);
                    Assert.True(span.HasOverlap);
                    Assert.Equal("   ", span.GetText());
                    Assert.Equal(1, span.Start.SpacesBefore);
                }
            }
        }
    }
}
