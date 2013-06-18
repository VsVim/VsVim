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
    }
}
