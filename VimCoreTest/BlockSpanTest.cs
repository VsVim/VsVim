using EditorUtils;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;

namespace Vim.UnitTest
{
    [TestFixture]
    public sealed class BlockSpanTest : VimTestBase
    {
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
        }

        /// <summary>
        /// Make sure the end point is correct for a single line BlockSpanData
        /// </summary>
        [Test]
        public void EndPoint_SingleLine()
        {
            Create("cat", "dog");
            var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), 2, 1);
            Assert.AreEqual(_textBuffer.GetLine(0).Start.Add(2), blockSpan.End);
        }

        /// <summary>
        /// Make sure the end point is correct for a multiline BlockSpanData
        /// </summary>
        [Test]
        public void EndPoint_MultiLine()
        {
            Create("cat", "dog", "fish");
            var blockSpan = new BlockSpan(_textBuffer.GetPoint(0), 2, 2);
            Assert.AreEqual(_textBuffer.GetLine(1).Start.Add(2), blockSpan.End);
        }

        /// <summary>
        /// Make sure operator equality functions as expected
        /// </summary>
        [Test]
        public void Equality_Operator()
        {
            Create("cat", "dog");
            EqualityUtil.RunAll(
                (left, right) => left == right,
                (left, right) => left != right,
                false,
                false,
                EqualityUnit.Create(new BlockSpan(_textBuffer.GetPoint(0), 2, 2))
                    .WithEqualValues(new BlockSpan(_textBuffer.GetPoint(0), 2, 2))
                    .WithNotEqualValues(
                        new BlockSpan(_textBuffer.GetPoint(1), 2, 2),
                        new BlockSpan(_textBuffer.GetPoint(1), 2, 3)));
        }
    }
}
