using Vim.Extensions;
using Vim.Interpreter;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class TokenizerTest
    {
        internal Tokenizer _tokenizer;

        protected virtual void Create(string line)
        {
            _tokenizer = new Tokenizer(line, TokenizerFlags.None);
        }

        protected void AssertWord(string word)
        {
            Assert.True(_tokenizer.CurrentToken.TokenKind.IsWord);
            Assert.Equal(word, _tokenizer.CurrentToken.TokenText);
        }

        public sealed class CurrentCharTest : TokenizerTest
        {
            [Fact]
            public void WordStart()
            {
                Create("hello world");
                Assert.Equal('h', _tokenizer.CurrentChar);
            }

            /// <summary>
            /// CurrentChar should work for all token types
            /// </summary>
            [Fact]
            public void Digit()
            {
                Create("42 world");
                Assert.Equal('4', _tokenizer.CurrentChar);
            }

            /// <summary>
            /// There is no CurrentChar when we are at the end of the line
            /// </summary>
            [Fact]
            public void EndOfLine()
            {
                Create("hello");
                _tokenizer.MoveNextToken();
                Assert.True(_tokenizer.IsAtEndOfLine);
            }
        }

        public sealed class SkipBlanks : TokenizerTest
        {
            protected override void Create(string line)
            {
                base.Create(line);
                _tokenizer.TokenizerFlags = TokenizerFlags.SkipBlanks;
            }

            [Fact]
            public void Simple()
            {
                Create("cat dog");
                AssertWord("cat");
                _tokenizer.MoveNextToken();
                AssertWord("dog");
            }

            [Fact]
            public void SeveralBlanks()
            {
                Create("cat      dog");
                AssertWord("cat");
                _tokenizer.MoveNextToken();
                AssertWord("dog");
            }

            [Fact]
            public void ManyWords()
            {
                Create("cat dog fish rock");
                AssertWord("cat");
                _tokenizer.MoveNextToken();
                AssertWord("dog");
                _tokenizer.MoveNextToken();
                AssertWord("fish");
                _tokenizer.MoveNextToken();
                AssertWord("rock");
            }

            /// <summary>
            /// If the SkipBlanks option is specified and the text starts with blanks then
            /// the tokenizer should move over that blank
            /// </summary>
            [Fact]
            public void StartsWithBlank()
            {
                Create(" cat");
                AssertWord("cat");
            }
        }

        public sealed class ScopedFlagsTest : TokenizerTest
        {
            [Fact]
            public void DisposeReset()
            {
                Create("");
                var before = _tokenizer.TokenizerFlags;
                Assert.NotEqual(TokenizerFlags.AllowDoubleQuote, before);
                using (_tokenizer.SetTokenizerFlagsScoped(TokenizerFlags.AllowDoubleQuote))
                {
                    Assert.Equal(TokenizerFlags.AllowDoubleQuote, _tokenizer.TokenizerFlags);
                }
                Assert.Equal(before, _tokenizer.TokenizerFlags);
            }

            [Fact]
            public void ManualReset()
            {
                Create("");
                var before = _tokenizer.TokenizerFlags;
                Assert.NotEqual(TokenizerFlags.AllowDoubleQuote, before);
                using (var reset = _tokenizer.SetTokenizerFlagsScoped(TokenizerFlags.AllowDoubleQuote))
                {
                    Assert.Equal(TokenizerFlags.AllowDoubleQuote, _tokenizer.TokenizerFlags);
                    reset.Reset();
                    Assert.Equal(before, _tokenizer.TokenizerFlags);
                }
                Assert.Equal(before, _tokenizer.TokenizerFlags);
            }
        }

        public sealed class MiscTest : TokenizerTest
        {

            /// <summary>
            /// The specified index should be length of text when the tokenizer is at the end of the line
            /// </summary>
            [Fact]
            public void Index_EndOfLine()
            {
                Create("bird");
                _tokenizer.MoveNextToken();
                Assert.Equal(4, _tokenizer.Mark);
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
                _tokenizer.MoveToIndex(_tokenizer.Mark);
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
                Assert.Equal((char)0, _tokenizer.CurrentChar);
                _tokenizer.TokenizerFlags = TokenizerFlags.AllowDoubleQuote;
                Assert.Equal('"', _tokenizer.CurrentChar);
                Assert.True(_tokenizer.CurrentTokenKind.IsCharacter);
            }
        }
    }
}
