using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class VirtualSnapshotPointUtilTest
    {
        ITextBuffer _buffer;
        ITextSnapshot _snapshot;

        public void Create(params string[] lines)
        {
            _buffer = EditorUtil.CreateTextBuffer(lines);
            _snapshot = _buffer.CurrentSnapshot;
        }

        [TearDown]
        public void TearDown()
        {
            _buffer = null;
            _snapshot = null;
        }

        [Test]
        public void AddOneOnSameLine1()
        {
            Create("dog cat");
            var point = new VirtualSnapshotPoint(_buffer.GetPoint(0));
            point = VirtualSnapshotPointUtil.AddOneOnSameLine(point);
            Assert.AreEqual(_buffer.GetPoint(1), point.Position);
            Assert.IsFalse(point.IsInVirtualSpace);
        }

        [Test]
        public void AddOneOnSameLine2()
        {
            Create("dog");
            var point = new VirtualSnapshotPoint(_buffer.GetLine(0).EndIncludingLineBreak);
            point = VirtualSnapshotPointUtil.AddOneOnSameLine(point);
            Assert.AreEqual(_buffer.GetLine(0).EndIncludingLineBreak, point.Position);
            Assert.IsTrue(point.IsInVirtualSpace);
            Assert.AreEqual(1, point.VirtualSpaces);
        }

        [Test]
        public void AddOneOnSameLine3()
        {
            Create("dog");
            var point = new VirtualSnapshotPoint(_buffer.GetLine(0).EndIncludingLineBreak, 1);
            point = VirtualSnapshotPointUtil.AddOneOnSameLine(point);
            Assert.AreEqual(_buffer.GetLine(0).EndIncludingLineBreak, point.Position);
            Assert.IsTrue(point.IsInVirtualSpace);
            Assert.AreEqual(2, point.VirtualSpaces);
        }
    }
}
