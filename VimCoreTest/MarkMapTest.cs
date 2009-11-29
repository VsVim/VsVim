using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using VimCore;

namespace VimCoreTest
{
    [TestClass]
    public class MarkMapTest
    {
        ITextBuffer _buffer;
        MarkMap _map;

        [TestInitialize]
        public void Init()
        {
            _map = new VimCore.MarkMap();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _map.DeleteAllMarks();
        }

        private void CreateBuffer(params string[] lines)
        {
            _buffer = Utils.EditorUtil.CreateBuffer(lines);
        }

        [TestMethod]
        public void SetMark1()
        {
            CreateBuffer("foo", "bar");
            _map.SetMark(new SnapshotPoint(_buffer.CurrentSnapshot, 0), 'a');
            var opt = _map.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsSome());
            var data = opt.Value;
            Assert.AreEqual(0, data.Position.Position);
            Assert.IsFalse(data.IsInVirtualSpace);
        }

        [TestMethod]
        public void GetLocalMark1()
        {
            CreateBuffer("foo");
            var opt = _map.GetLocalMark(_buffer, 'b');
            Assert.IsFalse(opt.IsSome());
        }

        [TestMethod, Description("Simple insertion shouldn't invalidate the mark")]
        public void TrackReplace1()
        {
            CreateBuffer("foo");
            _map.SetMark(new SnapshotPoint(_buffer.CurrentSnapshot, 0), 'a');
            _buffer.Replace(new Span(0, 1), "b");
            var opt = _map.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(0, opt.Value.Position.Position);
        }

        [TestMethod, Description("Insertions elsewhere on the line should not affect the mark")]
        public void TrackReplace2()
        {
            CreateBuffer("foo");
            _map.SetMark(new SnapshotPoint(_buffer.CurrentSnapshot, 1), 'a');
            _buffer.Replace(new Span(2, 1), "b");
            var opt = _map.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(1, opt.Value.Position.Position);
        }

        [TestMethod, Description("Shrinking the line should just return the position in Virtual Space")]
        public void TrackReplace3()
        {
            CreateBuffer("foo");
            _map.SetMark(new SnapshotPoint(_buffer.CurrentSnapshot, 2), 'a');
            _buffer.Delete(new Span(0, 3));
            var opt = _map.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsSome());
            var data = opt.Value;
            Assert.IsTrue(data.IsInVirtualSpace);
            Assert.AreEqual(0, data.Position.Position);
        }

        [TestMethod, Description("Deleting the line above should not affect the mark")]
        public void TrackReplace4()
        {
            CreateBuffer("foo", "bar");
            _map.SetMark(_buffer.CurrentSnapshot.GetLineFromLineNumber(1).Start, 'a');
            _buffer.Delete(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak.Span);
            var opt = _map.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsSome());
            var data = opt.Value;
            Assert.AreEqual(0, data.Position.Position);
        }

        [TestMethod]
        public void TrackDeleteLine1()
        {
            CreateBuffer("foo", "bar");
            _map.SetMark(_buffer.CurrentSnapshot.GetLineFromLineNumber(1).Start, 'a');
            var span = new SnapshotSpan(
                _buffer.CurrentSnapshot.GetLineFromLineNumber(0).End,
                _buffer.CurrentSnapshot.GetLineFromLineNumber(1).EndIncludingLineBreak);
            _buffer.Delete(span.Span);
            var opt = _map.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsNone());
        }

        // TODO: Test Undo logic
       
    }
}
