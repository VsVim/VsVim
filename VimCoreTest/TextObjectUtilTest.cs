using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Xunit;
using EditorUtils;

namespace Vim.UnitTest
{
    public abstract class TextObjectUtilTest : VimTestBase
    {
        private ITextBuffer _textBuffer;
        private TextObjectUtil _textObjectUtil;

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
            _textObjectUtil = new TextObjectUtil(Vim.GlobalSettings, _textBuffer);
        }

        public sealed class IsSentenceEndTest : TextObjectUtilTest
        {
            /// <summary>
            /// A single space after a '.' should make the '.' the sentence end
            /// </summary>
            [Fact]
            public void SingleSpace()
            {
                Create("a!b. c");
                Assert.True(_textObjectUtil.IsSentenceEnd(SentenceKind.Default, _textBuffer.GetColumn(4)));
            }

            /// <summary>
            /// The last portion of many trailing characters is the end of a sentence
            /// </summary>
            [Fact]
            public void ManyTrailingCharacters()
            {
                Create("a?)]' b.");
                Assert.True(_textObjectUtil.IsSentenceEnd(SentenceKind.Default, _textBuffer.GetColumn(5)));
            }

            /// <summary>
            /// Don't report the start of a buffer as being the end of a sentence
            /// </summary>
            [Fact]
            public void StartOfBuffer()
            {
                Create("dog. cat");
                Assert.False(_textObjectUtil.IsSentenceEnd(SentenceKind.Default, _textBuffer.GetColumn(0)));
            }

            /// <summary>
            /// A blank line is a complete sentence so the EndLine value is the end of the sentence
            /// </summary>
            [Fact]
            public void BlankLine()
            {
                Create("dog", "", "bear");
                Assert.True(_textObjectUtil.IsSentenceEnd(SentenceKind.Default, _textBuffer.GetLine(1).Start.GetColumn()));
            }

            [Fact]
            public void Thorough()
            {
                Create("dog", "cat", "bear");
                for (var i = 1; i < _textBuffer.CurrentSnapshot.Length; i++)
                {
                    var point = _textBuffer.GetPoint(i);
                    var column = point.GetColumn();
                    var test = _textObjectUtil.IsSentenceEnd(SentenceKind.Default, column);
                    Assert.False(test);
                }
            }

        }

        public sealed class IsSentenceStartOnlyTest : TextObjectUtilTest
        {
            [Fact]
            public void AfterTrailingChars()
            {
                Create("a?)]' b.");
                Assert.True(_textObjectUtil.IsSentenceStartOnly(SentenceKind.Default, _textBuffer.GetPoint(6)));
            }

            /// <summary>
            /// Make sure we don't report the second char as the start due to a math error
            /// </summary>
            [Fact]
            public void SecondChar()
            {
                Create("dog. cat");
                Assert.True(_textObjectUtil.IsSentenceStartOnly(SentenceKind.Default, _textBuffer.GetPoint(0)));
                Assert.False(_textObjectUtil.IsSentenceStartOnly(SentenceKind.Default, _textBuffer.GetPoint(1)));
            }

            /// <summary>
            /// A blank line is a sentence start
            /// </summary>
            [Fact]
            public void BlankLine()
            {
                Create("dog.  ", "", "");
                Assert.True(_textObjectUtil.IsSentenceStart(SentenceKind.Default, _textBuffer.GetLine(1).Start.GetColumn()));
            }
        }
    }
}
