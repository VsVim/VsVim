using System.Linq;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.Extensions;

namespace VimCore.UnitTest
{
    /// <summary>
    /// Summary description for TextUtilTest
    /// </summary>
    [TestFixture]
    public class TextUtilTest
    {
        private void AssertWordSpans(string input, WordKind kind, params string[] expected)
        {
            var wordsForward = TextUtil.GetWordSpans(kind, Path.Forward, input).Select(x => input.Substring(x.Start, x.Length)).ToList();
            CollectionAssert.AreEquivalent(
                expected,
                wordsForward);
            var wordsBackward = TextUtil.GetWordSpans(kind, Path.Backward, input).Select(x => input.Substring(x.Start, x.Length)).ToList();
            CollectionAssert.AreEquivalent(
                expected.Reverse(),
                wordsBackward);
        }

        private string FindCurrentNormalWord(string input, int index)
        {
            var span = TextUtil.FindCurrentWordSpan(WordKind.NormalWord, input, index);
            Assert.IsTrue(span.IsSome());
            return input.Substring(span.Value.Start, span.Value.Length);
        }

        private string FindCurrentBigWord(string input, int index)
        {
            var span = TextUtil.FindCurrentWordSpan(WordKind.BigWord, input, index);
            Assert.IsTrue(span.IsSome());
            return input.Substring(span.Value.Start, span.Value.Length);
        }

        /// <summary>
        /// Simple test of getting word spans
        /// </summary>
        [Test]
        public void GetWordSpans_Simple()
        {
            AssertWordSpans("foo bar baz!", WordKind.NormalWord, "foo", "bar", "baz", "!");
        }

        /// <summary>
        /// Put some digits and underscores into the words
        /// </summary>
        [Test]
        public void GetWordSpans_WithDigitsAndUnderscores()
        {
            AssertWordSpans("h42o wo_orld", WordKind.NormalWord, "h42o", "wo_orld");
        }

        /// <summary>
        /// Put some non-word characters into the middle of the words
        /// </summary>
        [Test]
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
        [Test]
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
        [Test]
        public void FindWord1()
        {
            Assert.AreEqual("foo", FindCurrentNormalWord("foo ", 0));
            Assert.AreEqual("foo_123", FindCurrentNormalWord("foo_123", 0));
        }

        /// <summary>
        /// Non-zero index tests
        /// </summary>
        [Test]
        public void FindWord2()
        {
            Assert.AreEqual("oo", FindCurrentNormalWord("foo", 1));
            Assert.AreEqual("oo123", FindCurrentNormalWord("foo123", 1));
        }

        /// <summary>
        /// Limits
        /// </summary>
        [Test]
        public void FindWord3()
        {
            var span = TextUtil.FindCurrentWordSpan(WordKind.NormalWord, " foo", 0);
            Assert.IsTrue(span.IsNone());

            Assert.AreEqual("oo_", FindCurrentNormalWord("foo_", 1));

            span = TextUtil.FindCurrentWordSpan(WordKind.NormalWord, "foo", 23);
            Assert.IsTrue(span.IsNone());
        }

        /// <summary>
        /// Non-keyword words
        /// </summary>
        [Test]
        public void FindWord4()
        {
            Assert.AreEqual("!@#$", FindCurrentNormalWord("!@#$", 0));
            Assert.AreEqual("!!!", FindCurrentNormalWord("foo!!!", 3));
        }

        /// <summary>
        /// Mix of keyword and non-keyword strings
        /// </summary>
        [Test]
        public void FindWord5()
        {
            Assert.AreEqual("#$", FindCurrentNormalWord("#$foo", 0));
            Assert.AreEqual("foo", FindCurrentNormalWord("foo!@#$", 0));
        }

        [Test]
        public void FindBigWord1()
        {
            Assert.AreEqual("foo!@#$", FindCurrentBigWord("foo!@#$", 0));
            Assert.AreEqual("!foo!", FindCurrentBigWord("!foo!", 0));
        }

        /// <summary>
        /// Make sure that FindFullWordSpan words the span for any index into the
        /// word
        /// </summary>
        [Test]
        public void FindFullWordSpan_Normal_FromAllParts()
        {
            var word = "foo";
            for (var i = 0; i < word.Length; i++)
            {
                var span = TextUtil.FindFullWordSpan(WordKind.NormalWord, word, i);
                Assert.IsTrue(span.IsSome());
                Assert.AreEqual(new Span(0, 3), span.Value);
            }
        }

        /// <summary>
        /// Make sure that FindFullWordSpan words the span for any index into the
        /// word on big words
        /// </summary>
        [Test]
        public void FindFullWordSpan_Big_FromAllParts()
        {
            var word = "foo_123";
            var text = word + " and some extra";
            for (var i = 0; i < word.Length; i++)
            {
                var span = TextUtil.FindFullWordSpan(WordKind.NormalWord, text, i);
                Assert.IsTrue(span.IsSome());
                Assert.AreEqual(word, text.Substring(span.Value.Start, span.Value.Length));
            }
        }

        /// <summary>
        /// Another test to ensure that FindFullWordSpan works with words that are 
        /// entirely composed of symbols
        /// </summary>
        [Test]
        public void FindFullBigWord_Big_FromAllParts2()
        {
            var word = "!@#";
            for (var i = 0; i < word.Length; i++)
            {
                var span = TextUtil.FindFullWordSpan(WordKind.NormalWord, word, i);
                Assert.IsTrue(span.IsSome());
                Assert.AreEqual(new Span(0, 3), span.Value);
            }
        }

        /// <summary>
        /// Ensure that FindFullWordSpan works from the middle of the string 
        /// </summary>
        [Test]
        public void FindFullWordSpan_Normal_MiddleOfString()
        {
            var text = "cat dog";
            var span = TextUtil.FindFullWordSpan(WordKind.NormalWord, text, 5);
            Assert.IsTrue(span.IsSome());
            Assert.AreEqual("dog", text.Substring(span.Value.Start, span.Value.Length));
        }

        [Test]
        public void FindPreviousWordSpan1()
        {
            Assert.AreEqual(0, TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "foo", 1).Value.Start);
            Assert.AreEqual(0, TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "foo", 2).Value.Start);
        }

        /// <summary>
        /// Move back accross a blank
        /// </summary>
        [Test]
        public void FindPreviousWordSpan_FromBlank()
        {
            var span = TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "foo bar", 3);
            Assert.IsTrue(span.IsSome());
            Assert.AreEqual(new Span(0, 3), span.Value);
        }

        /// <summary>
        /// Move back accross a blank
        /// </summary>
        [Test]
        public void FindPreviousWordSpan_FromSecondBlank()
        {
            var span = TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "foo bar baz", 7);
            Assert.IsTrue(span.IsSome());
            Assert.AreEqual(new Span(4, 3), span.Value);
        }

        /// <summary>
        /// Move back when starting at a word.  Shouldd go to the start of the previous word
        /// </summary>
        [Test]
        public void FindPreviousWordSpan_FromStartOfWord()
        {
            var span = TextUtil.FindPreviousWordSpan(WordKind.BigWord, "foo bar", 4);
            Assert.IsTrue(span.IsSome());
            Assert.AreEqual(new Span(0, 3), span.Value);
        }

        /// <summary>
        /// At the start of a line there is no previous word 
        /// </summary>
        [Test]
        public void FindPreviousWordSpan_NoPreviousWord()
        {
            Assert.IsTrue(TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "foo", 0).IsNone());
            Assert.IsTrue(TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "   foo", 1).IsNone());
            Assert.IsTrue(TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "   foo", 3).IsNone());
        }

        /// <summary>
        /// Mix of word and WORD characters
        /// </summary>
        [Test]
        public void FindPreviousWordSpan_MixedSymbols()
        {
            var span = TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "foo#$", 4);
            Assert.IsTrue(span.IsSome());
            Assert.AreEqual(new Span(3, 2), span.Value);

            span = TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "foo #$", 5);
            Assert.IsTrue(span.IsSome());
            Assert.AreEqual(new Span(4, 2), span.Value);
        }

        /// <summary>
        /// Simple test to jump past the curret word and find the next word in the 
        /// provided strinrg.  Verify this is correct for every index in the preceeding
        /// word
        /// </summary>
        [Test]
        public void FindNextWordSpan_Simple()
        {
            var text = "cat dog";
            for (var i = 0; i < 3; i++)
            {
                var span = TextUtil.FindNextWordSpan(WordKind.NormalWord, text, i);
                Assert.IsTrue(span.IsSome());
                Assert.AreEqual("dog", text.Substring(span.Value.Start, span.Value.Length));
            }
        }
    }
}
