using NUnit.Framework;
using Vim.Extensions;
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

        [Test]
        public void CurrentChar_WordStart()
        {
            Create("hello world");
            Assert.AreEqual('h', _tokenizer.CurrentChar.Value);
        }

        /// <summary>
        /// CurrentChar should work for all token types
        /// </summary>
        [Test]
        public void CurrentChar_Digit()
        {
            Create("42 world");
            Assert.AreEqual('4', _tokenizer.CurrentChar.Value);
        }

        /// <summary>
        /// There is no CurrentChar when we are at the end of the line
        /// </summary>
        [Test]
        public void CurrentChar_EndOfLine()
        {
            Create("hello");
            _tokenizer.MoveNextToken();
            Assert.IsTrue(_tokenizer.CurrentChar.IsNone());
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
        /// If the line starts with a " then the current token should be the end of the line
        /// </summary>
        [Test]
        public void InitialState_AtEndOfLine()
        {
            Create(@"""hello world");
            Assert.IsTrue(_tokenizer.CurrentTokenKind.IsEndOfLine);
        }

        /// <summary>
        /// Need to go to the first token on initialization
        /// </summary>
        [Test]
        public void InitialState_Number()
        {
            Create(@"42 hello");
            Assert.IsTrue(_tokenizer.CurrentTokenKind.IsNumber);
        }

        /// <summary>
        /// Make sure it advances past the first character and rebuilds a new Word
        /// </summary>
        [Test]
        public void MoveNextChar_Word()
        {
            Create("cat dog");
            _tokenizer.MoveNextChar();
            Assert.IsTrue(_tokenizer.CurrentTokenKind.IsWord);
            Assert.AreEqual("at", _tokenizer.CurrentToken.TokenText);
        }

        /// <summary>
        /// The MoveNextChar should rebuild the token based off of the new index even if 
        /// it's a new token kind
        /// </summary>
        [Test]
        public void MoveNextChar_TokenChange()
        {
            Create("a dog");
            _tokenizer.MoveNextChar();
            Assert.IsTrue(_tokenizer.CurrentTokenKind.IsBlank);
            Assert.AreEqual(" ", _tokenizer.CurrentToken.TokenText);
        }

        /// <summary>
        /// Make sure it advances past the first character and rebuilds a digit
        /// </summary>
        [Test]
        public void MoveNextChar_Number()
        {
            Create("42 dog");
            _tokenizer.MoveNextChar();
            Assert.IsTrue(_tokenizer.CurrentTokenKind.IsNumber);
            Assert.AreEqual("2", _tokenizer.CurrentToken.TokenText);
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
