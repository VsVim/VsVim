using Vim.Extensions;
using Vim.Interpreter;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class TokenizerTest
    {
        private Tokenizer _tokenizer;

        public void Create(string line)
        {
            _tokenizer = new Tokenizer(line);
        }

        [Fact]
        public void CurrentChar_WordStart()
        {
            Create("hello world");
            Assert.Equal('h', _tokenizer.CurrentChar.Value);
        }

        /// <summary>
        /// CurrentChar should work for all token types
        /// </summary>
        [Fact]
        public void CurrentChar_Digit()
        {
            Create("42 world");
            Assert.Equal('4', _tokenizer.CurrentChar.Value);
        }

        /// <summary>
        /// There is no CurrentChar when we are at the end of the line
        /// </summary>
        [Fact]
        public void CurrentChar_EndOfLine()
        {
            Create("hello");
            _tokenizer.MoveNextToken();
            Assert.True(_tokenizer.CurrentChar.IsNone());
        }

        /// <summary>
        /// The specified index should be length of text when the tokenizer is at the end of the line
        /// </summary>
        [Fact]
        public void Index_EndOfLine()
        {
            Create("bird");
            _tokenizer.MoveNextToken();
            Assert.Equal(4, _tokenizer.Index);
        }

        /// <summary>
        /// If the line starts with a " then the current token should be the end of the line
        /// </summary>
        [Fact]
        public void InitialState_AtEndOfLine()
        {
            Create(@"""hello world");
            Assert.True(_tokenizer.CurrentTokenKind.IsEndOfLine);
        }

        /// <summary>
        /// Need to go to the first token on initialization
        /// </summary>
        [Fact]
        public void InitialState_Number()
        {
            Create(@"42 hello");
            Assert.True(_tokenizer.CurrentTokenKind.IsNumber);
        }

        /// <summary>
        /// Make sure it advances past the first character and rebuilds a new Word
        /// </summary>
        [Fact]
        public void MoveNextChar_Word()
        {
            Create("cat dog");
            _tokenizer.MoveNextChar();
            Assert.True(_tokenizer.CurrentTokenKind.IsWord);
            Assert.Equal("at", _tokenizer.CurrentToken.TokenText);
        }

        /// <summary>
        /// The MoveNextChar should rebuild the token based off of the new index even if 
        /// it's a new token kind
        /// </summary>
        [Fact]
        public void MoveNextChar_TokenChange()
        {
            Create("a dog");
            _tokenizer.MoveNextChar();
            Assert.True(_tokenizer.CurrentTokenKind.IsBlank);
            Assert.Equal(" ", _tokenizer.CurrentToken.TokenText);
        }

        /// <summary>
        /// Make sure it advances past the first character and rebuilds a digit
        /// </summary>
        [Fact]
        public void MoveNextChar_Number()
        {
            Create("42 dog");
            _tokenizer.MoveNextChar();
            Assert.True(_tokenizer.CurrentTokenKind.IsNumber);
            Assert.Equal("2", _tokenizer.CurrentToken.TokenText);
        }

        /// <summary>
        /// Rewind to the end of the line should put you back at the end of the line
        /// </summary>
        [Fact]
        public void MoveToIndex_EndOfLine()
        {
            Create("bird");
            _tokenizer.MoveNextToken();
            Assert.True(_tokenizer.CurrentTokenKind.IsEndOfLine);
            _tokenizer.MoveToIndex(_tokenizer.Index);
            Assert.True(_tokenizer.CurrentTokenKind.IsEndOfLine);
        }

        /// <summary>
        /// It's possible that we want to re-examine the end of line character should it
        /// end in a comment after we've already gotten there.  Useful when parsing out string
        /// constants
        /// </summary>
        [Fact]
        public void MoveToIndex_TokenizeEndOfLine()
        {
            Create(@"42 "" again");
            _tokenizer.MoveNextToken();
            _tokenizer.MoveNextToken();
            Assert.True(_tokenizer.IsAtEndOfLine);
            Assert.True(_tokenizer.CurrentChar.IsNone());
            _tokenizer.MoveToIndexEx(_tokenizer.Index, NextTokenFlags.AllowDoubleQuote);
            Assert.Equal('"', _tokenizer.CurrentChar.Value);
            Assert.True(_tokenizer.CurrentTokenKind.IsCharacter);
        }
    }
}
