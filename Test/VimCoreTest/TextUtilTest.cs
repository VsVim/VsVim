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
        private void AssertWordSpans(string text, WordKind kind, params string[] expected)
        {
            var wordsForward = TextUtil.GetWordSpans(kind, SearchPath.Forward, text).Select(x => text.Substring(x.Start, x.Length)).ToList();
            Assert.Equal(
                expected,
                wordsForward);
            var wordsBackward = TextUtil.GetWordSpans(kind, SearchPath.Backward, text).Select(x => text.Substring(x.Start, x.Length)).ToList();
            Assert.Equal(
                expected.Reverse(),
                wordsBackward);
        }

        private string GetCurrentNormalWord(string text, int index)
        {
            var span = TextUtil.GetCurrentWordSpan(WordKind.NormalWord, text, index);
            Assert.True(span.IsSome());
            return text.Substring(span.Value.Start, span.Value.Length);
        }

        private string GetCurrentBigWord(string text, int index)
        {
            var span = TextUtil.GetCurrentWordSpan(WordKind.BigWord, text, index);
            Assert.True(span.IsSome());
            return text.Substring(span.Value.Start, span.Value.Length);
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
        public void GetWord1()
        {
            Assert.Equal("foo", GetCurrentNormalWord("foo ", 0));
            Assert.Equal("foo_123", GetCurrentNormalWord("foo_123", 0));
        }

        /// <summary>
        /// Non-zero index tests
        /// </summary>
        [Fact]
        public void GetWord2()
        {
            Assert.Equal("oo", GetCurrentNormalWord("foo", 1));
            Assert.Equal("oo123", GetCurrentNormalWord("foo123", 1));
        }

        /// <summary>
        /// Limits
        /// </summary>
        [Fact]
        public void GetWord3()
        {
            var span = TextUtil.GetCurrentWordSpan(WordKind.NormalWord, " foo", 0);
            Assert.True(span.IsNone());

            Assert.Equal("oo_", GetCurrentNormalWord("foo_", 1));

            span = TextUtil.GetCurrentWordSpan(WordKind.NormalWord, "foo", 23);
            Assert.True(span.IsNone());
        }

        /// <summary>
        /// Non-keyword words
        /// </summary>
        [Fact]
        public void GetWord4()
        {
            Assert.Equal("!@#$", GetCurrentNormalWord("!@#$", 0));
            Assert.Equal("!!!", GetCurrentNormalWord("foo!!!", 3));
        }

        /// <summary>
        /// Mix of keyword and non-keyword strings
        /// </summary>
        [Fact]
        public void GetWord5()
        {
            Assert.Equal("#$", GetCurrentNormalWord("#$foo", 0));
            Assert.Equal("foo", GetCurrentNormalWord("foo!@#$", 0));
        }

        [Fact]
        public void GetBigWord1()
        {
            Assert.Equal("foo!@#$", GetCurrentBigWord("foo!@#$", 0));
            Assert.Equal("!foo!", GetCurrentBigWord("!foo!", 0));
        }

        /// <summary>
        /// Make sure that GetFullWordSpan words the span for any index into the
        /// word
        /// </summary>
        [Fact]
        public void GetFullWordSpan_Normal_FromAllParts()
        {
            var word = "foo";
            for (var i = 0; i < word.Length; i++)
            {
                var span = TextUtil.GetFullWordSpan(WordKind.NormalWord, word, i);
                Assert.True(span.IsSome());
                Assert.Equal(new Span(0, 3), span.Value);
            }
        }

        /// <summary>
        /// Make sure that GetFullWordSpan words the span for any index into the
        /// word on big words
        /// </summary>
        [Fact]
        public void GetFullWordSpan_Big_FromAllParts()
        {
            var word = "foo_123";
            var text = word + " and some extra";
            for (var i = 0; i < word.Length; i++)
            {
                var span = TextUtil.GetFullWordSpan(WordKind.NormalWord, text, i);
                Assert.True(span.IsSome());
                Assert.Equal(word, text.Substring(span.Value.Start, span.Value.Length));
            }
        }

        /// <summary>
        /// Another test to ensure that GetFullWordSpan works with words that are 
        /// entirely composed of symbols
        /// </summary>
        [Fact]
        public void GetFullBigWord_Big_FromAllParts2()
        {
            var word = "!@#";
            for (var i = 0; i < word.Length; i++)
            {
                var span = TextUtil.GetFullWordSpan(WordKind.NormalWord, word, i);
                Assert.True(span.IsSome());
                Assert.Equal(new Span(0, 3), span.Value);
            }
        }

        /// <summary>
        /// Ensure that GetFullWordSpan works from the middle of the string 
        /// </summary>
        [Fact]
        public void GetFullWordSpan_Normal_MiddleOfString()
        {
            var text = "cat dog";
            var span = TextUtil.GetFullWordSpan(WordKind.NormalWord, text, 5);
            Assert.True(span.IsSome());
            Assert.Equal("dog", text.Substring(span.Value.Start, span.Value.Length));
        }

        [Fact]
        public void GetPreviousWordSpan1()
        {
            Assert.Equal(0, TextUtil.GetPreviousWordSpan(WordKind.NormalWord, "foo", 1).Value.Start);
            Assert.Equal(0, TextUtil.GetPreviousWordSpan(WordKind.NormalWord, "foo", 2).Value.Start);
        }

        /// <summary>
        /// Move back accross a blank
        /// </summary>
        [Fact]
        public void GetPreviousWordSpan_FromBlank()
        {
            var span = TextUtil.GetPreviousWordSpan(WordKind.NormalWord, "foo bar", 3);
            Assert.True(span.IsSome());
            Assert.Equal(new Span(0, 3), span.Value);
        }

        /// <summary>
        /// Move back accross a blank
        /// </summary>
        [Fact]
        public void GetPreviousWordSpan_FromSecondBlank()
        {
            var span = TextUtil.GetPreviousWordSpan(WordKind.NormalWord, "foo bar baz", 7);
            Assert.True(span.IsSome());
            Assert.Equal(new Span(4, 3), span.Value);
        }

        /// <summary>
        /// Move back when starting at a word.  Shouldd go to the start of the previous word
        /// </summary>
        [Fact]
        public void GetPreviousWordSpan_FromStartOfWord()
        {
            var span = TextUtil.GetPreviousWordSpan(WordKind.BigWord, "foo bar", 4);
            Assert.True(span.IsSome());
            Assert.Equal(new Span(0, 3), span.Value);
        }

        /// <summary>
        /// At the start of a line there is no previous word 
        /// </summary>
        [Fact]
        public void GetPreviousWordSpan_NoPreviousWord()
        {
            Assert.True(TextUtil.GetPreviousWordSpan(WordKind.NormalWord, "foo", 0).IsNone());
            Assert.True(TextUtil.GetPreviousWordSpan(WordKind.NormalWord, "   foo", 1).IsNone());
            Assert.True(TextUtil.GetPreviousWordSpan(WordKind.NormalWord, "   foo", 3).IsNone());
        }

        /// <summary>
        /// Mix of word and WORD characters
        /// </summary>
        [Fact]
        public void GetPreviousWordSpan_MixedSymbols()
        {
            var span = TextUtil.GetPreviousWordSpan(WordKind.NormalWord, "foo#$", 4);
            Assert.True(span.IsSome());
            Assert.Equal(new Span(3, 2), span.Value);

            span = TextUtil.GetPreviousWordSpan(WordKind.NormalWord, "foo #$", 5);
            Assert.True(span.IsSome());
            Assert.Equal(new Span(4, 2), span.Value);
        }

        /// <summary>
        /// Simple test to jump past the curret word and Get the next word in the 
        /// provided strinrg.  Verify this is correct for every index in the preceeding
        /// word
        /// </summary>
        [Fact]
        public void GetNextWordSpan_Simple()
        {
            var text = "cat dog";
            for (var i = 0; i < 3; i++)
            {
                var span = TextUtil.GetNextWordSpan(WordKind.NormalWord, text, i);
                Assert.True(span.IsSome());
                Assert.Equal("dog", text.Substring(span.Value.Start, span.Value.Length));
            }
        }
    }
}
