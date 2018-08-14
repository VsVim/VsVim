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
                [WpfFact]
                public void TabStart()
                {
                    Create("\t");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(0), beforeSpaces: 0, totalSpaces: 4);
                    Assert.Equal(0, point.SpacesBefore);
                    Assert.Equal(3, point.SpacesAfter);
                }

                [WpfFact]
                public void TabEnd()
                {
                    Create("\t");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(0), beforeSpaces: 3, totalSpaces: 4);
                    Assert.Equal(3, point.SpacesBefore);
                    Assert.Equal(0, point.SpacesAfter);
                }

                [WpfFact]
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
                [WpfFact]
                public void Simple()
                {
                    Create("cat");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(0), beforeSpaces: 0, totalSpaces: 1);
                    Assert.Equal(0, point.SpacesBefore);
                    Assert.Equal(0, point.SpacesAfter);
                }

                [WpfFact]
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
                [WpfFact]
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

                [WpfFact]
                public void Tab()
                {
                    Create("\tcat");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(0));
                    AssertPoint(point, '\t');
                }

                [WpfFact]
                public void Normal()
                {
                    Create("\tcat");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(1));
                    AssertPoint(point, 'c');
                }

                [WpfFact]
                public void Wide()
                {
                    Create("\t\u3042cat");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(1));
                    AssertPoint(point, '\u3042');
                }

                [WpfFact]
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
    }
}
