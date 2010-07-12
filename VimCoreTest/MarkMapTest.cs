using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using VimCore.Test.Mock;
using VimCore.Test.Utils;

namespace VimCore.Test
{
    [TestFixture]
    public class MarkMapTest
    {
        ITextView _textView;
        ITextBuffer _buffer;
        MarkMap _mapRaw;
        IMarkMap _map;
        IVimBufferCreationListener _mapListener;

        [SetUp]
        public void Init()
        {
            var service = new TrackingLineColumnService();
            _mapRaw = new Vim.MarkMap(service);
            _map = _mapRaw;
            _mapListener = _mapRaw;
        }

        [TearDown]
        public void Cleanup()
        {
            _mapRaw.DeleteAllMarks();
        }

        private void Create(params string[] lines)
        {
            _textView = Utils.EditorUtil.CreateView(lines);
            _buffer = _textView.TextBuffer;
            var vimBuffer = new MockVimBuffer() { TextViewImpl = _textView, TextBufferImpl = _textView.TextBuffer };
            _mapListener.VimBufferCreated(vimBuffer);
        }

        [Test]
        public void SetLocalMark1()
        {
            Create("foo", "bar");
            _mapRaw.SetLocalMark(new SnapshotPoint(_buffer.CurrentSnapshot, 0), 'a');
            var opt = _mapRaw.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsSome());
            var data = opt.Value;
            Assert.AreEqual(0, data.Position.Position);
            Assert.IsFalse(data.IsInVirtualSpace);
        }

        [Test]
        public void GetLocalMark1()
        {
            Create("foo");
            var opt = _mapRaw.GetLocalMark(_buffer, 'b');
            Assert.IsFalse(opt.IsSome());
        }

        [Test, Description("Simple insertion shouldn't invalidate the mark")]
        public void TrackReplace1()
        {
            Create("foo");
            _mapRaw.SetLocalMark(new SnapshotPoint(_buffer.CurrentSnapshot, 0), 'a');
            _buffer.Replace(new Span(0, 1), "b");
            var opt = _mapRaw.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(0, opt.Value.Position.Position);
        }

        [Test, Description("Insertions elsewhere on the line should not affect the mark")]
        public void TrackReplace2()
        {
            Create("foo");
            _mapRaw.SetLocalMark(new SnapshotPoint(_buffer.CurrentSnapshot, 1), 'a');
            _buffer.Replace(new Span(2, 1), "b");
            var opt = _mapRaw.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsSome());
            Assert.AreEqual(1, opt.Value.Position.Position);
        }

        [Test, Description("Shrinking the line should just return the position in Virtual Space")]
        public void TrackReplace3()
        {
            Create("foo");
            _mapRaw.SetLocalMark(new SnapshotPoint(_buffer.CurrentSnapshot, 2), 'a');
            _buffer.Delete(new Span(0, 3));
            var opt = _mapRaw.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsSome());
            var data = opt.Value;
            Assert.IsTrue(data.IsInVirtualSpace);
            Assert.AreEqual(0, data.Position.Position);
        }

        [Test, Description("Deleting the line above should not affect the mark")]
        public void TrackReplace4()
        {
            Create("foo", "bar");
            _mapRaw.SetLocalMark(_buffer.CurrentSnapshot.GetLineFromLineNumber(1).Start, 'a');
            _buffer.Delete(_buffer.CurrentSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak.Span);
            var opt = _mapRaw.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsSome());
            var data = opt.Value;
            Assert.AreEqual(0, data.Position.Position);
        }

        [Test]
        public void TrackDeleteLine1()
        {
            Create("foo", "bar");
            _mapRaw.SetLocalMark(_buffer.CurrentSnapshot.GetLineFromLineNumber(1).Start, 'a');
            var span = new SnapshotSpan(
                _buffer.CurrentSnapshot.GetLineFromLineNumber(0).End,
                _buffer.CurrentSnapshot.GetLineFromLineNumber(1).EndIncludingLineBreak);
            _buffer.Delete(span.Span);
            var opt = _mapRaw.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsNone());
        }

        [Test, Description("Deletion of a previous line shouldn't affect the mark")]
        public void TrackDeleteLine2()
        {
            Create("foo", "bar", "baz");
            _mapRaw.SetLocalMark(_buffer.CurrentSnapshot.GetLineFromLineNumber(2).Start, 'a');
            _buffer.Delete(_buffer.CurrentSnapshot.GetLineFromLineNumber(1).ExtentIncludingLineBreak.Span);
            var opt = _mapRaw.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsSome());
            var data = opt.Value;
            Assert.AreEqual(_buffer.CurrentSnapshot.GetLineFromLineNumber(1).Start, data.Position);
        }

        [Test, Description("Deleting a line in the middle of the buffer")]
        public void TrackDeleteLine3()
        {
            Create("foo", "bar", "baz");
            _mapRaw.SetLocalMark(_buffer.CurrentSnapshot.GetLineFromLineNumber(1).Start, 'a');
            _buffer.Delete(_buffer.CurrentSnapshot.GetLineFromLineNumber(1).ExtentIncludingLineBreak.Span);
            var opt = _mapRaw.GetLocalMark(_buffer, 'a');
            Assert.IsTrue(opt.IsNone());
        }

        [Test, Description("Deleting a non-existant mark is OK")]
        public void DeleteLocalMark1()
        {
            Create("foo");
            Assert.IsFalse(_mapRaw.DeleteLocalMark(_buffer, 'a'));
        }

        [Test, Description("Simple Mark deletion")]
        public void DeleteLocalMark2()
        {
            Create("foo");
            _mapRaw.SetLocalMark(new SnapshotPoint(_buffer.CurrentSnapshot, 0), 'a');
            Assert.IsTrue(_mapRaw.DeleteLocalMark(_buffer, 'a'));
            Assert.IsTrue(_mapRaw.GetLocalMark(_buffer, 'a').IsNone());
        }

        [Test, Description("Double deletion of a mark")]
        public void DeleteLocalMark3()
        {
            Create("foo");
            _mapRaw.SetLocalMark(new SnapshotPoint(_buffer.CurrentSnapshot, 0), 'a');
            Assert.IsTrue(_mapRaw.DeleteLocalMark(_buffer, 'a'));
            Assert.IsTrue(_mapRaw.GetLocalMark(_buffer, 'a').IsNone());
            Assert.IsFalse(_mapRaw.DeleteLocalMark(_buffer, 'a'));
            Assert.IsTrue(_mapRaw.GetLocalMark(_buffer, 'a').IsNone());
        }

        [Test, Description("Deleting a mark in one buffer shouldn't affect another")]
        public void DeleteLocalMark4()
        {
            Create("foo");
            var buffer2 = Utils.EditorUtil.CreateBuffer("baz");
            _mapRaw.SetLocalMark(new SnapshotPoint(_buffer.CurrentSnapshot, 0), 'a');
            _mapRaw.SetLocalMark(new SnapshotPoint(buffer2.CurrentSnapshot, 0), 'a');
            Assert.IsTrue(_mapRaw.DeleteLocalMark(buffer2, 'a'));
            Assert.IsTrue(_mapRaw.GetLocalMark(_buffer, 'a').IsSome());
        }

        [Test, Description("Should work on an empty map")]
        public void DeleteAllMarks()
        {
            _mapRaw.DeleteAllMarks();
        }

        [Test]
        public void DeleteAllMarks2()
        {
            Create();
            _mapRaw.SetLocalMark(new SnapshotPoint(_buffer.CurrentSnapshot, 0), 'a');
            _mapRaw.DeleteAllMarks();
            Assert.IsTrue(_mapRaw.GetLocalMark(_buffer, 'a').IsNone());
        }

        [Test]
        public void DeleteAllMarksForBuffer1()
        {
            var buf1 = EditorUtil.CreateBuffer("foo");
            _mapRaw.SetLocalMark(new SnapshotPoint(buf1.CurrentSnapshot, 0), 'a');
            _mapRaw.DeleteAllMarksForBuffer(buf1);
            Assert.IsTrue(_mapRaw.GetLocalMark(buf1, 'a').IsNone());
        }

        [Test]
        public void DeleteAllMarksForBuffer2()
        {
            var buf1 = EditorUtil.CreateBuffer("foo");
            var buf2 = EditorUtil.CreateBuffer("bar");
            _mapRaw.SetLocalMark(new SnapshotPoint(buf1.CurrentSnapshot, 0), 'a');
            _mapRaw.SetLocalMark(new SnapshotPoint(buf2.CurrentSnapshot, 0), 'b');
            _mapRaw.DeleteAllMarksForBuffer(buf1);
            Assert.IsTrue(_mapRaw.GetLocalMark(buf1, 'a').IsNone());
            Assert.IsFalse(_mapRaw.GetLocalMark(buf2, 'b').IsNone());
        }

        [Test]
        public void IsLocalMark1()
        {
            Assert.IsTrue(MarkMap.IsLocalMark('a'));
            Assert.IsTrue(MarkMap.IsLocalMark('b'));
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
            _mapRaw.SetLocalMark(new SnapshotPoint(buf1.CurrentSnapshot, 0), 'a');
            var ret = _mapRaw.GetMark(buf1, 'a');
            Assert.IsTrue(ret.IsSome());
            Assert.AreEqual(0, ret.Value.Position);
        }

        [Test]
        public void GetMark2()
        {
            var buf1 = EditorUtil.CreateBuffer("foo");
            var buf2 = EditorUtil.CreateBuffer("bar");
            _mapRaw.SetLocalMark(new SnapshotPoint(buf1.CurrentSnapshot, 0), 'a');
            var ret = _mapRaw.GetMark(buf2, 'a');
            Assert.IsTrue(ret.IsNone());
        }

        [Test]
        [Description("Closed should remove all data")]
        public void BufferLifetime1()
        {
            var textView = EditorUtil.CreateView("foo");
            var vimBuffer = new MockVimBuffer() { TextBufferImpl = textView.TextBuffer, TextViewImpl = textView};
            _mapListener.VimBufferCreated(vimBuffer);
            _map.SetLocalMark(new SnapshotPoint(textView.TextSnapshot, 0), 'c');
            vimBuffer.RaiseClosed();
            Assert.IsTrue(_map.GetLocalMark(textView.TextBuffer, 'c').IsNone());
        }

        [Test]
        public void MarkSelectionStart1()
        {
            Create("the", "quick", "fox");
            Assert.IsTrue(_map.GetMark(_buffer, '<').IsNone());
        }

        [Test]
        public void MarkSelectionStart2()
        {
            Create("the", "quick", "fox");
            _textView.Selection.Select(_buffer.GetLineSpan(0), false);
            Assert.AreEqual(0, _map.GetMark(_buffer,'<').Value.Position.Position);
        }

        [Test]
        public void MarkSelectionStart3()
        {
            Create("the", "quick", "fox");
            _textView.Selection.Select(_buffer.GetLineSpan(0), false);
            _textView.Selection.Clear();
            Assert.AreEqual(0, _map.GetMark(_buffer,'<').Value.Position.Position);
        }


        // TODO: Test Undo logic

    }
}
