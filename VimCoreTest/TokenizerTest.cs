using NUnit.Framework;
using Vim.Interpreter;

namespace Vim.UnitTest
{
    [TestFixture]
    public sealed class TokenizerTest
    {
        private Tokenizer _tokenizer;

        public void Create(string line)
        {
            _tokenizer = new Tokenizer(line);
        }

        /// <summary>
        /// The specified index should be length of text when the tokenizer is at the end of the line
        /// </summary>
        [Test]
        public void Index_EndOfLine()
        {
            Create("bird");
            _tokenizer.MoveNextToken();
            Assert.AreEqual(4, _tokenizer.Index);
        }

        /// <summary>
        /// Rewind to the end of the line should put you back at the end of the line
        /// </summary>
        [Test]
        public void Rewind_EndOfLine()
        {
            Create("bird");
            _tokenizer.MoveNextToken();
            Assert.IsTrue(_tokenizer.CurrentTokenKind.IsEndOfLine);
            _tokenizer.Rewind(_tokenizer.Index);
            Assert.IsTrue(_tokenizer.CurrentTokenKind.IsEndOfLine);
        }
    }
}
