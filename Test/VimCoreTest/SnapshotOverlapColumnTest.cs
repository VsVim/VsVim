using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Xunit;
using Vim.EditorHost;

namespace Vim.UnitTest
{
    public abstract class SnapshotOverlapColumnTest : VimTestBase
    {
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
        }

        public abstract class CtorTest : SnapshotOverlapColumnTest
        {
            public sealed class TabTest : CtorTest
            {
                [WpfFact]
                public void TabStart()
                {
                    Create("\t");
                    var point = new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(0), beforeSpaces: 0, totalSpaces: 4, tabStop: 4);
                    Assert.Equal(0, point.SpacesBefore);
                    Assert.Equal(3, point.SpacesAfter);
                }

                [WpfFact]
                public void TabEnd()
                {
                    Create("\t");
                    var point = new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(0), beforeSpaces: 3, totalSpaces: 4, tabStop: 4);
                    Assert.Equal(3, point.SpacesBefore);
                    Assert.Equal(0, point.SpacesAfter);
                }

                [WpfFact]
                public void TabMiddle()
                {
                    Create("\t");
                    var point = new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(0), beforeSpaces: 1, totalSpaces: 4, tabStop: 4);
                    Assert.Equal(1, point.SpacesBefore);
                    Assert.Equal(2, point.SpacesAfter);
                    Assert.Equal(4, point.TotalSpaces);
                }
            }

            public sealed class NormalTest : CtorTest
            {
                [WpfFact]
                public void Simple()
                {
                    Create("cat");
                    var point = new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(0), beforeSpaces: 0, totalSpaces: 1, tabStop: 4);
                    Assert.Equal(0, point.SpacesBefore);
                    Assert.Equal(0, point.SpacesAfter);
                }

                [WpfFact]
                public void SimpleNonColumnZero()
                {
                    Create("cat");
                    var point = new SnapshotOverlapColumn(_textBuffer.GetColumnFromPosition(1), beforeSpaces: 0, totalSpaces: 1, tabStop: 4);
                    Assert.Equal(0, point.SpacesBefore);
                    Assert.Equal(0, point.SpacesAfter);
                }
            }

            public sealed class EndTest : CtorTest
            {
                [WpfFact]
                public void EndOfBuffer()
                {
                    Create("cat");
                    var point = new SnapshotOverlapColumn(_textBuffer.GetEndColumn(), 0, 0, tabStop: 4);
                    Assert.Equal(0, point.TotalSpaces);
                    Assert.Equal(0, point.SpacesBefore);
                    Assert.Equal(0, point.SpacesAfter);
                }
            }
        }

        public sealed class WithTabStopTest : SnapshotOverlapColumnTest
        {
            [WpfFact]
            public void Simple()
            {
                Create("dog");
                var column = SnapshotOverlapColumn.GetColumnForSpacesOrEnd(_textBuffer.GetLine(0), spaces: 0, tabStop: 4);
                Assert.Equal(4, column.TabStop);
                Assert.Equal(8, column.WithTabStop(8).TabStop);
            }

            [WpfFact]
            public void WithTab()
            {
                Create("\tdog");
                var column = SnapshotOverlapColumn.GetColumnForSpacesOrEnd(_textBuffer.GetLine(0), spaces: 2, tabStop: 4);
                Assert.Equal(4, column.TabStop);
                Assert.Equal(2, column.SpacesBefore);
                column = column.WithTabStop(1);
                Assert.True(column.Column.IsCharacter('o'));
                Assert.Equal(0, column.SpacesBefore);
            }
        }

        public sealed class GetSpaceWithOverlapOrEndTest :  SnapshotOverlapColumnTest
        {
            [WpfFact]
            public void BeforeTab()
            {
                Create("d\tog", "extra");
                var point = SnapshotLineUtil.GetSpaceWithOverlapOrEnd(_textBuffer.GetLine(0), spacesCount: 0, tabStop: 4);
                Assert.Equal(_textBuffer.GetPoint(position: 0), point.Point);
            }

            [WpfFact]
            public void PartialTab()
            {
                Create("d\tog", "extra");
                var point = SnapshotLineUtil.GetSpaceWithOverlapOrEnd(_textBuffer.GetLine(0), spacesCount: 4, tabStop: 4);
                Assert.Equal(_textBuffer.GetPoint(position: 2), point.Point);
            }

            /// <summary>
            /// The number of spaces should be the same no matter where into the SnapshotPoint we end up 
            /// indexing.  The only values that should change are SpacesBefore and SpacesAfter
            /// </summary>
            [WpfFact]
            public void PartialTab2()
            {
                Create("d\tog", "extra");
                var point = SnapshotLineUtil.GetSpaceWithOverlapOrEnd(_textBuffer.GetLine(0), spacesCount: 2, tabStop: 4);
                Assert.Equal(1, point.SpacesBefore);
                Assert.Equal(3, point.Spaces);
                Assert.Equal(_textBuffer.GetPoint(position: 1), point.Point);
            }

            [WpfFact]
            public void AfterTab()
            {
                Create("d\tog", "extra");
                var point = SnapshotLineUtil.GetSpaceWithOverlapOrEnd(_textBuffer.GetLine(0), spacesCount: 5, tabStop: 4);
                Assert.Equal(_textBuffer.GetPoint(position: 3), point.Point);
                Assert.Equal('g', point.Point.GetChar());
            }
        }
    }
}
