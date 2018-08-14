using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Vim.EditorHost;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public abstract class VirtualSnapshotColumnTest : VimTestBase
    {
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
        }

        private void CreateRaw(string content)
        {
            _textBuffer = CreateTextBufferRaw(content);
        }

        public sealed class GetForColumnTest : VirtualSnapshotColumnTest
        {
            [WpfFact]
            public void Simple()
            {
                Create("cat", "dog");
                var column = VirtualSnapshotColumn.GetForColumnNumber(_textBuffer.GetLine(0), 3);
                Assert.Equal(0, column.VirtualSpaces);
                Assert.True(column.Column.IsLineBreak);
            }
        }

        public sealed class VirtualStartPointTest : VirtualSnapshotColumnTest
        {
            [WpfFact]
            public void Middle()
            {
                Create("cat", "dog");
                var column = VirtualSnapshotColumn.GetForColumnNumber(_textBuffer.GetLine(0), 1);
                Assert.Equal(1, column.VirtualStartPoint.Position);
                Assert.Equal(0, column.VirtualSpaces);
                Assert.False(column.IsInVirtualSpace);
            }

            [WpfFact]
            public void End()
            {
                Create("cat", "dog");
                var column = VirtualSnapshotColumn.GetForColumnNumber(_textBuffer.GetLine(0), 3);
                Assert.Equal(3, column.VirtualStartPoint.Position);
                Assert.Equal(0, column.VirtualSpaces);
                Assert.False(column.IsInVirtualSpace);
            }

            [WpfFact]
            public void EndBig()
            {
                Create("cat", "dog");
                var column = VirtualSnapshotColumn.GetForColumnNumber(_textBuffer.GetLine(0), 10);
                Assert.Equal(3, column.VirtualStartPoint.Position);
                Assert.Equal(7, column.VirtualSpaces);
                Assert.True(column.IsInVirtualSpace);
            }
        }

        public sealed class AddInLineTest : VirtualSnapshotColumnTest
        {
            [WpfFact]
            public void SimpleAdd()
            {
                Create("cat", "dog");
                var column = VirtualSnapshotColumn.GetForColumnNumber(_textBuffer.GetLine(0), 3);
                Assert.True(column.Column.IsLineBreak);
                column = column.AddInLine(1);
                Assert.True(column.IsInVirtualSpace);
                Assert.Equal(1, column.VirtualSpaces);
            }

            [WpfFact]
            public void SimpleAddNoVirtual()
            {
                Create("cat", "dog");
                var column = _textBuffer.GetVirtualColumnFromPosition(0);
                Assert.False(column.Column.IsLineBreak);
                Assert.False(column.IsInVirtualSpace);
                column = column.AddInLine(1);
                Assert.False(column.Column.IsLineBreak);
                Assert.False(column.IsInVirtualSpace);
                Assert.Equal(1, column.Column.StartPosition);
            }

            [WpfFact]
            public void SimpleAddToLineBreak()
            {
                Create("cat", "dog");
                var column = _textBuffer.GetVirtualColumnFromPosition(2);
                Assert.False(column.Column.IsLineBreak);
                Assert.False(column.IsInVirtualSpace);
                column = column.AddInLine(1);
                Assert.True(column.Column.IsLineBreak);
                Assert.False(column.IsInVirtualSpace);
                Assert.Equal(3, column.Column.StartPosition);
            }

            [WpfFact]
            public void GiantAdd()
            {
                Create("cat", "dog");
                var column = VirtualSnapshotColumn.GetForColumnNumber(_textBuffer.GetLine(0), 3);
                Assert.True(column.Column.IsLineBreak);
                column = column.AddInLine(300);
                Assert.True(column.IsInVirtualSpace);
                Assert.Equal(300, column.VirtualSpaces);
            }

            [WpfFact]
            public void SimpleSubtract()
            {
                Create("cat", "dog");
                var column = VirtualSnapshotColumn.GetForColumnNumber(_textBuffer.GetLine(0), 4);
                Assert.True(column.Column.IsLineBreak);
                Assert.True(column.IsInVirtualSpace);
                column = column.SubtractInLine(1);
                Assert.False(column.IsInVirtualSpace);
                Assert.Equal(0, column.VirtualSpaces);
            }

            [WpfFact]
            public void SubtractPastStart()
            {
                Create("cat", "dog");
                var column = VirtualSnapshotColumn.GetForColumnNumber(_textBuffer.GetLine(0), 0);
                Assert.Throws<ArgumentException>(() => column.SubtractInLine(1));
            }
        }

        public sealed class GetSpaces : VirtualSnapshotColumnTest
        {
            public static readonly int TabStop = 8;

            [WpfFact]
            public void Simple()
            {
                Create("cat", "dog");
                var column = _textBuffer.GetVirtualColumnFromPosition(3, 1);
                Assert.Equal(5, column.GetSpacesIncludingToColumn(TabStop));
                Assert.Equal(4, column.GetSpacesToColumn(TabStop));
                Assert.Equal(1, column.GetSpaces(TabStop));
            }

            [WpfFact]
            public void SimpleBlankLine()
            {
                Create("", "dog");
                var column = _textBuffer.GetVirtualColumnFromPosition(0, 4);
                Assert.Equal(5, column.GetSpacesIncludingToColumn(TabStop));
                Assert.Equal(4, column.GetSpacesToColumn(TabStop));
                Assert.Equal(1, column.GetSpaces(TabStop));
            }
        }

        public sealed class GetColumnForSpaces : VirtualSnapshotColumnTest
        {
            public static readonly int TabStop = 8;

            [WpfFact]
            public void SimpleBlankLine()
            {
                Create("", "dog");
                var column = VirtualSnapshotColumn.GetColumnForSpaces(_textBuffer.GetLine(0), spaces: 0, TabStop);
                Assert.True(column.Column.IsLineBreak);
                Assert.False(column.IsInVirtualSpace);
                Assert.Equal(0, column.VirtualSpaces);
            }

            [WpfFact]
            public void SimpleBlankLineFurther()
            {
                Create("", "dog");
                var column = VirtualSnapshotColumn.GetColumnForSpaces(_textBuffer.GetLine(0), spaces: 1, TabStop);
                Assert.True(column.Column.IsLineBreak);
                Assert.True(column.IsInVirtualSpace);
                Assert.Equal(1, column.VirtualSpaces);
                Assert.Equal(1, column.GetSpacesToColumn(TabStop));
            }
        }
    }
}
