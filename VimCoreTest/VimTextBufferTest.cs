using EditorUtils;
using Microsoft.VisualStudio.Text;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
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
        [Fact]
        public void GetLocalMark_NotSet()
        {
            Create("");
            Assert.True(_vimTextBuffer.GetLocalMark(_localMarkA).IsNone());
        }

        /// <summary>
        /// Sanity check to ensure we can get and set a local mark 
        /// </summary>
        [Fact]
        public void SetLocalMark_FirstLine()
        {
            Create("hello world");
            Assert.True(_vimTextBuffer.SetLocalMark(_localMarkA, 0, 1));
            Assert.Equal(1, _vimTextBuffer.GetLocalMark(_localMarkA).Value.Position.Position);
        }

        /// <summary>
        /// Sanity check to ensure we can get and set a local mark 
        /// </summary>
        [Fact]
        public void SetLocalMark_SecondLine()
        {
            Create("hello", "world");
            Assert.True(_vimTextBuffer.SetLocalMark(_localMarkA, 1, 1));
            Assert.Equal(_textBuffer.GetLine(1).Start.Add(1).Position, _vimTextBuffer.GetLocalMark(_localMarkA).Value.Position.Position);
        }

        /// <summary>
        /// Attempting to set a read only mark should return false and not update the mark
        /// </summary>
        [Fact]
        public void SetLocalMark_ReadOnlyMark()
        {
            Create("hello", "world");
            var visualSpan = VisualSpan.NewCharacter(new CharacterSpan(_textBuffer.GetPoint(0), 1, 2));
            _vimTextBuffer.LastVisualSelection = FSharpOption.Create(VisualSelection.CreateForward(visualSpan));
            Assert.False(_vimTextBuffer.SetLocalMark(LocalMark.LastSelectionStart, 0, 4));
            Assert.Equal(0, _vimTextBuffer.GetLocalMark(LocalMark.LastSelectionStart).Value.Position.Position);
        }
    }
}
