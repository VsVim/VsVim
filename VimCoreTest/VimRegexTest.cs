using NUnit.Framework;
using Vim;
using Vim.Extensions;

namespace VimCore.Test
{
    [TestFixture]
    public class VimRegexTest
    {
        private IVimGlobalSettings _settings;
        private VimRegexFactory _factory;

        [SetUp]
        public void Setup()
        {
            _settings = new Vim.GlobalSettings();
            _settings.IgnoreCase = true;
            _settings.SmartCase = false;
            _factory = new VimRegexFactory(_settings);
        }

        private void VerifyMatches(string pattern, params string[] inputArray)
        {
            var regex = _factory.Create(pattern);
            foreach (var cur in inputArray)
            {
                Assert.IsTrue(regex.IsMatch(cur));
            }
        }

        private void VerifyNotMatches(string pattern, params string[] inputArray)
        {
            var regex = _factory.Create(pattern);
            foreach (var cur in inputArray)
            {
                Assert.IsFalse(regex.IsMatch(cur));
            }
        }

        private void VerifyMatchIs(string pattern, string input, string toMatch)
        {
            var regex = _factory.Create(pattern);
            Assert.IsTrue(regex.Regex.IsSome());
            var match = regex.Regex.Value.Match(input);
            Assert.IsTrue(match.Success);
            Assert.AreEqual(toMatch, match.Value);
        }

        [Test]
        public void LettersCase1()
        {
            VerifyMatches("a", "a", "A");
            VerifyMatches("b", "b", "b");
        }

        [Test]
        public void LettersCase2()
        {
            _settings.IgnoreCase = false;
            VerifyMatches("a", "a");
            VerifyNotMatches("a", "A");
            VerifyMatches("b", "b");
            VerifyNotMatches("b", "B");
        }

        [Test]
        public void LettersCase3()
        {
            VerifyMatches(@"\Ca", "a");
            VerifyMatches(@"\Cb", "b");
            VerifyNotMatches(@"\Ca", "A");
            VerifyNotMatches(@"\Cb", "B");
        }

        [Test]
        [Description(@"The \C modifier takes precedence over ignorecase option")]
        public void LettersCase4()
        {
            _settings.IgnoreCase = true;
            VerifyMatches(@"\Ca", "a");
            VerifyMatches(@"\Cb", "b");
            VerifyNotMatches(@"\Ca", "A");
            VerifyNotMatches(@"\Cb", "B");
        }

        [Test]
        public void LettersCase5()
        {
            VerifyMatches(@"\ca", "a", "A");
            VerifyMatches(@"\cb", "b", "B");
        }

        [Test]
        [Description(@"The \c modifier takes precedence over the ignore case option")]
        public void LettersCase6()
        {
            _settings.IgnoreCase = false;
            VerifyMatches(@"\ca", "a", "A");
            VerifyMatches(@"\cb", "b", "B");
        }

        [Test]
        [Description(@"SmartCase should match both if only lower")]
        public void LettersCase7()
        {
            _settings.SmartCase = true;
            VerifyMatches("a", "A", "a");
            VerifyMatches("b", "b", "B");
        }

        [Test]
        [Description(@"SmartCase is case sensitive if any are upper")]
        public void LettersCase8()
        {
            _settings.SmartCase = true;
            VerifyMatches("A", "A");
            VerifyNotMatches("A", "a");
            VerifyMatches("B", "B");
            VerifyNotMatches("B", "b");
        }

        [Test]
        [Description(@"The \c modifier beats smart case as well")]
        public void LettersCase9()
        {
            _settings.SmartCase = true;
            VerifyMatches(@"\cFoo", "foo", "FOO", "fOO");
            VerifyMatches(@"\cBar", "BAR", "bar");
        }

        [Test]
        [Description(@"The \C modifier beats smart case as well")]
        public void LettersCase10()
        {
            _settings.SmartCase = true;
            VerifyMatches(@"\CFOO", "FOO");
            VerifyNotMatches(@"\CFOO", "foo");
            VerifyMatches(@"\CBAR", "BAR");
            VerifyNotMatches(@"\CBAR", "bar");
        }

        [Test]
        [Description("Verify the magic option")]
        public void Magic1()
        {
            _settings.Magic = true;
            VerifyMatches(".", "a", "b", "c");
        }

        [Test]
        [Description("Verify the nomagic option")]
        public void Magic2()
        {
            _settings.Magic = false;
            VerifyNotMatches(".", "a", "b", "c");
            VerifyMatches(@"\.", "a", "b", "c");
        }

        [Test]
        [Description("Verify the magic prefix ")]
        public void Magic3()
        {
            _settings.Magic = false;
            VerifyMatches(@"\m.", "a", "b", "c");
        }

        [Test]
        [Description("Verify the nomagic prefix")]
        public void Magic4()
        {
            _settings.Magic = true;
            VerifyNotMatches(@"\M.", "a", "b", "c");
            VerifyMatches(@"\M\.", "a", "b", "c");
        }

        [Test]
        public void Magic5()
        {
            _settings.Magic = false;
            VerifyMatches(@"a\m.", "ab", "ac");
        }

        [Test]
        public void Magic6()
        {
            _settings.Magic = true;
            VerifyNotMatches(@"a\M.", "ab", "ac");
            VerifyMatches(@"a\M.", "a.");
        }

        [Test]
        public void VeryMagic1()
        {
            _settings.Magic = false;
            VerifyMatches(@"\v.", "a", "b");
        }

        [Test]
        public void VeryMagic2()
        {
            _settings.Magic = true;
            VerifyNotMatches(@"\V.", "a", "b");
            VerifyMatches(@"\V\.", "a", "b");
        }

        [Test]
        public void ItemStar1()
        {
            VerifyMatchIs(@"ab*", "abb", "abb");
            VerifyMatchIs(@"ab*", "cab", "ab");
            VerifyMatchIs(@"ab*", "cabb", "abb");
        }

        [Test]
        public void ItemStar2()
        {
            VerifyMatchIs(@"\Mab*", "ab*", "ab*");
            VerifyMatchIs(@"\Mab\*", "ab", "ab");
            VerifyMatchIs(@"\Mab\*", "caabb", "a");
            VerifyMatchIs(@"\Mab\*", "cabb", "abb");
        }

        [Test]
        public void ItemStar3()
        {
            VerifyMatchIs(@"\mab*", "abb", "abb");
            VerifyMatchIs(@"\mab*", "cab", "ab");
            VerifyMatchIs(@"\mab*", "cabb", "abb");
        }

        [Test]
        public void ItemQuestion1()
        {
            VerifyMatchIs(@"ab?", "ab?", "ab?");
            VerifyMatchIs(@"ab\?", "ab", "ab");
            VerifyMatchIs(@"ab\?", "abc", "ab");
            VerifyMatchIs(@"ab\?", "adc", "a");
        }

        [Test]
        public void ItemQuestion2()
        {
            VerifyMatchIs(@"\Mab?", "ab?", "ab?");
            VerifyMatchIs(@"\Mab\?", "ab", "ab");
            VerifyMatchIs(@"\Mab\?", "abc", "ab");
        }

        [Test]
        public void ItemQuestion3()
        {
            VerifyMatchIs(@"\vab?", "ad", "a");
            VerifyMatchIs(@"\vab?", "ab", "ab");
            VerifyMatchIs(@"\vab?", "abc", "ab");
        }

        [Test]
        public void ItemEqual1()
        {
            VerifyMatchIs(@"ab\=", "a", "a");
            VerifyMatchIs(@"ab\=", "ab", "ab");
            VerifyMatchIs(@"ab\=", "abc", "ab");
        }

        [Test]
        public void ItemEqual2()
        {
            VerifyMatchIs(@"\Mab=", "ab=", "ab=");
            VerifyMatchIs(@"\Mab\=", "ab", "ab");
            VerifyMatchIs(@"\Mab\=", "abc", "ab");
            VerifyMatchIs(@"\Mab\=", "adc", "a");
        }

        [Test]
        public void ItemEqual3()
        {
            VerifyMatchIs(@"\vab=", "a", "a");
            VerifyMatchIs(@"\vab=", "ab", "ab");
            VerifyMatchIs(@"\vab=", "abc", "ab");
        }

        [Test]
        public void AtomHat1()
        {
            VerifyMatches(@"^m", "m");
            VerifyMatches(@"^", "aoeu");
        }

        [Test]
        public void AtomHat2()
        {
            VerifyMatches(@"\M^m", "m");
            VerifyMatches(@"\M^", "aoeu");
        }

        [Test]
        public void AtomHat3()
        {
            VerifyMatches(@"\v^m", "m");
            VerifyMatches(@"\v^", "aoeu");
        }

        [Test]
        [Description("Only use ^ as magic at the start of a pattern")]
        public void AtomHat4()
        {
            VerifyMatchIs(@"a^", "a^", "a^");
            VerifyMatchIs(@"\Ma^", "a^", "a^");
        }

        [Test]
        public void AtomHat5()
        {
            VerifyMatches(@"\V^m", "a^m");
            VerifyMatches(@"\V^", "a^aoeu");
        }

        [Test]
        public void AtomBackslashHat1()
        {
            VerifyMatchIs(@"\^", "^", "^");
            VerifyMatchIs(@"\^a", "^a", "^a");
            VerifyMatchIs(@"b\^a", "b^a", "b^a");
            VerifyNotMatches(@"\^", "a");
        }

        [Test]
        public void AtomBackslashHat2()
        {
            VerifyMatches(@"\V\^m", "m");
            VerifyMatches(@"\V\^", "aoeu");
        }

        [Test]
        public void AtomBackslashHat3()
        {
            VerifyMatchIs(@"\M\^", "^", "^");
            VerifyMatchIs(@"\M\^a", "^a", "^a");
            VerifyMatchIs(@"\Mb\^a", "b^a", "b^a");
            VerifyNotMatches(@"\M\^", "a");
        }

        [Test]
        public void AtomBackslashHat4()
        {
            VerifyMatchIs(@"\v\^", "^", "^");
            VerifyMatchIs(@"\v\^a", "^a", "^a");
            VerifyMatchIs(@"\vb\^a", "b^a", "b^a");
            VerifyNotMatches(@"\v\^", "a");
        }

        [Test]
        public void AtomBackslashUnderscoreHat1()
        {
            VerifyNotMatches(@"\_", "_");
            VerifyNotMatches(@"\_", "");
            VerifyNotMatches(@"ab\_", "ab");
        }

        [Test]
        public void AtomBackslashUnderscoreHat2()
        {
            VerifyNotMatches(@"\M\_", "_");
            VerifyNotMatches(@"\M\_", "_");
        }

        [Test]
        public void AtomBackslashUnderscoreHat3()
        {
            VerifyMatches(@"\_^", "abc");
            VerifyMatches(@"\_^", "");
            VerifyMatches(@"c\?\_^ab", "ab");
        }

        [Test]
        public void AtomBackslashUnderscoreHat4()
        {
            VerifyMatches(@"\M\_^", "abc");
            VerifyMatches(@"\M\_^", "");
            VerifyMatches(@"\Mc\?\_^ab", "ab");
        }

        [Test]
        public void AtomDollar1()
        {
            VerifyMatches(@"$", "");
            VerifyMatches(@"$", "aoe");
            VerifyMatches(@"\M$", "aoe");
        }

        [Test]
        public void AtomDollar2()
        {
            VerifyMatchIs(@"a$", "baaa", "a");
            VerifyMatchIs(@"a*$", "baaa", "aaa");
        }

        [Test]
        public void AtomDollar3()
        {
            VerifyMatchIs(@"\Ma$", "baaa", "a");
            VerifyMatchIs(@"\Ma\*$", "baaa", "aaa");
        }

        [Test]
        public void AtomDollar4()
        {
            VerifyMatchIs(@"\Ma$b", "a$bz", "a$b");
            VerifyMatchIs(@"\Ma\*$b", "aa$bz", "aa$b");
        }

        [Test]
        public void AtomBackslashDollar1()
        {
            VerifyNotMatches(@"\$", "");
            VerifyNotMatches(@"\$", "aoe");
            VerifyNotMatches(@"\M\$", "aoe");
        }

        [Test]
        public void AtomBackslashDollar2()
        {
            VerifyMatchIs(@"\$", "$", "$");
            VerifyMatchIs(@"\$ab", "$ab", "$ab");
        }

        [Test]
        public void AtomBackslashUnderscoreDollar1()
        {
            VerifyMatchIs(@"\$", "$", "$");
            VerifyMatchIs(@"\$ab", "$ab", "$ab");
        }

        [Test]
        public void AtomBackslashUnderscoreDollar2()
        {
            VerifyNotMatches(@"\_", "_");
            VerifyNotMatches(@"\_", "");
        }

        [Test]
        public void AtomBackslashUnderscoreDollar3()
        {
            VerifyMatchIs(@"\Ma\_$", "baaa", "a");
            VerifyMatchIs(@"\Ma\*\_$", "baaa", "aaa");
        }

        [Test]
        public void WordBoundary1()
        {
            VerifyNotMatches(@"\<word", "aword");
            VerifyNotMatches(@"\M\<word", "aword");
        }

        [Test]
        public void WordBoundary2()
        {
            VerifyMatches(@"\<word", "a word");
            VerifyMatches(@"\M\<word", "a word");
        }

        [Test]
        public void WordBoundary3()
        {
            VerifyMatchIs(@"\<word", "a word", "word");
            VerifyMatchIs(@"\M\<word", "a word", "word");
        }

        [Test]
        public void WordBoundary4()
        {
            VerifyNotMatches(@"word\>", "words");
            VerifyNotMatches(@"\Mword\>", "words");
        }

        [Test]
        public void WordBoundary5()
        {
            VerifyMatches(@"word\>", "a word again");
            VerifyMatches(@"\Mword\>", "a word again");
        }

        [Test]
        [Description("Boundary at the end of a line")]
        public void WordBoundary6()
        {
            VerifyMatches(@"word\>", "a word");
            VerifyMatches(@"\Mword\>", "a word");
        }
    }
}
