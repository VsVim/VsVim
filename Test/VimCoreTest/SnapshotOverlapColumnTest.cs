using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Xunit;
using Vim.EditorHost;
using Vim.Extensions;
using Microsoft.FSharp.Core;

namespace Vim.UnitTest
{
    public abstract class SnapshotOverlapColumnTest : VimTestBase
    {
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
        }

        private static void AssertColumn(FSharpOption<SnapshotOverlapColumn> actual, SnapshotColumn? expected = null, int? spacesBefore = null, int? spacesAfter = null, int? spacesTotal = null)
        {
            Assert.True(actual.IsSome());
            AssertColumn(actual.Value, expected, spacesBefore, spacesAfter, spacesTotal);
        }

        private static void AssertColumn(SnapshotOverlapColumn actual, SnapshotColumn? expected = null, int? spacesBefore = null, int? spacesAfter = null, int? spacesTotal = null)
        {
            if (expected != null)
            {
                Assert.Equal(expected.Value, actual.Column);
            }

            if (spacesBefore != null)
            {
                Assert.Equal(spacesBefore.Value, actual.SpacesBefore);
            }

            if (spacesAfter != null)
            {
                Assert.Equal(spacesAfter.Value, actual.SpacesAfter);
            }

            if (spacesTotal != null)
            {
                Assert.Equal(spacesTotal.Value, actual.TotalSpaces);
            }
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

        public sealed class GetSpaceWithOverlapTest :  SnapshotOverlapColumnTest
        {
            [WpfFact]
            public void Simple()
            {
                Create("dog", "cat");
                var column = SnapshotOverlapColumn.GetColumnForSpaces(_textBuffer.GetLine(0), spaces: 1, tabStop: 4);
                AssertColumn(column, expected: _textBuffer.GetColumnFromPosition(1), spacesBefore: 0, spacesAfter: 0);
            }

            [WpfFact]
            public void SimpleSurrogatePair()
            {
                const string alien = "\U0001F47D"; // 👽
                Create($"{alien} dog", "cat");
                var column = SnapshotOverlapColumn.GetColumnForSpaces(_textBuffer.GetLine(0), spaces: 1, tabStop: 4);
                AssertColumn(column, expected: _textBuffer.GetColumnFromPosition(2), spacesBefore: 0, spacesAfter: 0);
            }

            /// <summary>
            /// The number of spaces should be the same no matter where into the SnapshotPoint we end up 
            /// indexing.  The only values that should change are SpacesBefore and SpacesAfter
            /// </summary>
            [WpfFact]
            public void PartialTab()
            {
                Create("d\tog", "extra");
                var column = SnapshotOverlapColumn.GetColumnForSpaces(_textBuffer.GetLine(0), spaces: 2, tabStop: 4);
                AssertColumn(column, expected: _textBuffer.GetColumnFromPosition(1), spacesBefore: 1, spacesAfter: 1, spacesTotal: 3);
            }

            [WpfFact]
            public void AtLineBreak()
            {
                Create("dog", "cat");
                var column = SnapshotOverlapColumn.GetColumnForSpaces(_textBuffer.GetLine(0), spaces: 3, tabStop: 4);
            }

            [WpfFact]
            public void InsideLineBreak()
            {
                Create("dog", "cat");
                var column = SnapshotOverlapColumn.GetColumnForSpaces(_textBuffer.GetLine(0), spaces: 4, tabStop: 4);
                Assert.True(column.IsNone());
            }

            [WpfFact]
            public void AtEnd()
            {
                Create("dog", "cat");
                var column = SnapshotOverlapColumn.GetColumnForSpaces(_textBuffer.GetLine(1), spaces: 3, tabStop: 4);
                AssertColumn(column, expected: SnapshotColumn.GetEndColumn(_textBuffer.CurrentSnapshot), spacesBefore: 0, spacesAfter: 0);
            }
        }

        public sealed class GetSpaceWithOverlapOrEndTest :  SnapshotOverlapColumnTest
        {
            [WpfFact]
            public void BeforeTab()
            {
                Create("d\tog", "extra");
                var column = SnapshotOverlapColumn.GetColumnForSpacesOrEnd(_textBuffer.GetLine(0), spaces: 0, tabStop: 4);
                Assert.Equal(_textBuffer.GetColumnFromPosition(position: 0), column.Column);
            }

            [WpfFact]
            public void PartialTab()
            {
                Create("d\tog", "extra");
                var column = SnapshotOverlapColumn.GetColumnForSpacesOrEnd(_textBuffer.GetLine(0), spaces: 4, tabStop: 4);
                Assert.Equal(_textBuffer.GetColumnFromPosition(position: 2), column.Column);
            }

            /// <summary>
            /// The number of spaces should be the same no matter where into the SnapshotPoint we end up 
            /// indexing.  The only values that should change are SpacesBefore and SpacesAfter
            /// </summary>
            [WpfFact]
            public void PartialTab2()
            {
                Create("d\tog", "extra");
                var column = SnapshotOverlapColumn.GetColumnForSpacesOrEnd(_textBuffer.GetLine(0), spaces: 2, tabStop: 4);
                Assert.Equal(1, column.SpacesBefore);
                Assert.Equal(3, column.TotalSpaces);
                Assert.Equal(_textBuffer.GetColumnFromPosition(position: 1), column.Column);
            }

            [WpfFact]
            public void AfterTab()
            {
                Create("d\tog", "extra");
                var column = SnapshotOverlapColumn.GetColumnForSpacesOrEnd(_textBuffer.GetLine(0), spaces: 5, tabStop: 4);
                Assert.Equal(_textBuffer.GetColumnFromPosition(position: 3), column.Column);
                Assert.True(column.Column.IsCharacter('g'));
            }
        }
    }
}
