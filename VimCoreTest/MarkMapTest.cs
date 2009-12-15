using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Microsoft.VisualStudio.Text;
using Vim;
using VimCoreTest.Utils;

namespace VimCoreTest
{
    [TestFixture]
    public class MarkMapTest
    {
        ITextBuffer _buffer;
        MarkMap _map;

        [SetUp]
        public void Init()
        {
            _map = new Vim.MarkMap();
        }

        [TearDown]
        public void Cleanup()
        {
            _map.DeleteAllMarks();
        }

        private void CreateBuffer(params string[] lines)
        {
            _buffer = Utils.EditorUtil.CreateBuffer(lines);
        }

        [Test]
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

        [Test]
        public void GetLocalMark1()
        {
            CreateBuffer("foo");
            var opt = _map.GetLocalMark(_buffer, 'b');
            Assert.IsFalse(opt.IsSome());
        }

        [Test, Description("Simple insertion shouldn't invalidate the mark")]
        public void TrackReplace1()
        {
            CreateBuffer("foo");
            _map.SetMark(new SnapshotPoint(_buffer.CurrentSnapshot, 0), 'a');
            _buffer.Replace(new Span(0, 1), "b");
            var opt = _map.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(0, opt.Value.Position.Position);
        }

        [Test, Description("Insertions elsewhere on the line should not affect the mark")]
        public void TrackReplace2()
        {
            CreateBuffer("foo");
            _map.SetMark(new SnapshotPoint(_buffer.CurrentSnapshot, 1), 'a');
            _buffer.Replace(new Span(2, 1), "b");
            var opt = _map.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(1, opt.Value.Position.Position);
        }

        [Test, Description("Shrinking the line should just return the position in Virtual Space")]
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

        [Test, Description("Deleting the line above should not affect the mark")]
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

        [Test]
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

        [Test, Description("Deletion of a previous line shouldn't affect the mark")]
        public void TrackDeleteLine2()
        {
            CreateBuffer("foo", "bar", "baz");
            _map.SetMark(_buffer.CurrentSnapshot.GetLineFromLineNumber(2).Start, 'a');
            _buffer.Delete(_buffer.CurrentSnapshot.GetLineFromLineNumber(1).ExtentIncludingLineBreak.Span);
            var opt = _map.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsSome());
            var data = opt.Value;
            Assert.AreEqual(_buffer.CurrentSnapshot.GetLineFromLineNumber(1).Start, data.Position);
        }

        [Test, Description("Deleting a line in the middle of the buffer")]
        public void TrackDeleteLine3()
        {
            CreateBuffer("foo", "bar", "baz");
            _map.SetMark(_buffer.CurrentSnapshot.GetLineFromLineNumber(1).Start, 'a');
            _buffer.Delete(_buffer.CurrentSnapshot.GetLineFromLineNumber(1).ExtentIncludingLineBreak.Span);
            var opt = _map.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsNone());
        }

        [Test, Description("Deleting a non-existant mark is OK")]
        public void DeleteMark1()
        {
            CreateBuffer("foo");
            Assert.IsFalse(_map.DeleteMark(_buffer, 'a'));
        }

        [Test, Description("Simple Mark deletion")]
        public void DeleteMark2()
        {
            CreateBuffer("foo");
            _map.SetMark(new SnapshotPoint(_buffer.CurrentSnapshot, 0), 'a');
            Assert.IsTrue(_map.DeleteMark(_buffer, 'a'));
            Assert.IsTrue(_map.GetLocalMark(_buffer, 'a').IsNone());
        }

        [Test, Description("Double deletion of a mark")]
        public void DeleteMark3()
        {
            CreateBuffer("foo");
            _map.SetMark(new SnapshotPoint(_buffer.CurrentSnapshot, 0), 'a');
            Assert.IsTrue(_map.DeleteMark(_buffer, 'a'));
            Assert.IsTrue(_map.GetLocalMark(_buffer, 'a').IsNone());
            Assert.IsFalse(_map.DeleteMark(_buffer, 'a'));
            Assert.IsTrue(_map.GetLocalMark(_buffer, 'a').IsNone());
        }

        [Test, Description("Deleting a mark in one buffer shouldn't affect another")]
        public void DeleteMark4()
        {
            CreateBuffer("foo");
            var buffer2 = Utils.EditorUtil.CreateBuffer("baz");
            _map.SetMark(new SnapshotPoint(_buffer.CurrentSnapshot, 0), 'a');
            _map.SetMark(new SnapshotPoint(buffer2.CurrentSnapshot, 0), 'a');
            Assert.IsTrue(_map.DeleteMark(buffer2, 'a'));
            Assert.IsTrue(_map.GetLocalMark(_buffer, 'a').IsSome());
        }

        [Test, Description("Should work on an empty map")]
        public void DeleteAllMarks()
        {
            _map.DeleteAllMarks();
        }

        [Test]
        public void DeleteAllMarks2()
        {
            CreateBuffer();
            _map.SetMark(new SnapshotPoint(_buffer.CurrentSnapshot, 0), 'a');
            _map.DeleteAllMarks();
            Assert.IsTrue(_map.GetLocalMark(_buffer, 'a').IsNone());
        }

        [Test]
        public void DeleteAllMarksForBuffer1()
        {
            var buf1 = EditorUtil.CreateBuffer("foo");
            _map.SetMark(new SnapshotPoint(buf1.CurrentSnapshot, 0), 'a');
            _map.DeleteAllMarksForBuffer(buf1);
            Assert.IsTrue(_map.GetLocalMark(buf1, 'a').IsNone());
        }

        [Test]
        public void DeleteAllMarksForBuffer2()
        {
            var buf1 = EditorUtil.CreateBuffer("foo");
            var buf2 = EditorUtil.CreateBuffer("bar");
            _map.SetMark(new SnapshotPoint(buf1.CurrentSnapshot, 0), 'a');
            _map.SetMark(new SnapshotPoint(buf2.CurrentSnapshot, 0), 'b');
            _map.DeleteAllMarksForBuffer(buf1);
            Assert.IsTrue(_map.GetLocalMark(buf1, 'a').IsNone());
            Assert.IsFalse(_map.GetLocalMark(buf2, 'b').IsNone());
        }

        [Test]
        public void IsLocalMark1()
        {
            Assert.IsTrue(MarkMap.IsLocalMark('a'));
            Assert.IsTrue(MarkMap.IsLocalMark('b'));
            Assert.IsTrue(MarkMap.IsLocalMark(']'));
        }

        [Test]
        public void IsLocalMark2()
        {
            Assert.IsFalse(MarkMap.IsLocalMark('B'));
            Assert.IsFalse(MarkMap.IsLocalMark('Z'));
            Assert.IsFalse(MarkMap.IsLocalMark('1'));
        }

        [Test]
        public void GetMark1()
        {
            var buf1 = EditorUtil.CreateBuffer("foo");
            _map.SetMark(new SnapshotPoint(buf1.CurrentSnapshot, 0), 'A');
            var ret = _map.GetMark(buf1, 'A');
            Assert.IsTrue(ret.IsSome());
            Assert.AreEqual(0, ret.Value.Position);
        }

        [Test]
        public void GetMark2()
        {
            var buf1 = EditorUtil.CreateBuffer("foo");
            var buf2 = EditorUtil.CreateBuffer("bar");
            _map.SetMark(new SnapshotPoint(buf1.CurrentSnapshot, 0), 'A');
            var ret = _map.GetMark(buf2, 'A');
            Assert.IsTrue(ret.IsNone());
        }

        // TODO: Test Undo logic

    }
}
