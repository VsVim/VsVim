using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Xunit;
using Vim.EditorHost;

namespace Vim.UnitTest
{
    public abstract class SnapshotOverlapColumnSpanTest : VimTestBase
    {
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
        }

        private SnapshotOverlapColumnSpan GetSpanFromSpaceAndCount(ITextSnapshotLine line, int start, int count, int tabStop)
        {
            var startColumn = SnapshotOverlapColumn.GetColumnForSpacesOrEnd(line, start, tabStop);
            var endColumn = SnapshotOverlapColumn.GetColumnForSpacesOrEnd(line, start + count, tabStop);
            return new SnapshotOverlapColumnSpan(startColumn, endColumn, tabStop);
        }

        public abstract class GetTextTest : SnapshotOverlapColumnSpanTest
        {
            public sealed class TabTest : GetTextTest
            {
                [WpfFact]
                public void BeforeComplete()
                {
                    Create("\tcat\t");
                    var span = new SnapshotOverlapColumnSpan(
                        new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(0), beforeSpaces: 0, totalSpaces: 4, tabStop: 4),
                        new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(2), beforeSpaces: 0, totalSpaces: 1, tabStop: 4),
                        tabStop: 4);
                    Assert.Equal("\tc", span.GetText());
                }

                [WpfFact]
                public void BeforePartial()
                {
                    Create("\tcat\t");
                    var span = new SnapshotOverlapColumnSpan(
                        new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(0), beforeSpaces: 1, totalSpaces: 4, tabStop: 4),
                        new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(2), beforeSpaces: 0, totalSpaces: 1, tabStop: 4),
                        tabStop: 4);
                    Assert.Equal("   c", span.GetText());
                }

                [WpfFact]
                public void AfterComplete()
                {
                    Create("\tcat\t");
                    var span = new SnapshotOverlapColumnSpan(
                        new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(3), beforeSpaces: 0, totalSpaces: 1, tabStop: 4),
                        new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(5), beforeSpaces: 0, totalSpaces: 1, tabStop: 4),
                        tabStop: 4);
                    Assert.Equal("t\t", span.GetText());
                }

                [WpfFact]
                public void AfterPartial()
                {
                    Create("\tcat\t");
                    var span = new SnapshotOverlapColumnSpan(
                        new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(3), beforeSpaces: 0, totalSpaces: 1, tabStop: 4),
                        new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(4), beforeSpaces: 3, totalSpaces: 4, tabStop: 4),
                        tabStop: 4);
                    Assert.Equal("t   ", span.GetText());
                }

                /// <summary>
                /// Make sure the text calculation is correct when the Start and End are within the 
                /// same point 
                /// </summary>
                [WpfFact]
                public void WithinSingle()
                {
                    Create("\tcat");
                    var span = GetSpanFromSpaceAndCount(_textBuffer.GetLine(0), start: 1, count: 3, tabStop: 12);
                    Assert.Equal("   ", span.GetText());
                }
            }

            public sealed class WideCharacterTest : GetTextTest
            {
                [WpfFact]
                public void Complete()
                {
                    Create("\u3042\u3044\u3046\u3048\u304A");
                    var span = new SnapshotOverlapColumnSpan(_textBuffer.GetSpan(0, 2), tabStop: 12);
                    Assert.Equal("\u3042\u3044", span.GetText());
                }

                [WpfFact]
                public void Partial()
                {
                    Create("\u3042\u3044\u3046\u3048\u304A");
                    var span = GetSpanFromSpaceAndCount(_textBuffer.GetLine(0), start: 1, count: 4, tabStop: 4);
                    Assert.Equal(" \u3044 ", span.GetText());
                }
            }
        }

        public sealed class OverarchingEndTest : SnapshotOverlapColumnSpanTest
        {
            [WpfFact]
            public void Simple()
            {
                Create("cat");
                var span = new SnapshotOverlapColumnSpan(
                    new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(0), beforeSpaces: 0, totalSpaces: 1, tabStop: 4),
                    new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(1), beforeSpaces: 0, totalSpaces: 1, tabStop: 4),
                    tabStop: 4);
                Assert.Equal(_textBuffer.GetColumnFromPosition(1), span.OverarchingEnd);
            }

            /// <summary>
            /// When the SnapshotOverlapColumn for End contains at least one character then the
            /// overarching end is the next SnapshotPoint.  It must be so to encompass the 
            /// partial text
            /// </summary>
            [WpfFact]
            public void EndPartial()
            {
                Create("c\tt");
                var span = new SnapshotOverlapColumnSpan(
                    new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(0), beforeSpaces: 0, totalSpaces: 1, tabStop: 4),
                    new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(1), beforeSpaces: 1, totalSpaces: 4, tabStop: 4),
                    tabStop: 4);
                Assert.Equal(_textBuffer.GetColumnFromPosition(2), span.OverarchingEnd);
            }
        }

        public sealed class OverarchingSpanTest : SnapshotOverlapColumnSpanTest
        {
            /// <summary>
            /// When the Start and End are in the same SnapshotPoint then the overarching span should 
            /// be a single character
            /// </summary>
            [WpfFact]
            public void Single()
            {
                Create("\tcat");
                var span = new SnapshotOverlapColumnSpan(
                    new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(0), beforeSpaces: 0, totalSpaces: 8, tabStop: 4),
                    new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(0), beforeSpaces: 2, totalSpaces: 8, tabStop: 4),
                    tabStop: 4);
                Assert.Equal(1, span.OverarchingSpan.Span.Length);
            }
        }

        public sealed class InnerSpanTest : SnapshotOverlapColumnSpanTest
        {
            /// <summary>
            /// When the Start and End are in the same SnapshotPoint then the InnerSpan should be 
            /// empty 
            /// </summary>
            [WpfFact]
            public void Empty()
            {
                Create("\t");
                var span = new SnapshotOverlapColumnSpan(
                    new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(0), beforeSpaces: 0, totalSpaces: 8, tabStop: 4),
                    new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(0), beforeSpaces: 2, totalSpaces: 8, tabStop: 4),
                    tabStop: 4);
                Assert.Equal(0, span.InnerSpan.Span.Length);
            }
        }

        public abstract class HasOverlapTest : SnapshotOverlapColumnSpanTest
        {
            public sealed class WideCharacterTest : HasOverlapTest
            {
                [WpfFact]
                public void Complete()
                {
                    Create("\u3042\u3044\u3046\u3048\u304A");
                    var span = GetSpanFromSpaceAndCount(_textBuffer.GetLine(0), start: 0, count: 2, tabStop: 4);
                    Assert.False(span.HasOverlap);
                    Assert.Equal(span.OverarchingSpan, span.InnerSpan);
                }

                /// <summary>
                /// This will overlap partially through the い character
                /// </summary>
                [WpfFact]
                public void PartialInEnd()
                {
                    Create("\u3042\u3044\u3046\u3048\u304A");
                    var span = GetSpanFromSpaceAndCount(_textBuffer.GetLine(0), start: 0, count: 3, tabStop: 4);
                    Assert.True(span.HasOverlap);
                }

                /// <summary>
                /// This will overlap partially through the あ character
                /// </summary>
                [WpfFact]
                public void PartialInStart()
                {
                    Create("\u3042\u3044\u3046\u3048\u304A");
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
                [WpfFact]
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
