using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class MarkMapTest : VimTestBase
    {
        private MarkMap _markMapRaw;
        private IMarkMap _markMap;
        private Mark _globalMarkC = Mark.NewGlobalMark(Letter.C);
        private Mark _localMarkC = Mark.NewLocalMark(LocalMark.NewLetter(Letter.C));
        private Mark _localMarkD = Mark.NewLocalMark(LocalMark.NewLetter(Letter.D));

        [SetUp]
        public void Setup()
        {
            var service = new BufferTrackingService();
            _markMapRaw = new MarkMap(service);
            _markMap = _markMapRaw;
        }

        [TearDown]
        public void Cleanup()
        {
            _markMap.ClearGlobalMarks();
        }

        /// <summary>
        /// Set a simple mark and ensure we can retrieve it
        /// </summary>
        [Test]
        public void SetMark_Local_Simple()
        {
            var vimTextBuffer = CreateVimTextBuffer("dog", "cat");
            _markMap.SetMark(_localMarkC, vimTextBuffer, 0, 1);
            var option = _markMap.GetMark(_localMarkC, vimTextBuffer);
            Assert.IsTrue(option.IsSome());
            Assert.AreEqual(vimTextBuffer.TextBuffer.GetPoint(1), option.Value.Position);
        }

        /// <summary>
        /// Set a simple mark in virtual space and ensure that it works 
        /// </summary>
        [Test]
        public void SetMark_Local_VirtualSpace()
        {
            var vimTextBuffer = CreateVimTextBuffer("dog", "cat");
            _markMap.SetMark(_localMarkC, vimTextBuffer, 0, 5);
            var option = _markMap.GetMark(_localMarkC, vimTextBuffer);
            Assert.IsTrue(option.IsSome());
            Assert.AreEqual(3, option.Value.Position.Position);
            Assert.AreEqual(2, option.Value.VirtualSpaces);
        }

        /// <summary>
        /// Querying for a mark which is not set should produce an empty option
        /// </summary>
        [Test]
        public void GetMark_Local_NotSet()
        {
            var vimTextBuffer = CreateVimTextBuffer("dog", "cat");
            var point = _markMap.GetMark(_localMarkC, vimTextBuffer);
            Assert.IsTrue(point.IsNone());
        }

        /// <summary>
        /// Querying for a global mark which is not set should produce an empty option
        /// </summary>
        [Test]
        public void GetMark_Global_NotSet()
        {
            var vimTextBuffer = CreateVimTextBuffer("dog", "cat");
            var point = _markMap.GetMark(_globalMarkC, vimTextBuffer);
            Assert.IsTrue(point.IsNone());
        }

        /// <summary>
        /// Simple insertion after shouldn't invalidate the mark
        /// </summary>
        [Test]
        public void Track_SimpleInsertAfter()
        {
            var vimTextBuffer = CreateVimTextBuffer("dog", "cat");
            _markMapRaw.SetMark(_localMarkC, vimTextBuffer, 0, 0);
            vimTextBuffer.TextBuffer.Replace(new Span(0, 1), "b");
            var option = _markMap.GetMark(_localMarkC, vimTextBuffer);
            Assert.IsTrue(option.IsSome());
            Assert.AreEqual(0, option.Value.Position.Position);
        }

        /// <summary>
        /// Insertion elsewhere in the ITextBuffer shouldn't affect the mark
        /// </summary>
        [Test]
        public void Track_ReplaceInbuffer()
        {
            var vimTextBuffer = CreateVimTextBuffer("foo");
            _markMap.SetMark(_localMarkC, vimTextBuffer, 0, 1);
            vimTextBuffer.TextBuffer.Replace(new Span(2, 1), "b");
            var option = _markMap.GetMark(_localMarkC, vimTextBuffer);
            Assert.IsTrue(option.IsSome());
            Assert.AreEqual(1, option.Value.Position.Position);
        }

        /// <summary>
        /// When shrinking a line where we are tracking a line column then we should just
        /// return the point in virtual space
        /// </summary>
        [Test]
        public void Track_ShrinkLineBelowMark()
        {
            var vimTextBuffer = CreateVimTextBuffer("foo");
            _markMap.SetMark(_localMarkC, vimTextBuffer, 0, 2);
            vimTextBuffer.TextBuffer.Delete(new Span(0, 3));
            var option = _markMap.GetMark(_localMarkC, vimTextBuffer);
            Assert.IsTrue(option.IsSome());
            var point = option.Value;
            Assert.IsTrue(point.IsInVirtualSpace);
            Assert.AreEqual(0, point.Position.Position);
        }

        /// <summary>
        /// Deleting the line above the mark shouldn't affect it other than to move it up 
        /// a line
        /// </summary>
        [Test]
        public void Track_DeleteLineAbove()
        {
            var vimTextBuffer = CreateVimTextBuffer("foo", "bar");
            _markMap.SetMark(_localMarkC, vimTextBuffer, 1, 0);
            vimTextBuffer.TextBuffer.Delete(vimTextBuffer.TextBuffer.GetLine(0).ExtentIncludingLineBreak.Span);
            var option = _markMap.GetMark(_localMarkC, vimTextBuffer);
            Assert.IsTrue(option.IsSome());
            var point = option.Value;
            Assert.AreEqual(0, point.Position.Position);
        }

        /// <summary>
        /// Deleting the line the mark is on should cause the mark to be invalidated
        /// </summary>
        [Test]
        public void Track_DeleteLine()
        {
            var vimTextBuffer = CreateVimTextBuffer("cat", "dog");
            _markMap.SetMark(_localMarkC, vimTextBuffer, 1, 0);
            var span = new SnapshotSpan(
                vimTextBuffer.TextBuffer.GetLine(0).End,
                vimTextBuffer.TextBuffer.GetLine(1).EndIncludingLineBreak);
            vimTextBuffer.TextBuffer.Delete(span.Span);
            var option = _markMap.GetMark(_localMarkC, vimTextBuffer);
            Assert.IsTrue(option.IsNone());
        }

        /// <summary>
        /// Clearing out all global marks should work on an empty map
        /// </summary>
        [Test]
        public void ClearGlobalMarks_Empty()
        {
            _markMap.ClearGlobalMarks();
        }

        /// <summary>
        /// Clearing out the global marks shouldn't affect any local marks
        /// </summary>
        [Test]
        public void ClearGlobalMarks_NoAffectOnLocal()
        {
            var vimTextBuffer = CreateVimTextBuffer("hello world");
            _markMap.SetMark(_localMarkD, vimTextBuffer, 0, 1);
            _markMap.ClearGlobalMarks();
            Assert.IsTrue(_markMap.GetMark(_localMarkD, vimTextBuffer).IsSome());
        }
    }
}
