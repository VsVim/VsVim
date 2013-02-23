using EditorUtils;
using Microsoft.VisualStudio.Text;
using Xunit;
using Vim.Extensions;
using System.Collections.Generic;
using System.Linq;

namespace Vim.UnitTest
{
    public sealed class MarkMapTest : VimTestBase
    {
        private MarkMap _markMapRaw;
        private IMarkMap _markMap;
        private Mark _globalMarkC = Mark.NewGlobalMark(Letter.C);
        private Mark _localMarkC = Mark.NewLocalMark(LocalMark.NewLetter(Letter.C));
        private Mark _localMarkD = Mark.NewLocalMark(LocalMark.NewLetter(Letter.D));

        public MarkMapTest()
        {
            var service = new BufferTrackingService();
            _markMapRaw = new MarkMap(service);
            _markMap = _markMapRaw;
        }

        /// <summary>
        /// Set a simple mark and ensure we can retrieve it
        /// </summary>
        [Fact]
        public void SetMark_Local_Simple()
        {
            var vimBufferData = CreateVimBufferData("dog", "cat");
            _markMap.SetMark(_localMarkC, vimBufferData, 0, 1);
            var option = _markMap.GetMark(_localMarkC, vimBufferData);
            Assert.True(option.IsSome());
            Assert.Equal(vimBufferData.TextBuffer.GetPoint(1), option.Value.Position);
        }

        /// <summary>
        /// Set a simple mark in virtual space and ensure that it works 
        /// </summary>
        [Fact]
        public void SetMark_Local_VirtualSpace()
        {
            var vimBufferData = CreateVimBufferData("dog", "cat");
            _markMap.SetMark(_localMarkC, vimBufferData, 0, 5);
            var option = _markMap.GetMark(_localMarkC, vimBufferData);
            Assert.True(option.IsSome());
            Assert.Equal(3, option.Value.Position.Position);
            Assert.Equal(2, option.Value.VirtualSpaces);
        }

        /// <summary>
        /// Querying for a mark which is not set should produce an empty option
        /// </summary>
        [Fact]
        public void GetMark_Local_NotSet()
        {
            var vimBufferData = CreateVimBufferData("dog", "cat");
            var point = _markMap.GetMark(_localMarkC, vimBufferData);
            Assert.True(point.IsNone());
        }

        /// <summary>
        /// Querying for a global mark which is not set should produce an empty option
        /// </summary>
        [Fact]
        public void GetMark_Global_NotSet()
        {
            var vimBufferData = CreateVimBufferData("dog", "cat");
            var point = _markMap.GetMark(_globalMarkC, vimBufferData);
            Assert.True(point.IsNone());
        }

        /// <summary>
        /// The GetMark function should always return the VirtualSnapshotPoint in the context of the 
        /// provided IVimBuffer.  If the Global mark exists in another ITextBuffer it should not
        /// be returned
        /// </summary>
        [Fact]
        public void GetMark_Global_CrossBuffer()
        {
            var vimBufferData1 = CreateVimBufferData("dog", "cat");
            var vimBufferData2 = CreateVimBufferData("dog", "cat");
            _markMap.SetGlobalMark(Letter.A, vimBufferData1.VimTextBuffer, 1, 0);
            var option = _markMap.GetMark(Mark.NewGlobalMark(Letter.A), vimBufferData1);
            Assert.True(option.IsSome());
            option = _markMap.GetMark(Mark.NewGlobalMark(Letter.A), vimBufferData2);
            Assert.True(option.IsNone());
        }

        /// <summary>
        /// Simple insertion after shouldn't invalidate the mark
        /// </summary>
        [Fact]
        public void Track_SimpleInsertAfter()
        {
            var vimBufferData = CreateVimBufferData("dog", "cat");
            _markMapRaw.SetMark(_localMarkC, vimBufferData, 0, 0);
            vimBufferData.TextBuffer.Replace(new Span(0, 1), "b");
            var option = _markMap.GetMark(_localMarkC, vimBufferData);
            Assert.True(option.IsSome());
            Assert.Equal(0, option.Value.Position.Position);
        }

        /// <summary>
        /// Insertion elsewhere in the ITextBuffer shouldn't affect the mark
        /// </summary>
        [Fact]
        public void Track_ReplaceInbuffer()
        {
            var vimBufferData = CreateVimBufferData("foo");
            _markMap.SetMark(_localMarkC, vimBufferData, 0, 1);
            vimBufferData.TextBuffer.Replace(new Span(2, 1), "b");
            var option = _markMap.GetMark(_localMarkC, vimBufferData);
            Assert.True(option.IsSome());
            Assert.Equal(1, option.Value.Position.Position);
        }

        /// <summary>
        /// When shrinking a line where we are tracking a line column then we should just
        /// return the point in virtual space
        /// </summary>
        [Fact]
        public void Track_ShrinkLineBelowMark()
        {
            var vimBufferData = CreateVimBufferData("foo");
            _markMap.SetMark(_localMarkC, vimBufferData, 0, 2);
            vimBufferData.TextBuffer.Delete(new Span(0, 3));
            var option = _markMap.GetMark(_localMarkC, vimBufferData);
            Assert.True(option.IsSome());
            var point = option.Value;
            Assert.True(point.IsInVirtualSpace);
            Assert.Equal(0, point.Position.Position);
        }

        /// <summary>
        /// Deleting the line above the mark shouldn't affect it other than to move it up 
        /// a line
        /// </summary>
        [Fact]
        public void Track_DeleteLineAbove()
        {
            var vimBufferData = CreateVimBufferData("foo", "bar");
            _markMap.SetMark(_localMarkC, vimBufferData, 1, 0);
            vimBufferData.TextBuffer.Delete(vimBufferData.TextBuffer.GetLine(0).ExtentIncludingLineBreak.Span);
            var option = _markMap.GetMark(_localMarkC, vimBufferData);
            Assert.True(option.IsSome());
            var point = option.Value;
            Assert.Equal(0, point.Position.Position);
        }

        /// <summary>
        /// Deleting the line the mark is on should cause the mark to be invalidated
        /// </summary>
        [Fact]
        public void Track_DeleteLine()
        {
            var vimBufferData = CreateVimBufferData("cat", "dog");
            _markMap.SetMark(_localMarkC, vimBufferData, 1, 0);
            var span = new SnapshotSpan(
                vimBufferData.TextBuffer.GetLine(0).End,
                vimBufferData.TextBuffer.GetLine(1).EndIncludingLineBreak);
            vimBufferData.TextBuffer.Delete(span.Span);
            var option = _markMap.GetMark(_localMarkC, vimBufferData);
            Assert.True(option.IsNone());
        }

        /// <summary>
        /// Clearing out all global marks should work on an empty map
        /// </summary>
        [Fact]
        public void ClearGlobalMarks_Empty()
        {
            _markMap.Clear();
        }

        /// <summary>
        /// Clearing out the global marks shouldn't affect any local marks
        /// </summary>
        [Fact]
        public void ClearGlobalMarks_NoAffectOnLocal()
        {
            var vimBufferData = CreateVimBufferData("hello world");
            _markMap.SetMark(_localMarkD, vimBufferData, 0, 1);
            _markMap.Clear();
            Assert.True(_markMap.GetMark(_localMarkD, vimBufferData).IsSome());
        }

        [Fact]
        public void LocalMark_BackAndForth()
        {
            foreach (var localMark in LocalMark.All)
            {
                var c = localMark.Char;
                var otherLocalMark = LocalMark.OfChar(c).Value;
                Assert.Equal(localMark, otherLocalMark);
            }
        }

        [Fact]
        public void Mark_BackAndForth()
        {
            var all = new List<Mark>();
            all.AddRange(LocalMark.All.Select(Mark.NewLocalMark));
            all.AddRange(Letter.All.Select(Mark.NewGlobalMark));
            all.Add(Mark.LastJump);

            foreach (var mark in all)
            {
                var c = mark.Char;
                var otherMark = Mark.OfChar(c).Value;
                Assert.Equal(otherMark, mark);
            }
        }
    }
}
