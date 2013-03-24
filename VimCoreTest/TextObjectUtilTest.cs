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
            /// A Empty line is a complete sentence so the EndLine value is the end of the sentence
            /// </summary>
            [Fact]
            public void EmptyLine()
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
            private bool IsSentenceStartOnly(SnapshotPoint point)
            {
                var column = point.GetColumn();
                return _textObjectUtil.IsSentenceStartOnly(SentenceKind.Default, column);
            }

            [Fact]
            public void AfterTrailingChars()
            {
                Create("a?)]' b.");
                Assert.True(IsSentenceStartOnly(_textBuffer.GetPoint(6)));
            }

            /// <summary>
            /// Make sure we don't report the second char as the start due to a math error
            /// </summary>
            [Fact]
            public void SecondChar()
            {
                Create("dog. cat");
                Assert.True(IsSentenceStartOnly(_textBuffer.GetPoint(0)));
                Assert.False(IsSentenceStartOnly(_textBuffer.GetPoint(1)));
            }

            /// <summary>
            /// A Empty line is a sentence start
            /// </summary>
            [Fact]
            public void EmptyLine()
            {
                Create("dog.  ", "", "");
                Assert.True(IsSentenceStartOnly(_textBuffer.GetPointInLine(1, 0)));
            }

            /// <summary>
            /// The second Empty line isn't a sentence start
            /// </summary>
            [Fact]
            public void DoubleEmptyLine()
            {
                Create("d. ", "", "");
                Assert.False(IsSentenceStartOnly(_textBuffer.GetPointInLine(2, 0)));
            }

            [Fact]
            public void AfterDoubleEmptyLine()
            {
                Create("a", "", "", "b");
                Assert.True(IsSentenceStartOnly(_textBuffer.GetPointInLine(3, 0)));
            }

            [Fact]
            public void StartOfLineAfterSentence()
            {
                Create("a!", "b.");
                Assert.True(IsSentenceStartOnly(_textBuffer.GetPointInLine(1, 0)));
            }
        }

        public class IsSentenceStartTest : TextObjectUtilTest
        {
            [Fact]
            public void DoubleEmptyLine()
            {
                Create("a", "", "", "b");
                Assert.True(_textObjectUtil.IsSentenceStart(SentenceKind.Default, _textBuffer.GetPointInLine(1, 0).GetColumn()));
                Assert.False(_textObjectUtil.IsSentenceStart(SentenceKind.Default, _textBuffer.GetPointInLine(2, 0).GetColumn()));
            }

            [Fact]
            public void AfterDoubleEmptyLine()
            {
                Create("a", "", "", "b");
                Assert.True(_textObjectUtil.IsSentenceStart(SentenceKind.Default, _textBuffer.GetPointInLine(3, 0).GetColumn()));
            }
        }
    }
}
