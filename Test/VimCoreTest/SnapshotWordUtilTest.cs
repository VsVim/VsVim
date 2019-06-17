using System;
using System.Linq;
using Vim.EditorHost;
using Vim.Extensions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;
using System.Collections.Generic;

namespace Vim.UnitTest
{
    public abstract class SnapshotWordUtilTest : VimTestBase
    {
        private readonly ITextBuffer _textBuffer;
        private readonly ITextView _textView;
        private readonly WordUtil _wordUtil;
        private readonly IVimLocalSettings _localSettings;

        protected SnapshotWordUtilTest()
        {
            _textView = CreateTextView();
            _textBuffer = _textView.TextBuffer;
            _localSettings = new LocalSettings(Vim.GlobalSettings);
            _wordUtil = new WordUtil(_textBuffer, _localSettings);
        }

        private IEnumerable<string> GetWordsInText(WordKind wordKind, SearchPath searchPath, string text) =>
            _wordUtil
            .GetWordSpansInText(wordKind, searchPath, text)
            .Select(span => text.Substring(span.Start, span.Length))
            .ToArray();

        public sealed class GetWordSpansTest : SnapshotWordUtilTest
        {
            private void AssertWordSpans(WordKind wordKind, string text, string[] expected)
            {
                var textWordsForward = GetWordsInText(wordKind, SearchPath.Forward, text);
                Assert.Equal(expected, textWordsForward);
                var textWordsBackward = GetWordsInText(wordKind, SearchPath.Forward, text).Reverse();
                Assert.Equal(expected.Reverse(), textWordsBackward);
                _textBuffer.SetTextContent(text);
                var bufferWordsForward = _wordUtil.GetWordSpans(wordKind, SearchPath.Forward, _textBuffer.GetStartPoint()).Select(x => x.GetText());
                Assert.Equal(expected, bufferWordsForward);
                var bufferWordsBackward = _wordUtil.GetWordSpans(wordKind, SearchPath.Backward, _textBuffer.GetEndPoint()).Select(x => x.GetText());
                Assert.Equal(expected.Reverse(), bufferWordsBackward);
            }

            [WpfTheory]
            [InlineData("cat dog fish!", new[] { "cat", "dog", "fish", "!" })]
            [InlineData("cat123    dog", new[] { "cat123", "dog" })]
            [InlineData("cat123\tdog", new[] { "cat123", "dog" })]
            [InlineData("he$$o wor$d", new[] { "he", "$$", "o", "wor", "$", "d" })]
            [InlineData("!@#$ cat!!!", new[] { "!@#$", "cat", "!!!" })]
            [InlineData("!@#$ !!!cat", new[] { "!@#$", "!!!", "cat" })]
            [InlineData("$$$foo$$$", new[] { "$$$", "foo", "$$$" })]
            [InlineData("cat\ndog", new[] { "cat", "dog" })]
            [InlineData("cat\n\ndog", new[] { "cat", "\n", "dog" })]
            [InlineData("cat\n\n\ndog", new[] { "cat", "\n", "\n", "dog" })]
            [InlineData("cat\r\n\r\n\r\ndog", new[] { "cat", "\r\n", "\r\n", "dog" })]
            [InlineData("cat\r\ndog", new[] { "cat", "dog" })]
            public void Normal(string text, string[] expected)
            {
                AssertWordSpans(WordKind.NormalWord, text, expected);
            }

            [WpfTheory]
            [InlineData("cat dog fish!", new[] { "cat", "dog", "fish!" })]
            [InlineData("cat123    dog", new[] { "cat123", "dog" })]
            [InlineData("cat123\tdog", new[] { "cat123", "dog" })]
            [InlineData("he$$o wor$d", new[] { "he$$o", "wor$d" })]
            [InlineData("!@#$ cat!!!", new[] { "!@#$", "cat!!!" })]
            [InlineData("!@#$ !!!cat", new[] { "!@#$", "!!!cat" })]
            [InlineData("$$$foo$$$", new[] { "$$$foo$$$" })]
            public void Big(string text, string[] expected)
            {
                AssertWordSpans(WordKind.BigWord, text, expected);
            }

            [WpfTheory]
            [InlineData("c,a", "cat", new[] { "ca", "t" })]
            [InlineData("a-c", "abcd", new[] { "abc", "d" })]
            [InlineData("@,-", "cat-dog fish-tree", new[] { "cat-dog", "fish-tree" })]
            public void CustomNormal(string iskeyword, string text, string[] expected)
            {
                _localSettings.IsKeyword = iskeyword;
                AssertWordSpans(WordKind.NormalWord, text, expected);
            }

            [WpfTheory]
            [InlineData("c,a", "cat", new[] { "cat" })]
            public void CustomBig(string iskeyword, string text, string[] expected)
            {
                _localSettings.IsKeyword = iskeyword;
                AssertWordSpans(WordKind.BigWord, text, expected);
            }
        }

        public sealed class GetFullWordSpanTest : SnapshotWordUtilTest
        {
            private void AssertFullWord(WordKind wordKind, string text, int index, string expected)
            {
                var span = _wordUtil.GetFullWordSpanInText(wordKind, text, index).Value;
                var found = text.Substring(span.Start, span.Length);
                Assert.Equal(expected, found);

                _textBuffer.SetTextContent(text);
                var snapshotSpan = _wordUtil.GetFullWordSpan(wordKind, _textBuffer.GetPoint(index)).Value;
                Assert.Equal(expected, snapshotSpan.GetText());
            }

            [WpfTheory]
            [InlineData("tree", 0, "tree")]
            [InlineData("tree", 1, "tree")]
            [InlineData("tree", 2, "tree")]
            [InlineData("$$tree", 0, "$$")]
            [InlineData("$$tree", 1, "$$")]
            [InlineData("tree$$", 0, "tree")]
            [InlineData("tree$$", 4, "$$")]
            [InlineData("!@#$", 0, "!@#$")]
            [InlineData("tree_123", 0, "tree_123")]
            [InlineData("tree_123", 1, "tree_123")]
            [InlineData("tree_123", 4, "tree_123")]
            public void GetFullWordSpan_Normal(string text, int index, string expected)
            {
                AssertFullWord(WordKind.NormalWord, text, index, expected);
            }

            [WpfTheory]
            [InlineData(" tree", 0)]
            [InlineData(" tree ", 5)]
            public void GetFullWordSpan_Normal_Invalid(string text, int index)
            {
                var option = _wordUtil.GetFullWordSpanInText(WordKind.NormalWord, text, index);
                Assert.True(option.IsNone());
            }

            [WpfTheory]
            [InlineData("tree", 0, "tree")]
            [InlineData("tree", 1, "tree")]
            [InlineData("tree", 2, "tree")]
            [InlineData("$$tree", 0, "$$tree")]
            [InlineData("$$tree", 1, "$$tree")]
            [InlineData("tree$$", 0, "tree$$")]
            [InlineData("tree$$", 4, "tree$$")]
            [InlineData("!@#$", 0, "!@#$")]
            [InlineData("tree_123", 0, "tree_123")]
            [InlineData("tree_123", 1, "tree_123")]
            public void GetFullWordSpan_Big(string text, int index, string expected)
            {
                AssertFullWord(WordKind.BigWord, text, index, expected);
            }
        }

        public sealed class MiscTest : SnapshotWordUtilTest
        {
            /// <summary>
            /// Break up the buffer into simple words
            /// </summary>
            [WpfFact]
            public void GetWords_Normal()
            {
                _textBuffer.SetText("dog ca$$t $b");
                var ret = _wordUtil.GetWordSpans(WordKind.NormalWord, SearchPath.Forward, _textBuffer.GetPoint(0));
                Assert.Equal(
                    new[] { "dog", "ca", "$$", "t", "$", "b" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// A blank line should be a word 
            /// </summary>
            [WpfFact]
            public void GetWords_BlankLine()
            {
                _textBuffer.SetText("dog cat", "", "bear");
                var ret = _wordUtil.GetWordSpans(WordKind.NormalWord, SearchPath.Forward, _textBuffer.GetPoint(0));
                Assert.Equal(
                    new[] { "dog", "cat", Environment.NewLine, "bear" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// From the middle of a word should return the span of the entire word
            /// </summary>
            [WpfFact]
            public void GetWords_FromMiddleOfWord()
            {
                _textBuffer.SetText("dog cat");
                var ret = _wordUtil.GetWordSpans(WordKind.NormalWord, SearchPath.Forward, _textBuffer.GetPoint(1));
                Assert.Equal(
                    new[] { "dog", "cat" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// From the end of a word should return the span of the entire word
            /// </summary>
            [WpfFact]
            public void GetWords_FromEndOfWord()
            {
                _textBuffer.SetText("dog cat");
                var ret = _wordUtil.GetWordSpans(WordKind.NormalWord, SearchPath.Forward, _textBuffer.GetPoint(2));
                Assert.Equal(
                    new[] { "dog", "cat" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// From the middle of a word backward
            /// </summary>
            [WpfFact]
            public void GetWords_BackwardFromMiddle()
            {
                _textBuffer.SetText("dog cat");
                var ret = _wordUtil.GetWordSpans(WordKind.NormalWord, SearchPath.Backward, _textBuffer.GetPoint(5));
                Assert.Equal(
                    new[] { "cat", "dog" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// From the start of a word backward should not include that particular word 
            /// </summary>
            [WpfFact]
            public void GetWords_BackwardFromStart()
            {
                _textBuffer.SetText("dog cat");
                var ret = _wordUtil.GetWordSpans(WordKind.NormalWord, SearchPath.Backward, _textBuffer.GetPoint(4));
                Assert.Equal(
                    new[] { "dog" },
                    ret.Select(x => x.GetText()).ToList());
            }

            /// <summary>
            /// Make sure that blank lines are counted as words when
            /// </summary>
            [WpfFact]
            public void GetWords_BackwardBlankLine()
            {
                _textBuffer.SetText("dog", "", "cat");
                var ret = _wordUtil.GetWordSpans(WordKind.NormalWord, SearchPath.Backward, _textBuffer.GetLine(2).Start.Add(1));
                Assert.Equal(
                    new[] { "cat", Environment.NewLine, "dog" },
                    ret.Select(x => x.GetText()).ToList());
            }
        }
    }
}
