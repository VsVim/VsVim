using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Xunit;
using EditorUtils;

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
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(0), before: 0, width: 4);
                    Assert.Equal(0, point.SpacesBefore);
                    Assert.Equal(3, point.SpacesAfter);
                }

                [Fact]
                public void TabEnd()
                {
                    Create("\t");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(0), before: 3, width: 4);
                    Assert.Equal(3, point.SpacesBefore);
                    Assert.Equal(0, point.SpacesAfter);
                }

                [Fact]
                public void TabMiddle()
                {
                    Create("\t");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(0), before: 1, width: 4);
                    Assert.Equal(1, point.SpacesBefore);
                    Assert.Equal(2, point.SpacesAfter);
                    Assert.Equal(4, point.Width);
                }
            }

            public sealed class NormalTest : CtorTest
            {
                [Fact]
                public void Simple()
                {
                    Create("cat");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(0), before: 0, width: 1);
                    Assert.Equal(0, point.SpacesBefore);
                    Assert.Equal(0, point.SpacesAfter);
                }

                [Fact]
                public void SimpleNonColumnZero()
                {
                    Create("cat");
                    var point = new SnapshotOverlapPoint(_textBuffer.GetPoint(1), before: 0, width: 1);
                    Assert.Equal(0, point.SpacesBefore);
                    Assert.Equal(0, point.SpacesAfter);
                }
            }
        }
    }
}
