using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using EditorUtils;

namespace Vim.UnitTest
{
    public abstract class SnapshotColumnTest : VimTestBase
    {
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
        }

        public sealed class AddSubtractTest : SnapshotColumnTest
        {
            [Fact]
            public void AddSameLine()
            {
                Create("cat", "dog", "fish");
                var original = new SnapshotColumn(_textBuffer.GetPoint(0));
                var column = original.Add(1);
                Assert.Equal(1, column.Column);
                Assert.Equal(0, column.LineNumber);
            }

            [Fact]
            public void AddNextLine()
            {
                Create("cat", "dog", "fish");
                var original = new SnapshotColumn(_textBuffer.GetPoint(0));
                var column = original.Add(5);
                Assert.Equal(0, column.Column);
                Assert.Equal(1, column.LineNumber);
            }

            [Fact]
            public void AddBeforeLine()
            {
                Create("cat", "dog", "fish");
                var original = new SnapshotColumn(_textBuffer.GetLine(1).Start);
                var column = original.Add(-3);
                Assert.Equal(2, column.Column);
                Assert.Equal(0, column.LineNumber);
            }

            [Fact]
            public void SubtractSameLine()
            {
                Create("cat", "dog", "fish");
                var original = new SnapshotColumn(_textBuffer.GetPoint(1));
                var column = original.Subtract(1);
                Assert.Equal(0, column.Column);
                Assert.Equal(0, column.LineNumber);
            }

            [Fact]
            public void SubtractBeforeLine()
            {
                Create("cat", "dog", "fish");
                var original = new SnapshotColumn(_textBuffer.GetLine(1).Start);
                var column = original.Subtract(3);
                Assert.Equal(2, column.Column);
                Assert.Equal(0, column.LineNumber);
            }
        }
    }
}
