using System.Linq;
using Microsoft.VisualStudio.Text;
using Xunit;
using Vim.Extensions;
using System.Collections.Generic;

namespace Vim.UnitTest
{
    public sealed class TextUtilTest
    {
        private static IEnumerable<string> GetWords(WordKind wordKind, SearchPath searchPath, string text) =>
            TextUtil
            .GetWordSpans(wordKind, searchPath, text)
            .Select(span => text.Substring(span.Start, span.Length))
            .ToArray();

        [Theory]
        [InlineData("cat dog fish!", new[] { "cat", "dog", "fish", "!" })]
        [InlineData("cat123    dog", new[] { "cat123", "dog" })]
        [InlineData("cat123\tdog", new[] { "cat123", "dog" })]
        [InlineData("he$$o wor$d", new[] { "he", "$$", "o", "wor", "$", "d" })]
        [InlineData("!@#$ cat!!!", new[] { "!@#$", "cat", "!!!" })]
        [InlineData("!@#$ !!!cat", new[] { "!@#$", "!!!", "cat" })]
        [InlineData("$$$foo$$$", new[] { "$$$", "foo", "$$$" })]
        public void GetWordSpans_Normal(string text, string[] expected)
        {
            var wordsForward = GetWords(WordKind.NormalWord, SearchPath.Forward, text);
            Assert.Equal(expected, wordsForward);
            var wordsBackward = GetWords(WordKind.NormalWord, SearchPath.Forward, text).Reverse();
            Assert.Equal(expected.Reverse(), wordsBackward);
        }

        [Theory]
        [InlineData("cat dog fish!", new[] { "cat", "dog", "fish!" })]
        [InlineData("cat123    dog", new[] { "cat123", "dog" })]
        [InlineData("cat123\tdog", new[] { "cat123", "dog" })]
        [InlineData("he$$o wor$d", new[] { "he$$o", "wor$d" })]
        [InlineData("!@#$ cat!!!", new[] { "!@#$", "cat!!!" })]
        [InlineData("!@#$ !!!cat", new[] { "!@#$", "!!!cat" })]
        [InlineData("$$$foo$$$", new[] { "$$$foo$$$" })]
        public void GetWordSpans_Big(string text, string[] expected)
        {
            var wordsForward = GetWords(WordKind.BigWord, SearchPath.Forward, text);
            Assert.Equal(expected, wordsForward);
            var wordsBackward = GetWords(WordKind.BigWord, SearchPath.Forward, text).Reverse();
            Assert.Equal(expected.Reverse(), wordsBackward);
        }

        [Theory]
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
        public void GetFullWordSpan_Normal(string text, int index, string expected)
        {
            var span = TextUtil.GetFullWordSpan(WordKind.NormalWord, text, index).Value;
            var found = text.Substring(span.Start, span.Length);
            Assert.Equal(expected, found);
        }

        [Theory]
        [InlineData(" tree", 0)]
        [InlineData(" tree ", 5)]
        public void GetFullWordSpan_Normal_Invalid(string text, int index)
        {
            var option = TextUtil.GetFullWordSpan(WordKind.NormalWord, text, index);
            Assert.True(option.IsNone());
        }

        [Theory]
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
            var span = TextUtil.GetFullWordSpan(WordKind.BigWord, text, index).Value;
            Assert.Equal(expected, text.Substring(span));
        }

        [Theory]
        [InlineData("cat dog", 1, "cat")]
        [InlineData("cat dog", 3, "cat")]
        [InlineData("cat dog", 4, "cat")]
        [InlineData("cat dog", 5, "dog")]
        [InlineData("cat dog tree", 7, "dog")]
        [InlineData("cat! dog tree", 4, "!")]
        [InlineData("cat!!! dog tree", 6, "!!!")]
        public void GetPreviousWordSpan_Normal(string text, int index, string expected)
        {
            var span = TextUtil.GetPreviousWordSpan(WordKind.NormalWord, text, index).Value;
            Assert.Equal(expected, text.Substring(span));
        }

        [Theory]
        [InlineData("cat", 0)]
        [InlineData(" cat", 1)]
        [InlineData("\tcat", 1)]
        public void GetPreviousWordSpan_Normal_Invalid(string text, int index)
        {
            var option = TextUtil.GetPreviousWordSpan(WordKind.NormalWord, text, index);
            Assert.True(option.IsNone());
        }
    }
}
