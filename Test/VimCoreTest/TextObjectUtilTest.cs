using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Xunit;
using Vim.EditorHost;

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
            private bool IsSentenceEnd(SnapshotPoint point)
            {
                return _textObjectUtil.IsSentenceEnd(SentenceKind.Default, point.GetColumn());
            }

            /// <summary>
            /// A single space after a '.' should make the '.' the sentence end
            /// </summary>
            [WpfFact]
            public void SingleSpace()
            {
                Create("a!b. c");
                Assert.True(_textObjectUtil.IsSentenceEnd(SentenceKind.Default, _textBuffer.GetColumn(4)));
            }

            /// <summary>
            /// The last portion of many trailing characters is the end of a sentence
            /// </summary>
            [WpfFact]
            public void ManyTrailingCharacters()
            {
                Create("a?)]' b.");
                Assert.True(_textObjectUtil.IsSentenceEnd(SentenceKind.Default, _textBuffer.GetColumn(5)));
            }

            /// <summary>
            /// Don't report the start of a buffer as being the end of a sentence
            /// </summary>
            [WpfFact]
            public void StartOfBuffer()
            {
                Create("dog. cat");
                Assert.False(_textObjectUtil.IsSentenceEnd(SentenceKind.Default, _textBuffer.GetColumn(0)));
            }

            /// <summary>
            /// A Empty line is a complete sentence so the EndLine value is the end of the sentence
            /// </summary>
            [WpfFact]
            public void EmptyLine()
            {
                Create("dog", "", "bear");
                Assert.True(_textObjectUtil.IsSentenceEnd(SentenceKind.Default, _textBuffer.GetLine(1).Start.GetColumn()));
            }

            [WpfFact]
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

            /// <summary>
            /// A blank line is a sentence hence the first point after a blank line is the end of a
            /// sentence
            /// </summary>
            [WpfFact]
            public void BlankAfterBlank()
            {
                Create("", "  test");
                Assert.True(IsSentenceEnd(_textBuffer.GetPointInLine(1, 0)));
            }
        }

        public sealed class IsSentenceStartOnlyTest : TextObjectUtilTest
        {
            private bool IsSentenceStartOnly(SnapshotPoint point)
            {
                var column = point.GetColumn();
                return _textObjectUtil.IsSentenceStartOnly(SentenceKind.Default, column);
            }

            [WpfFact]
            public void AfterTrailingChars()
            {
                Create("a?)]' b.");
                Assert.True(IsSentenceStartOnly(_textBuffer.GetPoint(6)));
            }

            /// <summary>
            /// Make sure we don't report the second char as the start due to a math error
            /// </summary>
            [WpfFact]
            public void SecondChar()
            {
                Create("dog. cat");
                Assert.True(IsSentenceStartOnly(_textBuffer.GetPoint(0)));
                Assert.False(IsSentenceStartOnly(_textBuffer.GetPoint(1)));
            }

            /// <summary>
            /// A Empty line is a sentence start
            /// </summary>
            [WpfFact]
            public void EmptyLine()
            {
                Create("dog.  ", "", "");
                Assert.True(IsSentenceStartOnly(_textBuffer.GetPointInLine(1, 0)));
            }

            /// <summary>
            /// The second Empty line isn't a sentence start
            /// </summary>
            [WpfFact]
            public void DoubleEmptyLine()
            {
                Create("d. ", "", "");
                Assert.False(IsSentenceStartOnly(_textBuffer.GetPointInLine(2, 0)));
            }

            [WpfFact]
            public void AfterDoubleEmptyLine()
            {
                Create("a", "", "", "b");
                Assert.True(IsSentenceStartOnly(_textBuffer.GetPointInLine(3, 0)));
            }

            [WpfFact]
            public void StartOfLineAfterSentence()
            {
                Create("a!", "b.");
                Assert.True(IsSentenceStartOnly(_textBuffer.GetPointInLine(1, 0)));
            }
        }

        public class IsSentenceStartTest : TextObjectUtilTest
        {
            private bool IsSentenceStart(SnapshotPoint point)
            {
                var column = point.GetColumn();
                return _textObjectUtil.IsSentenceStart(SentenceKind.Default, column);
            }

            [WpfFact]
            public void DoubleEmptyLine()
            {
                Create("a", "", "", "b");
                Assert.True(IsSentenceStart(_textBuffer.GetPointInLine(1, 0)));
                Assert.False(IsSentenceStart(_textBuffer.GetPointInLine(2, 0)));
            }

            [WpfFact]
            public void AfterDoubleEmptyLine()
            {
                Create("a", "", "", "b");
                Assert.True(IsSentenceStart(_textBuffer.GetPointInLine(3, 0)));
            }

            [WpfFact]
            public void Complex()
            {
                Create(" f", "", " c", "", " d");
                Assert.True(IsSentenceStart(_textBuffer.GetPointInLine(2, 1)));
            }

            /// <summary>
            /// The 'test' here is the start of a sentence.  The first sentence in the ITextBuffer
            /// in fact
            /// </summary>
            [WpfFact]
            public void WhiteSpaceStartOfBuffer()
            {
                Create("  test");
                Assert.True(IsSentenceStart(_textBuffer.GetPointInLine(0, 2)));
            }

            [WpfFact]
            public void WhiteSpaceAfterEmptyLine()
            {
                Create("", "  test");
                Assert.True(IsSentenceStart(_textBuffer.GetPointInLine(1, 2)));
            }

            [WpfFact]
            public void EmptyAtStartOfBuffer()
            {
                Create("", "  test");
                Assert.True(IsSentenceStart(_textBuffer.GetPointInLine(0, 0)));
            }
        }

        public sealed class IsEmptyLineWithNoEmptyAboveTest : TextObjectUtilTest
        {
            private bool IsEmptyLineWithNoEmptyAbove(int lineNumber)
            {
                var line = _textBuffer.GetLine(lineNumber);
                return _textObjectUtil.IsEmptyLineWithNoEmptyAbove(line);
            }

            [WpfFact]
            public void FirstLineIsEmpty()
            {
                Create("", "  test");
                Assert.True(IsEmptyLineWithNoEmptyAbove(0));
                Assert.False(IsEmptyLineWithNoEmptyAbove(1));
            }

            [WpfFact]
            public void SecondLineIsEmpty()
            {
                Create("cat", "", "  test");
                Assert.True(IsEmptyLineWithNoEmptyAbove(1));
            }

            /// <summary>
            /// A blank line is not an empty one.  It must have length 0 
            /// </summary>
            [WpfFact]
            public void BlankLineIsNotEmpty()
            {
                Create("cat", " ");
                Assert.False(IsEmptyLineWithNoEmptyAbove(1));
            }

            [WpfFact]
            public void DoubleEmpty()
            {
                Create("", "", "cat");
                Assert.True(IsEmptyLineWithNoEmptyAbove(0));
                Assert.False(IsEmptyLineWithNoEmptyAbove(1));
            }

            [WpfFact]
            public void BlankAndEmptyLines()
            {
                Create("", " ", "", "cat");
                Assert.True(IsEmptyLineWithNoEmptyAbove(0));
                Assert.True(IsEmptyLineWithNoEmptyAbove(2));
            }
        }

        public sealed class IsSentenceWhiteSpaceTest : TextObjectUtilTest
        {
            private bool IsSentenceWhiteSpace(SnapshotPoint point)
            {
                return _textObjectUtil.IsSentenceWhiteSpace(SentenceKind.Default, point.GetColumn());
            }

            [WpfFact]
            public void Simple()
            {
                Create("cat. dog");
                Assert.True(IsSentenceWhiteSpace(_textBuffer.GetPoint(4)));
                Assert.False(IsSentenceWhiteSpace(_textBuffer.GetPoint(5)));
            }

            /// <summary>
            /// Ignore the spaces which occur in the middle of sentences
            /// </summary>
            [WpfFact]
            public void IgnoreWhiteSpaceInMiddle()
            {
                Create("cat dog.");
                Assert.False(IsSentenceWhiteSpace(_textBuffer.GetPoint(3)));
            }

            /// <summary>
            /// An empty line is a sentence and hence isn't white space between a sentence
            /// </summary>
            [WpfFact]
            public void EmptyLineIsNotWhiteSpace()
            {
                Create("d.", "", "c");
                Assert.False(IsSentenceWhiteSpace(_textBuffer.GetPointInLine(1, 0)));
            }
        }
    }
}
