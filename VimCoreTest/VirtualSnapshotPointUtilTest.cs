using EditorUtils;
using Microsoft.VisualStudio.Text;
using Xunit;

namespace Vim.UnitTest
{
    public class VirtualSnapshotPointUtilTest : VimTestBase
    {
        ITextBuffer _buffer;
        ITextSnapshot _snapshot;

        public void Create(params string[] lines)
        {
            _buffer = CreateTextBuffer(lines);
            _snapshot = _buffer.CurrentSnapshot;
        }

        [Fact]
        public void AddOneOnSameLine1()
        {
            Create("dog cat");
            var point = new VirtualSnapshotPoint(_buffer.GetPoint(0));
            point = VirtualSnapshotPointUtil.AddOneOnSameLine(point);
            Assert.Equal(_buffer.GetPoint(1), point.Position);
            Assert.False(point.IsInVirtualSpace);
        }

        [Fact]
        public void AddOneOnSameLine2()
        {
            Create("dog");
            var point = new VirtualSnapshotPoint(_buffer.GetLine(0).EndIncludingLineBreak);
            point = VirtualSnapshotPointUtil.AddOneOnSameLine(point);
            Assert.Equal(_buffer.GetLine(0).EndIncludingLineBreak, point.Position);
            Assert.True(point.IsInVirtualSpace);
            Assert.Equal(1, point.VirtualSpaces);
        }

        [Fact]
        public void AddOneOnSameLine3()
        {
            Create("dog");
            var point = new VirtualSnapshotPoint(_buffer.GetLine(0).EndIncludingLineBreak, 1);
            point = VirtualSnapshotPointUtil.AddOneOnSameLine(point);
            Assert.Equal(_buffer.GetLine(0).EndIncludingLineBreak, point.Position);
            Assert.True(point.IsInVirtualSpace);
            Assert.Equal(2, point.VirtualSpaces);
        }
    }
}
