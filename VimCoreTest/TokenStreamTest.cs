using Vim.Extensions;
using Vim.Interpreter;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class TokenStreamTest
    {
        internal TokenStream _tokenStream;

        protected void Create(string text)
        {
            _tokenStream = new TokenStream();
            _tokenStream.Reset(text);
        }

        public sealed class PeekCharTest : TokenStreamTest
        {
            [Fact]
            public void OneAhead()
            {
                Create("cat");
                Assert.Equal('a', _tokenStream.PeekChar(1).Value);
            }

            [Fact]
            public void TwoAhead()
            {
                Create("cat");
                Assert.Equal('t', _tokenStream.PeekChar(2).Value);
            }

            [Fact]
            public void EndOfText()
            {
                Create("cat");
                Assert.True(_tokenStream.PeekChar(3).IsNone());
            }

            [Fact]
            public void PastEndOfText()
            {
                Create("cat");
                Assert.True(_tokenStream.PeekChar(4).IsNone());
            }
        }

        public sealed class IsPeekCharTest : TokenStreamTest
        {
            [Fact]
            public void OneAhead()
            {
                Create("cat");
                Assert.True(_tokenStream.IsPeekChar('a'));
                Assert.False(_tokenStream.IsPeekChar('b'));
            }

            [Fact]
            public void EndOfText()
            {
                Create("cat");
                _tokenStream.Index = 2;
                Assert.False(_tokenStream.IsPeekChar('a'));
            }

            [Fact]
            public void PastEndOfText()
            {
                Create("");
                Assert.False(_tokenStream.IsPeekChar('a'));
            }
        }

        public sealed class CurrentCharTest : TokenStreamTest
        {
            [Fact]
            public void Simple()
            {
                const string text = "cat";
                Create(text);
                for (int i = 0; i < text.Length; i++)
                {
                    Assert.Equal(text[i], _tokenStream.CurrentChar.Value);
                    _tokenStream.IncrementIndex();
                }
            }

            [Fact]
            public void EndOfText()
            {
                Create("cat");
                _tokenStream.Index = 3;
                Assert.True(_tokenStream.CurrentChar.IsNone());
            }

            [Fact]
            public void PastEndOfText()
            {
                Create("cat");
                _tokenStream.Index = 4;
                Assert.True(_tokenStream.CurrentChar.IsNone());
            }
        }
    }
}
