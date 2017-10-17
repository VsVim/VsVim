using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Xunit;
using Vim.EditorHost;

namespace Vim.UnitTest
{
    public abstract class SnapshotOverlapPointTest : VimTestBase
    {
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
        }

        public abstract class CtorTest : SnapshotOverlapPointTest
        {
            public sealed class TabTest : CtorTest
            {
                [Fact]
                public void TabStart()
                {
                    Create("\t");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(0), beforeSpaces: 0, totalSpaces: 4);
                    Assert.Equal(0, point.SpacesBefore);
                    Assert.Equal(3, point.SpacesAfter);
                }

                [Fact]
                public void TabEnd()
                {
                    Create("\t");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(0), beforeSpaces: 3, totalSpaces: 4);
                    Assert.Equal(3, point.SpacesBefore);
                    Assert.Equal(0, point.SpacesAfter);
                }

                [Fact]
                public void TabMiddle()
                {
                    Create("\t");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(0), beforeSpaces: 1, totalSpaces: 4);
                    Assert.Equal(1, point.SpacesBefore);
                    Assert.Equal(2, point.SpacesAfter);
                    Assert.Equal(4, point.Spaces);
                }
            }

            public sealed class NormalTest : CtorTest
            {
                [Fact]
                public void Simple()
                {
                    Create("cat");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(0), beforeSpaces: 0, totalSpaces: 1);
                    Assert.Equal(0, point.SpacesBefore);
                    Assert.Equal(0, point.SpacesAfter);
                }

                [Fact]
                public void SimpleNonColumnZero()
                {
                    Create("cat");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(1), beforeSpaces: 0, totalSpaces: 1);
                    Assert.Equal(0, point.SpacesBefore);
                    Assert.Equal(0, point.SpacesAfter);
                }
            }

            public sealed class EndTest : CtorTest
            {
                [Fact]
                public void EndOfBuffer()
                {
                    Create("cat");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetEndPoint(), 0, 0);
                    Assert.Equal(0, point.Spaces);
                    Assert.Equal(0, point.SpacesBefore);
                    Assert.Equal(0, point.SpacesAfter);
                }
            }

            /// <summary>
            /// When creating a SnapshotOverlapPoint over a SnapshotPoint just treat it as if it 
            /// is a non-overlapping single width value
            /// </summary>
            public sealed class PointTest : CtorTest
            {
                private void AssertPoint(SnapshotOverlapPoint point, char c)
                {
                    Assert.Equal(0, point.SpacesAfter);
                    Assert.Equal(0, point.SpacesBefore);
                    Assert.Equal(1, point.Spaces);
                    Assert.Equal(c, point.Point.GetChar());
                }

                [Fact]
                public void Tab()
                {
                    Create("\tcat");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(0));
                    AssertPoint(point, '\t');
                }

                [Fact]
                public void Normal()
                {
                    Create("\tcat");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(1));
                    AssertPoint(point, 'c');
                }

                [Fact]
                public void Wide()
                {
                    Create("\t\u3042cat");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(1));
                    AssertPoint(point, '\u3042');
                }

                [Fact]
                public void EndOfBuffer()
                {
                    Create("\t\u3042cat");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetEndPoint());
                    Assert.Equal(0, point.Spaces);
                    Assert.Equal(0, point.SpacesAfter);
                    Assert.Equal(0, point.SpacesBefore);
                }
            }
        }

        public sealed class GetSpaceWithOverlapOrEndTest :  SnapshotOverlapPointTest
        {
            [Fact]
            public void BeforeTab()
            {
                Create("d\tog", "extra");
                var point = SnapshotLineUtil.GetSpaceWithOverlapOrEnd(_textBuffer.GetLine(0), spacesCount: 0, tabStop: 4);
                Assert.Equal(_textBuffer.GetPoint(position: 0), point.Point);
            }

            [Fact]
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
            [Fact]
            public void PartialTab2()
            {
                Create("d\tog", "extra");
                var point = SnapshotLineUtil.GetSpaceWithOverlapOrEnd(_textBuffer.GetLine(0), spacesCount: 2, tabStop: 4);
                Assert.Equal(1, point.SpacesBefore);
                Assert.Equal(3, point.Spaces);
                Assert.Equal(_textBuffer.GetPoint(position: 1), point.Point);
            }

            [Fact]
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
