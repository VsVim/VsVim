using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class BlockSpanTest
    {
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateTextBuffer(lines);
        }

        /// <summary>
        /// Make sure the end point is correct for a single line BlockSpanData
        /// </summary>
        [Test]
        public void EndPoint_SingleLine()
        {
            Create("cat", "dog");
            var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), 2, 1);
            Assert.AreEqual(_textBuffer.GetLine(0).Start.Add(2), blockSpan.EndPoint);
        }

        /// <summary>
        /// Make sure the end point is correct for a multiline BlockSpanData
        /// </summary>
        [Test]
        public void EndPoint_MultiLine()
        {
            Create("cat", "dog", "fish");
            var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), 2, 2);
            Assert.AreEqual(_textBuffer.GetLine(1).Start.Add(2), blockSpan.EndPoint);
        }
    }
}
