using System.Linq;
using Microsoft.VisualStudio.Text;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    /// <summary>
    /// Summary description for TextUtilTest
    /// </summary>
    public class TextUtilTest
    {
        private void AssertWordSpans(string input, WordKind kind, params string[] expected)
        {
            var wordsForward = TextUtil.GetWordSpans(kind, Path.Forward, input).Select(x => input.Substring(x.Start, x.Length)).ToList();
            Assert.Equal(
                expected,
                wordsForward);
            var wordsBackward = TextUtil.GetWordSpans(kind, Path.Backward, input).Select(x => input.Substring(x.Start, x.Length)).ToList();
            Assert.Equal(
                expected.Reverse(),
                wordsBackward);
        }

        private string FindCurrentNormalWord(string input, int index)
        {
            var span = TextUtil.FindCurrentWordSpan(WordKind.NormalWord, input, index);
            Assert.True(span.IsSome());
            return input.Substring(span.Value.Start, span.Value.Length);
        }

        private string FindCurrentBigWord(string input, int index)
        {
            var span = TextUtil.FindCurrentWordSpan(WordKind.BigWord, input, index);
            Assert.True(span.IsSome());
            return input.Substring(span.Value.Start, span.Value.Length);
        }

        /// <summary>
        /// Simple test of getting word spans
        /// </summary>
        [Fact]
        public void GetWordSpans_Simple()
        {
            AssertWordSpans("foo bar baz!", WordKind.NormalWord, "foo", "bar", "baz", "!");
        }

        /// <summary>
        /// Put some digits and underscores into the words
        /// </summary>
        [Fact]
        public void GetWordSpans_WithDigitsAndUnderscores()
        {
            AssertWordSpans("h42o wo_orld", WordKind.NormalWord, "h42o", "wo_orld");
        }

        /// <summary>
        /// Put some non-word characters into the middle of the words
        /// </summary>
        [Fact]
        public void GetWordSpans_NonWordCharacters()
        {
            AssertWordSpans("he$$o wor$d", WordKind.NormalWord, "he", "$$", "o", "wor", "$", "d");
            AssertWordSpans("!@#$ cat!!!", WordKind.NormalWord, "!@#$", "cat", "!!!");
            AssertWordSpans("!@#$ !!!cat", WordKind.NormalWord, "!@#$", "!!!", "cat");
            AssertWordSpans("#$foo!@#$", WordKind.NormalWord, "#$", "foo", "!@#$");
        }

        /// <summary>
        /// Big words only care about non-blanks
        /// </summary>
        [Fact]
        public void GetWordSpans_BigSimple()
        {
            AssertWordSpans("he$$o wor$d", WordKind.BigWord, "he$$o", "wor$d");
            AssertWordSpans("!@#$ cat!!!", WordKind.BigWord, "!@#$", "cat!!!");
            AssertWordSpans("!@#$ !!!cat", WordKind.BigWord, "!@#$", "!!!cat");
            AssertWordSpans("#$foo!@#$", WordKind.BigWord, "#$foo!@#$");
        }

        /// <summary>
        /// Basic tests
        /// </summary>
        [Fact]
        public void FindWord1()
        {
            Assert.Equal("foo", FindCurrentNormalWord("foo ", 0));
            Assert.Equal("foo_123", FindCurrentNormalWord("foo_123", 0));
        }

        /// <summary>
        /// Non-zero index tests
        /// </summary>
        [Fact]
        public void FindWord2()
        {
            Assert.Equal("oo", FindCurrentNormalWord("foo", 1));
            Assert.Equal("oo123", FindCurrentNormalWord("foo123", 1));
        }

        /// <summary>
        /// Limits
        /// </summary>
        [Fact]
        public void FindWord3()
        {
            var span = TextUtil.FindCurrentWordSpan(WordKind.NormalWord, " foo", 0);
            Assert.True(span.IsNone());

            Assert.Equal("oo_", FindCurrentNormalWord("foo_", 1));

            span = TextUtil.FindCurrentWordSpan(WordKind.NormalWord, "foo", 23);
            Assert.True(span.IsNone());
        }

        /// <summary>
        /// Non-keyword words
        /// </summary>
        [Fact]
        public void FindWord4()
        {
            Assert.Equal("!@#$", FindCurrentNormalWord("!@#$", 0));
            Assert.Equal("!!!", FindCurrentNormalWord("foo!!!", 3));
        }

        /// <summary>
        /// Mix of keyword and non-keyword strings
        /// </summary>
        [Fact]
        public void FindWord5()
        {
            Assert.Equal("#$", FindCurrentNormalWord("#$foo", 0));
            Assert.Equal("foo", FindCurrentNormalWord("foo!@#$", 0));
        }

        [Fact]
        public void FindBigWord1()
        {
            Assert.Equal("foo!@#$", FindCurrentBigWord("foo!@#$", 0));
            Assert.Equal("!foo!", FindCurrentBigWord("!foo!", 0));
        }

        /// <summary>
        /// Make sure that FindFullWordSpan words the span for any index into the
        /// word
        /// </summary>
        [Fact]
        public void FindFullWordSpan_Normal_FromAllParts()
        {
            var word = "foo";
            for (var i = 0; i < word.Length; i++)
            {
                var span = TextUtil.FindFullWordSpan(WordKind.NormalWord, word, i);
                Assert.True(span.IsSome());
                Assert.Equal(new Span(0, 3), span.Value);
            }
        }

        /// <summary>
        /// Make sure that FindFullWordSpan words the span for any index into the
        /// word on big words
        /// </summary>
        [Fact]
        public void FindFullWordSpan_Big_FromAllParts()
        {
            var word = "foo_123";
            var text = word + " and some extra";
            for (var i = 0; i < word.Length; i++)
            {
                var span = TextUtil.FindFullWordSpan(WordKind.NormalWord, text, i);
                Assert.True(span.IsSome());
                Assert.Equal(word, text.Substring(span.Value.Start, span.Value.Length));
            }
        }

        /// <summary>
        /// Another test to ensure that FindFullWordSpan works with words that are 
        /// entirely composed of symbols
        /// </summary>
        [Fact]
        public void FindFullBigWord_Big_FromAllParts2()
        {
            var word = "!@#";
            for (var i = 0; i < word.Length; i++)
            {
                var span = TextUtil.FindFullWordSpan(WordKind.NormalWord, word, i);
                Assert.True(span.IsSome());
                Assert.Equal(new Span(0, 3), span.Value);
            }
        }

        /// <summary>
        /// Ensure that FindFullWordSpan works from the middle of the string 
        /// </summary>
        [Fact]
        public void FindFullWordSpan_Normal_MiddleOfString()
        {
            var text = "cat dog";
            var span = TextUtil.FindFullWordSpan(WordKind.NormalWord, text, 5);
            Assert.True(span.IsSome());
            Assert.Equal("dog", text.Substring(span.Value.Start, span.Value.Length));
        }

        [Fact]
        public void FindPreviousWordSpan1()
        {
            Assert.Equal(0, TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "foo", 1).Value.Start);
            Assert.Equal(0, TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "foo", 2).Value.Start);
        }

        /// <summary>
        /// Move back accross a blank
        /// </summary>
        [Fact]
        public void FindPreviousWordSpan_FromBlank()
        {
            var span = TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "foo bar", 3);
            Assert.True(span.IsSome());
            Assert.Equal(new Span(0, 3), span.Value);
        }

        /// <summary>
        /// Move back accross a blank
        /// </summary>
        [Fact]
        public void FindPreviousWordSpan_FromSecondBlank()
        {
            var span = TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "foo bar baz", 7);
            Assert.True(span.IsSome());
            Assert.Equal(new Span(4, 3), span.Value);
        }

        /// <summary>
        /// Move back when starting at a word.  Shouldd go to the start of the previous word
        /// </summary>
        [Fact]
        public void FindPreviousWordSpan_FromStartOfWord()
        {
            var span = TextUtil.FindPreviousWordSpan(WordKind.BigWord, "foo bar", 4);
            Assert.True(span.IsSome());
            Assert.Equal(new Span(0, 3), span.Value);
        }

        /// <summary>
        /// At the start of a line there is no previous word 
        /// </summary>
        [Fact]
        public void FindPreviousWordSpan_NoPreviousWord()
        {
            Assert.True(TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "foo", 0).IsNone());
            Assert.True(TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "   foo", 1).IsNone());
            Assert.True(TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "   foo", 3).IsNone());
        }

        /// <summary>
        /// Mix of word and WORD characters
        /// </summary>
        [Fact]
        public void FindPreviousWordSpan_MixedSymbols()
        {
            var span = TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "foo#$", 4);
            Assert.True(span.IsSome());
            Assert.Equal(new Span(3, 2), span.Value);

            span = TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "foo #$", 5);
            Assert.True(span.IsSome());
            Assert.Equal(new Span(4, 2), span.Value);
        }

        /// <summary>
        /// Simple test to jump past the curret word and find the next word in the 
        /// provided strinrg.  Verify this is correct for every index in the preceeding
        /// word
        /// </summary>
        [Fact]
        public void FindNextWordSpan_Simple()
        {
            var text = "cat dog";
            for (var i = 0; i < 3; i++)
            {
                var span = TextUtil.FindNextWordSpan(WordKind.NormalWord, text, i);
                Assert.True(span.IsSome());
                Assert.Equal("dog", text.Substring(span.Value.Start, span.Value.Length));
            }
        }
    }
}
