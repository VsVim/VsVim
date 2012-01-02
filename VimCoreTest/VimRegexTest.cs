using System;
using System.Linq;
using Microsoft.FSharp.Core;
using NUnit.Framework;
using Vim.Extensions;

namespace Vim.UnitTest
{
    [TestFixture]
    public sealed class VimRegexTest
    {
        private static readonly string[] LowerCaseLetters = TestConstants.LowerCaseLetters.Select(x => x.ToString()).ToArray();
        private static readonly string[] UpperCaseLetters = TestConstants.UpperCaseLetters.Select(x => x.ToString()).ToArray();
        private static readonly string[] Digits = TestConstants.Digits.Select(x => x.ToString()).ToArray();
        private IVimGlobalSettings _globalSettings;

        [SetUp]
        public void Setup()
        {
            _globalSettings = new GlobalSettings();
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = false;
        }

        private FSharpOption<VimRegex> Create(string pattern)
        {
            return VimRegexFactory.CreateForSettings(pattern, _globalSettings);
        }

        private void VerifyMatches(string pattern, params string[] inputArray)
        {
            VerifyMatches(VimRegexOptions.Default, pattern, inputArray);
        }

        private void VerifyMatches(VimRegexOptions options, string pattern, params string[] inputArray)
        {
            var opt = VimRegexFactory.Create(pattern, options);
            Assert.IsTrue(opt.IsSome());
            var regex = opt.Value;
            foreach (var cur in inputArray)
            {
                Assert.IsTrue(regex.IsMatch(cur));
            }
        }

        private void VerifyNotRegex(string pattern)
        {
            Assert.IsTrue(Create(pattern).IsNone());
        }

        private void VerifyNotMatches(string pattern, params string[] inputArray)
        {
            VerifyNotMatches(VimRegexOptions.Default, pattern, inputArray);
        }

        private void VerifyNotMatches(VimRegexOptions options, string pattern, params string[] inputArray)
        {
            var opt = VimRegexFactory.Create(pattern, options);
            Assert.IsTrue(opt.IsSome());
            var regex = opt.Value;
            foreach (var cur in inputArray)
            {
                Assert.IsFalse(regex.IsMatch(cur));
            }
        }

        private void VerifyMatchIs(string pattern, string input, string toMatch)
        {
            var regex = Create(pattern);
            Assert.IsTrue(regex.IsSome());
            var match = regex.Value.Regex.Match(input);
            Assert.IsTrue(match.Success);
            Assert.AreEqual(toMatch, match.Value);
        }

        private void VerifyReplace(string pattern, string input, string replace, string result)
        {
            VerifyReplace(VimRegexOptions.Default, pattern, input, replace, result);
        }

        private void VerifyReplace(VimRegexOptions options, string pattern, string input, string replace, string result)
        {
            var regex = VimRegexFactory.Create(pattern, options);
            Assert.IsTrue(regex.IsSome());

            var noMagic = VimRegexOptions.NoMagic == (options & VimRegexOptions.NoMagic);
            var replaceData = new ReplaceData(Environment.NewLine, !noMagic, 1);
            Assert.AreEqual(result, regex.Value.ReplaceAll(input, replace, replaceData));
        }

        [Test]
        public void Case_Simple()
        {
            VerifyMatches(VimRegexOptions.IgnoreCase, "a", "a", "A");
            VerifyMatches("b", "b", "b");
        }

        /// <summary>
        /// Make sure the parsing is case sensitive by default
        /// </summary>
        [Test]
        public void Case_RespectIgnoreCase()
        {
            VerifyMatches("a", "a");
            VerifyNotMatches("a", "A");
            VerifyMatches("b", "b");
            VerifyNotMatches("b", "B");
        }

        [Test]
        public void Case_SensitiveSpecifier()
        {
            VerifyMatches(@"\Ca", "a");
            VerifyMatches(@"\Cb", "b");
            VerifyNotMatches(@"\Ca", "A");
            VerifyNotMatches(@"\Cb", "B");
        }

        /// <summary>
        /// Make sure we support the \C specifier anywhere in the search string
        /// </summary>
        [Test]
        public void Case_SensitiveSpecifierInMiddleOfString()
        {
            var regex = Create(@"d\Cog").Value;
            Assert.IsTrue(regex.CaseSpecifier.IsOrdinalCase);
            Assert.AreEqual(@"d\Cog", regex.VimPattern);
            Assert.AreEqual("dog", regex.RegexPattern);
        }

        /// <summary>
        /// The \C modifier takes precedence over ignorecase option
        /// </summary>
        [Test]
        public void Case_SensitiveSpecifierBeatsIgonreCase()
        {
            VerifyMatches(VimRegexOptions.IgnoreCase, @"\Ca", "a");
            VerifyMatches(VimRegexOptions.IgnoreCase, @"\Cb", "b");
            VerifyNotMatches(VimRegexOptions.IgnoreCase, @"\Ca", "A");
            VerifyNotMatches(VimRegexOptions.IgnoreCase, @"\Cb", "B");
        }

        [Test]
        public void Case_InsensitiveSpecifier()
        {
            VerifyMatches(@"\ca", "a", "A");
            VerifyMatches(@"\cb", "b", "B");
        }

        /// <summary>
        /// Make sure we support the \c specifier anywhere in the search string
        /// </summary>
        [Test]
        public void Case_InsensitiveSpecifierInMiddleOfString()
        {
            var regex = Create(@"D\cOG").Value;
            Assert.IsTrue(regex.CaseSpecifier.IsIgnoreCase);
            Assert.AreEqual(@"D\cOG", regex.VimPattern);
            Assert.AreEqual("DOG", regex.RegexPattern);
        }

        /// <summary>
        /// The \c modifier takes precedence over the ignore case option
        /// </summary>
        [Test]
        public void Case_InsensitiveSpecifierBeatsDefault()
        {
            VerifyMatches(@"\ca", "a", "A");
            VerifyMatches(@"\cb", "b", "B");
        }

        /// <summary>
        /// SmartCase should match both if only lower
        /// </summary>
        [Test]
        public void SmartCase_Simple()
        {
            VerifyMatches(VimRegexOptions.SmartCase | VimRegexOptions.IgnoreCase, "a", "A", "a");
            VerifyMatches(VimRegexOptions.SmartCase | VimRegexOptions.IgnoreCase, "b", "b", "B");
        }

        /// <summary>
        /// SmartCase is case sensitive if any are upper
        /// </summary>
        [Test]
        public void SmartCase_WithUpper()
        {
            VerifyMatches(VimRegexOptions.SmartCase, "A", "A");
            VerifyNotMatches(VimRegexOptions.SmartCase, "A", "a");
            VerifyMatches(VimRegexOptions.SmartCase, "B", "B");
            VerifyNotMatches(VimRegexOptions.SmartCase, "B", "b");
        }

        /// <summary>
        /// The \c modifier beats smart case as well
        /// </summary>
        [Test]
        public void SmartCase_InsensitiveSpecifierWins()
        {
            VerifyMatches(VimRegexOptions.SmartCase, @"\cFoo", "foo", "FOO", "fOO");
            VerifyMatches(VimRegexOptions.SmartCase, @"\cBar", "BAR", "bar");
        }

        /// <summary>
        /// The \C modifier beats smart case as well
        /// </summary>
        [Test]
        public void SmartCase_SensitiveSpecifierWins()
        {
            var options = VimRegexOptions.SmartCase | VimRegexOptions.IgnoreCase;
            VerifyMatches(options, @"\CFOO", "FOO");
            VerifyNotMatches(options, @"\CFOO", "foo");
            VerifyMatches(options, @"\CBAR", "BAR");
            VerifyNotMatches(options, @"\CBAR", "bar");
        }

        [Test]
        public void CreateRegexOptions_NoMagic()
        {
            _globalSettings.Magic = false;
            var options = VimRegexFactory.CreateRegexOptions(_globalSettings);
            Assert.AreEqual(VimRegexOptions.NoMagic, options & VimRegexOptions.NoMagic);
        }

        /// <summary>
        /// Magic should the default setting
        /// </summary>
        [Test]
        public void Magic_ShouldBeDefault()
        {
            VerifyMatches(".", "a", "b", "c");
        }

        /// <summary>
        /// The \m should override the NoMagic option on the regex
        /// </summary>
        [Test]
        public void Magic_MagicSpecifierHasPrecedence()
        {
            VerifyMatches(VimRegexOptions.NoMagic, @"\m.", "a", "b", "c");
        }

        /// <summary>
        /// The \M should oveerride the default VimRegexOptions
        /// </summary>
        [Test]
        public void Magic_NoMagicSpecifierHasPrecedence()
        {
            VerifyNotMatches(@"\M.", "a", "b", "c");
            VerifyMatches(@"\M\.", "a", "b", "c");
        }

        /// <summary>
        /// The \M should oveerride the default VimRegexOptions
        /// </summary>
        [Test]
        public void Magic_MagicSpecifierInMiddle()
        {
            VerifyMatches(VimRegexOptions.NoMagic, @"a\m.", "ab", "ac");
        }

        [Test]
        public void Magic_NoMagicSpecifierInMiddle()
        {
            VerifyNotMatches(@"a\M.", "ab", "ac");
            VerifyMatches(@"a\M.", "a.");
        }

        [Test]
        public void VeryMagic1()
        {
            VerifyMatches(VimRegexOptions.NoMagic, @"\v.", "a", "b");
        }

        [Test]
        public void VeryMagic2()
        {
            VerifyNotMatches(@"\V.", "a", "b");
            VerifyMatches(@"\V\.", "a", "b");
        }

        [Test]
        public void VeryNoMagicDotIsNotSpecial()
        {
            VerifyNotMatches(@"\V.", "a");
            VerifyMatches(@"\V.", ".");
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
            VerifyNotRegex(@"\_");
            VerifyNotRegex(@"ab\_");
        }

        [Test]
        public void AtomBackslashUnderscoreHat2()
        {
            VerifyNotRegex(@"\M\_");
            VerifyNotRegex(@"\M\_");
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
            VerifyNotRegex(@"\_");
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

        [Test]
        public void Grouping1()
        {
            VerifyMatchIs(@"(a)", "foo(a)", "(a)");
            VerifyMatchIs(@"(abc)", "foo(abc)", "(abc)");
        }

        [Test]
        public void Grouping2()
        {
            VerifyMatchIs(@"\(ab\)", "foo(ab)", "ab");
            VerifyMatchIs(@"\(ab\)", "abc", "ab");
        }

        [Test]
        public void Grouping3()
        {
            VerifyMatchIs(@"\v(a)", "foo(a)", "a");
            VerifyMatchIs(@"\v(abc)", "foo(abc)", "abc");
        }

        [Test]
        public void Grouping4()
        {
            var regex = Create(@"\(");
            Assert.IsTrue(regex.IsNone());
        }

        [Test]
        public void Separator1()
        {
            VerifyMatchIs(@"a\|b", "foob", "b");
            VerifyMatchIs(@"a\|b", "acat", "a");
        }

        [Test]
        public void Separator2()
        {
            VerifyMatchIs(@"ab\|c", "abod", "ab");
            VerifyMatchIs(@"ab\|c", "babod", "ab");
            VerifyMatchIs(@"ab\|c", "bacod", "c");
        }

        [Test]
        public void Separator3()
        {
            VerifyMatchIs(@"\vab|c", "abod", "ab");
            VerifyMatchIs(@"\vab|c", "babod", "ab");
            VerifyMatchIs(@"\vab|c", "bacod", "c");
        }

        [Test]
        [Description("Simple no-magic replace")]
        public void Replace1()
        {
            VerifyReplace(@"foo", "foo bar", "bar", "bar bar");
            VerifyReplace(@"foo", "foo bar baz", "bar", "bar bar baz");
        }

        [Test]
        [Description("Atom match and replace")]
        public void Replace2()
        {
            VerifyReplace(@"a\|b", "cat", "o", "cot");
        }

        [Test]
        [Description("Word boundary replacements")]
        public void Replace3()
        {
            VerifyReplace(@"\<foo\>", "foo bar", "bar", "bar bar");
            VerifyReplace(@"\<foo\>", "foobar", "bar", "foobar");
            VerifyReplace(@"\<foo\>", "foo bar baz", "bar", "bar bar baz");
        }

        [Test]
        public void Replace4()
        {
            VerifyReplace(@"(ab)", "foo(ab)", "()", "foo()");
            VerifyReplace(@"foo(ab)", "foo(ab)", "()", "()");
            VerifyReplace(@"foo()", "foo(ab)", "()", "foo(ab)");
        }

        [Test]
        public void Replace5()
        {
            VerifyReplace(@"\(ab\)", "ab", "", "");
            VerifyReplace(@"\(ab\)", "cab", "", "c");
            VerifyReplace(@"\(ab\)", "c(ab)", "", "c()");
        }

        [Test]
        [Description("Replacement using the group specifier")]
        public void Replace6()
        {
            VerifyReplace(@"foo\(\.*\)", "foobar", @"\1", "bar");
            VerifyReplace(@"jaz\(\.*\)", "jaz123", @"\1", "123");
        }

        [Test]
        [Description("Replacement using the group specifier")]
        public void Replace7()
        {
            VerifyReplace(@"\(\.*\)b\(\.*\)", "abc", @"\2", "ac");
            VerifyReplace(@"\(\.*\)b\(\.*\)", "abc", @"\1\2", "ac");
            VerifyReplace(@"\(\.*\)b\(\.*\)", "abc", @"\1blah\2", "ablahc");
        }

        /// <summary>
        /// Escaped back slashes should appear as normal back slashes in the replacement string
        /// </summary>
        [Test]
        public void Replace_EscapedBackSlashes()
        {
            VerifyReplace("b", "abc", @"\\\\", @"a\\c");
        }

        /// <summary>
        /// When the '&' character is used in the replacement string it should replace with 
        /// the entire matched pattern
        /// </summary>
        [Test]
        public void Replace_Ampersand()
        {
            VerifyReplace("a", "cat", @"o&", "coat");
            VerifyReplace(@"a\+", "caat", @"o&", "coaat");
        }

        /// <summary>
        /// When there is no magic then the ampersand is not special and should replace 
        /// as normal
        /// </summary>
        [Test]
        public void Replace_Ampersand_NoMagic()
        {
            VerifyReplace(VimRegexOptions.NoMagic, "a", "cat", @"o&", "co&t");
            VerifyReplace(VimRegexOptions.NoMagic, @"a\+", "caat", @"o&", "co&t");
        }

        /// <summary>
        /// When escaped with magic it should behave simply as an ampersand
        /// </summary>
        [Test]
        public void Replace_EscapedAmpersand()
        {
            VerifyReplace("a", "cat", @"o\&", "co&t");
            VerifyReplace(@"a\+", "caat", @"o\&", "co&t");
        }

        /// <summary>
        /// The '\0' pattern is used to match the entire matched pattern.  It acts exactly 
        /// as '&' does in the replacement string
        /// </summary>
        [Test]
        public void Replace_EscapedZero()
        {
            VerifyReplace("a", "cat", @"o\0", "coat");
            VerifyReplace(@"a\+", "caat", @"o\0", "coaat");
        }

        /// <summary>
        /// The '\t' replacement string should insert a tab
        /// </summary>
        [Test]
        public void Replace_Escaped_T()
        {
            VerifyReplace("a", "a", @"\t", "\t");
            VerifyReplace("  ", "    ", @"\t", "\t\t");
        }

        /// <summary>
        /// The '\r' replacement should insert a carriage return
        /// </summary>
        [Test]
        public void Replace_Escaped_R()
        {
            VerifyReplace("a", "a", @"\r", Environment.NewLine);
        }

        [Test]
        public void CharacterSequence1()
        {
            VerifyMatches(@"[abc]", "a", "b", "c");
            VerifyMatches(@"\M\[abc]", "a", "b", "c");
        }

        [Test]
        [Description("Range support")]
        public void CharacterSequence2()
        {
            VerifyMatches(@"[a-z]", "a", "b", "c", "z");
            VerifyMatches(@"\M\[a-c]", "a", "b", "c");
        }

        [Test]
        public void AtomDigits()
        {
            VerifyMatches(@"\d", "1", "2");
            VerifyMatches(@"\M\d", "1", "2");
            VerifyNotMatches(@"\d", "a");
        }

        [Test]
        public void AtomNonDigits()
        {
            VerifyMatches(@"\D", "a", "b");
            VerifyMatches(@"\M\D", "a", "b");
            VerifyNotMatches(@"\M\D", "1", "2");
        }

        [Test]
        public void AtomWordCharacter()
        {
            VerifyMatches(@"\w", "a", "A", "_", "1", "4");
            VerifyMatches(@"\M\w", "a", "A", "_", "1", "4");
            VerifyNotMatches(@"\w", "%");
            VerifyNotMatches(@"\M\w", "%");
        }

        [Test]
        public void AtomNonWordCharacter()
        {
            VerifyNotMatches(@"\W", "a", "A", "_", "1", "4");
            VerifyNotMatches(@"\M\W", "a", "A", "_", "1", "4");
            VerifyMatches(@"\W", "%");
            VerifyMatches(@"\M\W", "%");
        }

        [Test]
        public void AtomHexDigit()
        {
            VerifyMatches(@"\x", "0123456789abcdef".Select(x => x.ToString()).ToArray());
            VerifyNotMatches(@"\x", "%", "^", "g", "h");
        }

        [Test]
        public void AtomNonHexDigit()
        {
            VerifyNotMatches(@"\X", "0123456789abcdef".Select(x => x.ToString()).ToArray());
            VerifyMatches(@"\X", "%", "^", "g", "h");
        }

        [Test]
        public void AtomOctal()
        {
            VerifyMatches(@"\o", "01234567".Select(x => x.ToString()).ToArray());
            VerifyNotMatches(@"\o", "%", "^", "g", "h", "8", "9");
        }

        [Test]
        public void AtomNonOctal()
        {
            VerifyNotMatches(@"\O", "01234567".Select(x => x.ToString()).ToArray());
            VerifyMatches(@"\O", "%", "^", "g", "h", "8", "9");
        }

        [Test]
        public void AtomHeadOfWord()
        {
            VerifyMatches(@"\h", LowerCaseLetters);
            VerifyMatches(@"\h", UpperCaseLetters);
            VerifyMatches(@"\h", "_");
            VerifyNotMatches(@"\h", Digits);
        }

        [Test]
        public void AtomNonHeadOfWord()
        {
            VerifyNotMatches(@"\H", LowerCaseLetters);
            VerifyNotMatches(@"\H", UpperCaseLetters);
            VerifyNotMatches(@"\H", "_");
            VerifyMatches(@"\H", Digits);
        }

        [Test]
        public void AtomAlphabeticChar()
        {
            VerifyMatches(@"\a", LowerCaseLetters);
            VerifyMatches(@"\a", UpperCaseLetters);
            VerifyNotMatches(@"\a", Digits);
        }

        [Test]
        public void AtomNonAlphabeticChar()
        {
            VerifyNotMatches(@"\A", LowerCaseLetters);
            VerifyNotMatches(@"\A", UpperCaseLetters);
            VerifyMatches(@"\A", "_");
            VerifyMatches(@"\A", Digits);
        }

        [Test]
        public void AtomLowerLetters()
        {
            _globalSettings.IgnoreCase = false;
            VerifyMatches(@"\l", LowerCaseLetters);
            VerifyNotMatches(@"\l", UpperCaseLetters);
            VerifyNotMatches(@"\l", Digits);
        }

        [Test]
        public void AtomNonLowerLetters()
        {
            _globalSettings.IgnoreCase = false;
            VerifyNotMatches(@"\L", LowerCaseLetters);
            VerifyMatches(@"\L", UpperCaseLetters);
            VerifyMatches(@"\L", "_");
            VerifyMatches(@"\L", Digits);
        }

        [Test]
        public void AtomUpperLetters()
        {
            _globalSettings.IgnoreCase = false;
            VerifyMatches(@"\u", UpperCaseLetters);
            VerifyNotMatches(@"\u", LowerCaseLetters);
            VerifyNotMatches(@"\u", Digits);
        }

        [Test]
        public void AtomNonUpperLetters()
        {
            _globalSettings.IgnoreCase = false;
            VerifyNotMatches(@"\U", UpperCaseLetters);
            VerifyMatches(@"\U", LowerCaseLetters);
            VerifyMatches(@"\U", "_");
            VerifyMatches(@"\U", Digits);
        }

        [Test]
        public void AtomPlus()
        {
            _globalSettings.Magic = true;
            VerifyMatches(@"a\+", "a", "aa");
            VerifyNotMatches(@"a\+", "b");
            VerifyMatchIs(@"\va+", "aa", "aa");
        }

        [Test]
        public void AtomCount()
        {
            _globalSettings.Magic = true;
            VerifyMatchIs(@"a\{1}", "aaa", "a");
            VerifyMatchIs(@"a\{1,2}", "aaa", "aa");
            VerifyMatchIs(@"a\{1,3}", "aaa", "aaa");
            VerifyMatchIs(@"a\{2,3}", "aaa", "aaa");
            VerifyNotMatches(@"a\{3}", "a");
        }

        [Test]
        public void AtomStar_Magic()
        {
            _globalSettings.Magic = true;
            VerifyMatchIs(@"a*", "aa", "aa");
            VerifyMatchIs(@"a\*", "a*", "a*");
        }

        [Test]
        public void AtomStar_NoMagic()
        {
            _globalSettings.Magic = false;
            VerifyMatchIs(@"a*", "a*", "a*");
            VerifyMatchIs(@"a\*", "aaa", "aaa");
        }

        [Test]
        public void AtomGroup_GroupAllBut()
        {
            _globalSettings.Magic = true;
            VerifyMatchIs(@"[^""]*b", "acbd", "acb");
            VerifyMatchIs(@"""[^""]*", @"b""cd", @"""cd");
        }

        [Test]
        public void AtomWhitespace_NoMagic()
        {
            _globalSettings.Magic = false;
            VerifyMatchIs(@"\s", " ", " ");
            VerifyMatchIs(@"hello\sworld", "hello world", "hello world");
            VerifyMatchIs(@"hello\s\*world", "hello   world", "hello   world");
        }

        [Test]
        public void AtomWhitespace_Magic()
        {
            _globalSettings.Magic = true;
            VerifyMatchIs(@"\s", " ", " ");
            VerifyMatchIs(@"hello\sworld", "hello world", "hello world");
            VerifyMatchIs(@"hello\s*world", "hello   world", "hello   world");
        }

        [Test]
        public void AtomWhitespace_MultipleWithStarQualifier()
        {
            _globalSettings.Magic = true;
            VerifyMatchIs(@"TCHAR\s\s*buff", "TCHAR buff", "TCHAR buff");
        }

        [Test]
        public void AtomNonWhitespace_NoMagic()
        {
            _globalSettings.Magic = false;
            VerifyMatchIs(@"\S", "a", "a");
            VerifyMatchIs(@"hello\Sworld", "hello!world", "hello!world");
        }

        [Test]
        public void AtomNonWhitespace_Magic()
        {
            _globalSettings.Magic = true;
            VerifyMatchIs(@"\S", "a", "a");
            VerifyMatchIs(@"hello\Sworld", "hello!world", "hello!world");
        }

        /// <summary>
        /// The '{' should match as a literal even when magic is on 
        /// </summary>
        [Test]
        public void AtomOpenCurly_Magic()
        {
            _globalSettings.Magic = true;
            VerifyMatches(@"{", "{");
            VerifyMatches(@"{hello}", "{hello}");
        }

        /// <summary>
        /// The '{' should register as a count open when very magic is on
        /// </summary>
        [Test]
        public void AtomOpenCurly_VeryMagic()
        {
            _globalSettings.Magic = true;
            VerifyMatches(@"\va{1,3}", "a", "aaa", "aa");
        }

        /// <summary>
        /// Make sure a few items are not actually regexs
        /// </summary>
        [Test]
        public void AtomOpenCurly_Bad()
        {
            VerifyNotRegex(@"\{");
            VerifyNotRegex(@"\v{");
            VerifyNotRegex(@"\{1");
        }

        /// <summary>
        /// The '}' should register as a literal even when very magic is on. 
        /// </summary>
        [Test]
        public void AtomCloseCurly_VeryMagic()
        {
            _globalSettings.Magic = true;
            VerifyMatches(@"}", "}");
            VerifyMatches(@"\v}", "}");
            VerifyMatches(@"{hello}", "{hello}");
        }

        /// <summary>
        /// The "\{}" is an exact match count
        /// </summary>
        [Test]
        public void Count_Exact()
        {
            VerifyMatches(@"a\{2}", "aa", "aaa");
            VerifyMatches(@"\va{2}", "aa", "aaa");
            VerifyNotMatches(@"a\{2}", "a");
            VerifyMatchIs(@"a\{2}", "baad", "aa");
        }

        /// <summary>
        /// The "\{,}" is a range
        /// </summary>
        [Test]
        public void Count_Range()
        {
            VerifyMatches(@"a\{2,3}", "aa", "aaa");
            VerifyMatches(@"\va{2,3}", "aa", "aaa");
            VerifyNotMatches(@"a\{2,3}", "a");
            VerifyMatchIs(@"a\{2,3}", "baaad", "aaa");
        }

        /// <summary>
        /// It's Ok for the final } to be escaped with a \ as well
        /// </summary>
        [Test]
        public void Count_ExtraBackslash()
        {
            VerifyMatches(@"a\{2\}", "aa");
            VerifyNotMatches(@"a\{2\}", "a");
        }

        /// <summary>
        /// A \t should be able to match a tab
        /// </summary>
        [Test]
        public void Tab()
        {
            VerifyMatches(@"\t", "hello\tworld", "\t");
        }

        /// <summary>
        /// A \n should be able to match a tab
        /// </summary>
        [Test]
        public void NewLine()
        {
            VerifyMatches(@"\n", "hello" + Environment.NewLine);
        }
    }
}
