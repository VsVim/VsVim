using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class VimTextBufferTest : VimTestBase
    {
        private IVimTextBuffer _vimTextBuffer;
        private ITextBuffer _textBuffer;
        private LocalMark _localMarkA = LocalMark.NewLetter(Letter.A);

        private void Create(params string[] lines)
        {
            _vimTextBuffer = CreateVimTextBuffer(lines);
            _textBuffer = _vimTextBuffer.TextBuffer;
        }

        /// <summary>
        /// Requesting a LocalMark which isn't set should produce an empty option
        /// </summary>
        [Test]
        public void GetLocalMark_NotSet()
        {
            Create("");
            Assert.IsTrue(_vimTextBuffer.GetLocalMark(_localMarkA).IsNone());
        }

        /// <summary>
        /// Sanity check to ensure we can get and set a local mark 
        /// </summary>
        [Test]
        public void SetLocalMark_FirstLine()
        {
            Create("hello world");
            _vimTextBuffer.SetLocalMark(_localMarkA, 0, 1);
            Assert.AreEqual(1, _vimTextBuffer.GetLocalMark(_localMarkA).Value.Position.Position);
        }

        /// <summary>
        /// Sanity check to ensure we can get and set a local mark 
        /// </summary>
        [Test]
        public void SetLocalMark_SecondLine()
        {
            Create("hello", "world");
            _vimTextBuffer.SetLocalMark(_localMarkA, 1, 1);
            Assert.AreEqual(_textBuffer.GetLine(1).Start.Add(1).Position, _vimTextBuffer.GetLocalMark(_localMarkA).Value.Position.Position);
        }
    }
}
