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
    public abstract class WordUtilTest : VimTestBase
    {
        private readonly ITextBuffer _textBuffer;
        private readonly ITextView _textView;
        private readonly WordUtil _wordUtil;
        private readonly IVimLocalSettings _localSettings;

        protected WordUtilTest()
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

        public sealed class GetWordSpansTest : WordUtilTest
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
            [InlineData("dog ca$$t $b", new[] { "dog", "ca", "$$", "t", "$", "b" })]
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

            [WpfTheory]
            [InlineData("dog cat", 0, true, new[] { "dog", "cat" })]
            [InlineData("dog cat", 1, true, new[] { "dog", "cat" })]
            [InlineData("dog cat", 2, true, new[] { "dog", "cat" })]
            [InlineData("dog cat", 5, false, new[] { "cat", "dog" })]
            [InlineData("dog cat", 4, false, new[] { "dog" })]
            [InlineData("dog\n\ncat", 6, false, new[] { "cat", "\n", "dog" })]
            public void NormalIndex(string text, int index, bool isForward, string[] expected)
            {
                _textBuffer.SetTextContent(text);
                var searchPath = isForward ? SearchPath.Forward : SearchPath.Backward;
                var bufferWords = _wordUtil.GetWordSpans(WordKind.NormalWord, searchPath, _textBuffer.GetPoint(index)).Select(x => x.GetText());
                Assert.Equal(expected, bufferWords);
            }
        }

        public sealed class GetFullWordSpanTest : WordUtilTest
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
    }
}
