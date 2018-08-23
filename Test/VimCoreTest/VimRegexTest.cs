using System;
using System.Linq;
using Microsoft.FSharp.Core;
using Xunit;
using Vim.Extensions;
using Moq;

namespace Vim.UnitTest
{
    public abstract class VimRegexTest
    {
        private static readonly string[] s_lowerCaseLetters = TestConstants.LowerCaseLetters.Select(x => x.ToString()).ToArray();
        private static readonly string[] s_upperCaseLetters = TestConstants.UpperCaseLetters.Select(x => x.ToString()).ToArray();
        private static readonly string[] s_digits = TestConstants.Digits.Select(x => x.ToString()).ToArray();
        private readonly IVimGlobalSettings _globalSettings;

        protected VimRegexTest()
        {
            // This can't be simplified due to a bug in Roslyn. Suppressing the suggestion
            // until fixed
            // https://github.com/dotnet/roslyn/issues/23368
#pragma warning disable IDE0017 
            _globalSettings = new GlobalSettings();
#pragma warning restore IDE0017
            _globalSettings.IgnoreCase = true;
            _globalSettings.SmartCase = false;
        }

        private FSharpOption<VimRegex> Create(string pattern)
        {
            return VimRegexFactory.CreateForSettings(pattern, _globalSettings);
        }

        private static void VerifyRegex(string vimPattern, string regexPattern)
        {
            VerifyRegex(VimRegexOptions.Default, vimPattern, regexPattern);
        }

        private static void VerifyRegex(VimRegexOptions options, string vimPattern, string regexPattern)
        {
            var vimRegex = VimRegexFactory.Create(vimPattern, options).AssertSome();
            Assert.Equal(regexPattern, vimRegex.RegexPattern);
        }

        private void VerifyMatches(string pattern, params string[] inputArray)
        {
            VerifyMatches(VimRegexOptions.Default, pattern, inputArray);
        }

        private void VerifyMatches(VimRegexOptions options, string pattern, params string[] inputArray)
        {
            var opt = VimRegexFactory.Create(pattern, options);
            Assert.True(opt.IsSome());
            var regex = opt.Value;
            foreach (var cur in inputArray)
            {
                Assert.True(regex.IsMatch(cur));
            }
        }

        private void VerifyNotRegex(string pattern)
        {
            Assert.True(Create(pattern).IsNone());
        }

        private void VerifyNotMatches(string pattern, params string[] inputArray)
        {
            VerifyNotMatches(VimRegexOptions.Default, pattern, inputArray);
        }

        private void VerifyNotMatches(VimRegexOptions options, string pattern, params string[] inputArray)
        {
            var opt = VimRegexFactory.Create(pattern, options);
            Assert.True(opt.IsSome());
            var regex = opt.Value;
            foreach (var cur in inputArray)
            {
                Assert.False(regex.IsMatch(cur));
            }
        }

        private void VerifyMatchIs(string pattern, string input, string toMatch)
        {
            var regex = Create(pattern);
            Assert.True(regex.IsSome());
            var match = regex.Value.Regex.Match(input);
            Assert.True(match.Success);
            Assert.Equal(toMatch, match.Value);
        }

        private void VerifyMatchesAt(VimRegexOptions options, string pattern, string input, params Tuple<int, int>[] spans)
        {
            var opt = VimRegexFactory.Create(pattern, options);
            Assert.True(opt.IsSome());
            var regex = opt.Value;
            var matches = regex.Regex.Matches(input);
            var count = matches.Cast<object>().Count();
            Assert.Equal(count, spans.Length);
            for (int i = 0; i < count; i++)
            {
                var position = spans[i].Item1;
                var length = spans[i].Item2;
                var match = matches[i];
                Assert.Equal(position, match.Index);
                Assert.Equal(length, match.Length);
            }
        }

        public sealed class BracketTest : VimRegexTest
        {
            /// <summary>
            /// If there is an unmatched bracket then it is matched literally
            /// </summary>
            [Fact]
            public void OpenBracketMatchesLiterally()
            {
                VerifyRegex(VimRegexOptions.NoMagic, "[", @"\[");
                VerifyMatches(VimRegexOptions.NoMagic, "[", "[", "int[", "][");
                VerifyMatches(VimRegexOptions.Default, "[", "[", "int[", "][");
            }

            /// <summary>
            /// If there is an unmatched bracket then it is matched literally
            /// </summary>
            [Fact]
            public void CloseBracketMatchesLiterally()
            {
                VerifyMatches(VimRegexOptions.NoMagic, "]", "]", "int[]", "][");
                VerifyMatches(VimRegexOptions.Default, "]", "]", "int[]", "][");
            }

            [Fact]
            public void PairedBracketWithNoContentMatchesLiterally()
            {
                VerifyMatches(VimRegexOptions.NoMagic, "[]", "[]", "int[]");
                VerifyMatches(VimRegexOptions.Default, "[]", "[]", "int[]");
                VerifyNotMatches(VimRegexOptions.Default, "[]", "[ ]", "a", "");
            }

            [Fact]
            public void NormalMatched()
            {
                VerifyMatches(VimRegexOptions.Default, "[ab]", "a", "b", "ab");
                VerifyNotMatches(VimRegexOptions.Default, "[ab]", "[", "]");
                VerifyMatches(VimRegexOptions.NoMagic, "[ab]", "[ab]");
                VerifyMatches(VimRegexOptions.NoMagic, @"\[ab]", "a", "b", "ab");
            }
        }

        public sealed class CaseSensitiveTest : VimRegexTest
        {
            /// <summary>
            /// Make sure the parsing is case sensitive by default
            /// </summary>
            [Fact]
            public void RespectIgnoreCase()
            {
                VerifyMatches("a", "a");
                VerifyNotMatches("a", "A");
                VerifyMatches("b", "b");
                VerifyNotMatches("b", "B");
            }

            [Fact]
            public void SensitiveSpecifier()
            {
                VerifyMatches(@"\Ca", "a");
                VerifyMatches(@"\Cb", "b");
                VerifyNotMatches(@"\Ca", "A");
                VerifyNotMatches(@"\Cb", "B");
            }

            /// <summary>
            /// Make sure we support the \C specifier anywhere in the search string
            /// </summary>
            [Fact]
            public void SensitiveSpecifierInMiddleOfString()
            {
                var regex = Create(@"d\Cog").Value;
                Assert.True(regex.CaseSpecifier.IsOrdinalCase);
                Assert.Equal(@"d\Cog", regex.VimPattern);
                Assert.Equal("dog", regex.RegexPattern);
            }

            /// <summary>
            /// The \C modifier takes precedence over ignorecase option
            /// </summary>
            [Fact]
            public void SensitiveSpecifierBeatsIgonreCase()
            {
                VerifyMatches(VimRegexOptions.IgnoreCase, @"\Ca", "a");
                VerifyMatches(VimRegexOptions.IgnoreCase, @"\Cb", "b");
                VerifyNotMatches(VimRegexOptions.IgnoreCase, @"\Ca", "A");
                VerifyNotMatches(VimRegexOptions.IgnoreCase, @"\Cb", "B");
            }

            [Fact]
            public void InsensitiveSpecifier()
            {
                VerifyMatches(@"\ca", "a", "A");
                VerifyMatches(@"\cb", "b", "B");
            }

            /// <summary>
            /// Make sure we support the \c specifier anywhere in the search string
            /// </summary>
            [Fact]
            public void InsensitiveSpecifierInMiddleOfString()
            {
                var regex = Create(@"D\cOG").Value;
                Assert.True(regex.CaseSpecifier.IsIgnoreCase);
                Assert.Equal(@"D\cOG", regex.VimPattern);
                Assert.Equal("DOG", regex.RegexPattern);
            }

            /// <summary>
            /// The \c modifier takes precedence over the ignore case option
            /// </summary>
            [Fact]
            public void InsensitiveSpecifierBeatsDefault()
            {
                VerifyMatches(@"\ca", "a", "A");
                VerifyMatches(@"\cb", "b", "B");
            }

            /// <summary>
            /// SmartCase should match both if only lower
            /// </summary>
            [Fact]
            public void Simple()
            {
                VerifyMatches(VimRegexOptions.SmartCase | VimRegexOptions.IgnoreCase, "a", "A", "a");
                VerifyMatches(VimRegexOptions.SmartCase | VimRegexOptions.IgnoreCase, "b", "b", "B");
            }

            /// <summary>
            /// SmartCase is case sensitive if any are upper
            /// </summary>
            [Fact]
            public void WithUpper()
            {
                VerifyMatches(VimRegexOptions.SmartCase, "A", "A");
                VerifyNotMatches(VimRegexOptions.SmartCase, "A", "a");
                VerifyMatches(VimRegexOptions.SmartCase, "B", "B");
                VerifyNotMatches(VimRegexOptions.SmartCase, "B", "b");
            }

            /// <summary>
            /// The \c modifier beats smart case as well
            /// </summary>
            [Fact]
            public void InsensitiveSpecifierWins()
            {
                VerifyMatches(VimRegexOptions.SmartCase, @"\cFoo", "foo", "FOO", "fOO");
                VerifyMatches(VimRegexOptions.SmartCase, @"\cBar", "BAR", "bar");
            }

            /// <summary>
            /// The \C modifier beats smart case as well
            /// </summary>
            [Fact]
            public void SensitiveSpecifierWins()
            {
                var options = VimRegexOptions.SmartCase | VimRegexOptions.IgnoreCase;
                VerifyMatches(options, @"\CFOO", "FOO");
                VerifyNotMatches(options, @"\CFOO", "foo");
                VerifyMatches(options, @"\CBAR", "BAR");
                VerifyNotMatches(options, @"\CBAR", "bar");
            }
        }

        public sealed class ReplaceTest : VimRegexTest
        {
            private readonly IRegisterMap _registerMap;
            private readonly Mock<IClipboardDevice> _clipboardDevice;

            public ReplaceTest()
            {
                _clipboardDevice = new Mock<IClipboardDevice>(MockBehavior.Loose);
                Func<FSharpOption<string>> func = () => FSharpOption<string>.None;
                _registerMap = new RegisterMap(
                    new VimData(_globalSettings),
                    _clipboardDevice.Object,
                    func.ToFSharpFunc());
            }

            private void VerifyReplace(string pattern, string input, string replace, string result, VimRegexReplaceCount count = null)
            {
                count = count ?? VimRegexReplaceCount.All;
                VerifyReplace(VimRegexOptions.Default, pattern, input, replace, result, count);
            }

            private void VerifyReplace(VimRegexOptions options, string pattern, string input, string replace, string result, VimRegexReplaceCount count = null)
            {
                count = count ?? VimRegexReplaceCount.All;
                var regex = VimRegexFactory.Create(pattern, options);
                Assert.True(regex.IsSome());

                var noMagic = VimRegexOptions.NoMagic == (options & VimRegexOptions.NoMagic);
                var replaceData = new VimRegexReplaceData("xyzzy", Environment.NewLine, !noMagic, count);
                Assert.Equal(result, regex.Value.Replace(input, replace, replaceData, _registerMap));
            }

            /// <summary>
            /// Simple no-magic replace
            /// </summary>
            [Fact]
            public void Replace1()
            {
                VerifyReplace(@"foo", "foo bar", "bar", "bar bar");
                VerifyReplace(@"foo", "foo bar baz", "bar", "bar bar baz");
            }

            [Fact]
            public void Replace2()
            {
                VerifyReplace(@"a\|b", "cat", "o", "cot");
            }

            [Fact]
            public void Replace3()
            {
                VerifyReplace(@"\<foo\>", "foo bar", "bar", "bar bar");
                VerifyReplace(@"\<foo\>", "foobar", "bar", "foobar");
                VerifyReplace(@"\<foo\>", "foo bar baz", "bar", "bar bar baz");
            }

            [Fact]
            public void Replace4()
            {
                VerifyReplace(@"(ab)", "foo(ab)", "()", "foo()");
                VerifyReplace(@"foo(ab)", "foo(ab)", "()", "()");
                VerifyReplace(@"foo()", "foo(ab)", "()", "foo(ab)");
            }

            [Fact]
            public void Replace5()
            {
                VerifyReplace(@"\(ab\)", "ab", "", "");
                VerifyReplace(@"\(ab\)", "cab", "", "c");
                VerifyReplace(@"\(ab\)", "c(ab)", "", "c()");
            }

            [Fact]
            public void Replace6()
            {
                VerifyReplace(@"foo\(\.*\)", "foobar", @"\1", "bar");
                VerifyReplace(@"jaz\(\.*\)", "jaz123", @"\1", "123");
            }

            [Fact]
            public void Replace7()
            {
                VerifyReplace(@"\(\.*\)b\(\.*\)", "abc", @"\2", "ac");
                VerifyReplace(@"\(\.*\)b\(\.*\)", "abc", @"\1\2", "ac");
                VerifyReplace(@"\(\.*\)b\(\.*\)", "abc", @"\1blah\2", "ablahc");
            }

            /// <summary>
            /// Escaped back slashes should appear as normal back slashes in the replacement string
            /// </summary>
            [Fact]
            public void EscapedBackSlashes()
            {
                VerifyReplace("b", "abc", @"\\\\", @"a\\c");
            }

            /// <summary>
            /// Don't treat an escaped backslash in front of a 'n' character as a new line. 
            /// 
            /// Issue #779
            /// </summary>
            [Fact]
            public void EscapedBackSlashNotNewLine()
            {
                VerifyReplace("b", "abc", @"\\n\\", @"a\n\c");
                VerifyReplace("$", "dog", @"\\n\\", @"dog\n\");
            }

            /// <summary>
            /// When the '&' character is used in the replacement string it should replace with 
            /// the entire matched pattern
            /// </summary>
            [Fact]
            public void Ampersand_Magic()
            {
                VerifyReplace("a", "cat", @"o&", "coat");
                VerifyReplace(@"a\+", "caat", @"o&", "coaat");
            }

            /// <summary>
            /// When there is no magic then the ampersand is not special and should replace 
            /// as normal
            /// </summary>
            [Fact]
            public void Ampersand_NoMagic()
            {
                VerifyReplace(VimRegexOptions.NoMagic, "a", "cat", @"o&", "co&t");
                VerifyReplace(VimRegexOptions.NoMagic, @"a\+", "caat", @"o&", "co&t");
            }

            /// <summary>
            /// When escaped with magic it should behave simply as an ampersand
            /// </summary>
            [Fact]
            public void EscapedAmpersand_Magic()
            {
                VerifyReplace("a", "cat", @"o\&", "co&t");
                VerifyReplace(@"a\+", "caat", @"o\&", "co&t");
            }

            /// <summary>
            /// When escaped with nomagic it should replace with the entire
            /// matched pattern
            /// </summary>
            [Fact]
            public void EscapedAmpersand_NoMagic()
            {
                VerifyReplace(VimRegexOptions.NoMagic, "a", "cat", @"o\&", "coat");
                VerifyReplace(VimRegexOptions.NoMagic, @"a\+", "caat", @"o\&", "coaat");
            }

            /// <summary>
            /// When the '~' character is used in the replacement string it should replace with 
            /// the previous replacement
            /// </summary>
            [Fact]
            public void Tilde_Magic()
            {
                VerifyReplace("a", "cat", @"o~", "coxyzzyt");
                VerifyReplace(@"a\+", "caat", @"o~", "coxyzzyt");
            }

            /// <summary>
            /// When there is no magic then the tilde is not special and should replace 
            /// as normal
            /// </summary>
            [Fact]
            public void Tilde_NoMagic()
            {
                VerifyReplace(VimRegexOptions.NoMagic, "a", "cat", @"o~", "co~t");
                VerifyReplace(VimRegexOptions.NoMagic, @"a\+", "caat", @"o~", "co~t");
            }

            /// <summary>
            /// When escaped with magic it should behave simply as an tilde
            /// </summary>
            [Fact]
            public void EscapedTilde_Magic()
            {
                VerifyReplace("a", "cat", @"o\~", "co~t");
                VerifyReplace(@"a\+", "caat", @"o\~", "co~t");
            }

            /// <summary>
            /// When escaped with nomagic it should replace with the previous
            /// replacement
            /// </summary>
            [Fact]
            public void EscapedTilde_NoMagic()
            {
                VerifyReplace(VimRegexOptions.NoMagic, "a", "cat", @"o\~", "coxyzzyt");
                VerifyReplace(VimRegexOptions.NoMagic, @"a\+", "caat", @"o\~", "coxyzzyt");
            }

            /// <summary>
            /// The '\0' pattern is used to match the entire matched pattern.  It acts exactly 
            /// as '&' does in the replacement string
            /// </summary>
            [Fact]
            public void EscapedZero()
            {
                VerifyReplace("a", "cat", @"o\0", "coat");
                VerifyReplace(@"a\+", "caat", @"o\0", "coaat");
            }

            /// <summary>
            /// The '\t' replacement string should insert a tab
            /// </summary>
            [Fact]
            public void Escaped_T()
            {
                VerifyReplace("a", "a", @"\t", "\t");
                VerifyReplace("  ", "    ", @"\t", "\t\t");
            }

            /// <summary>
            /// The '\r' replacement should insert a carriage return
            /// </summary>
            [Fact]
            public void Escaped_R()
            {
                VerifyReplace("a", "a", @"\r", Environment.NewLine);
            }

            [Fact]
            public void Newline()
            {
                VerifyRegex(@"\n", VimRegexFactory.NewLineRegex);
                VerifyReplace(@"\n", "hello\nworld", " ", "hello world");
                VerifyReplace(@"\n", "hello\r\nworld", " ", "hello world");
            }

            [Fact]
            public void Multiline()
            {
                VerifyRegex(@"abc\ndef", "abc" + VimRegexFactory.NewLineRegex + "def");
                VerifyReplace(@"abc\ndef", "abc\ndef", "xyzzy", "xyzzy");
                VerifyReplace(@"abc\ndef", "abc\r\ndef", "xyzzy", "xyzzy");
            }

            [Fact]
            public void UpperCaseChar()
            {
                VerifyReplace("cat", "cat dog", @"\u&", "Cat dog");
                VerifyReplace("cat", "cat dog", @"\ubat", "Bat dog");
                VerifyReplace(@"\(cat\)", "cat dog", @"\u\1", "Cat dog");
                VerifyReplace("cat", "cat dog", @"\u\0", "Cat dog");
            }

            [Fact]
            public void UpperCaseUntil()
            {
                VerifyReplace("cat", "cat dog", @"\U&", "CAT dog");
                VerifyReplace("cat", "cat dog", @"\U&&", "CATCAT dog");
                VerifyReplace("cat", "cat dog", @"\U&\e&", "CATcat dog");
            }

            [Fact]
            public void LowerCaseChar()
            {
                VerifyReplace("CAT", "CAT dog", @"\l&", "cAT dog");
                VerifyReplace("CAT", "CAT dog", @"\l&s", "cATs dog");
                VerifyReplace("CAT", "CAT dog", @"\l\0s", "cATs dog");
            }

            [Fact]
            public void LowerCaseUntil()
            {
                VerifyReplace("CAT", "CAT dog", @"\L&", "cat dog");
                VerifyReplace("CAT", "CAT dog", @"\L&s", "cats dog");
                VerifyReplace("CAT", "CAT dog", @"\L\0s", "cats dog");
            }

            [Fact]
            public void BadGroupSpecifier()
            {
                VerifyReplace("fish", "fish tree", @"let\3", "let tree");
            }

            [Fact]
            public void BadPattern()
            {
                VerifyReplace("fishy", "fish tree", @"let\3", "fish tree");
            }

            [Fact]
            public void InsertNewLine()
            {
                VerifyReplace(@"o", "dog", @"\r", "d" + Environment.NewLine + "g");
                VerifyReplace(@"o", "dog", @"\" + CharCodes.Enter, "d" + Environment.NewLine + "g");
                VerifyReplace(@"o", "dog", @"" + CharCodes.Enter, "d" + Environment.NewLine + "g");
            }

            /// <summary>
            /// The \n replace actually inserts the null character and not a new line 
            /// into the file
            /// </summary>
            [Fact]
            public void InsertNullCharacter()
            {
                VerifyReplace(@"o", "dog", @"\n", "d" + (char)0 + "g");
            }

            [Fact]
            public void InsertRegisterValue()
            {
                _registerMap.SetRegisterValue('c', "fish");
                VerifyReplace("o", "dog", @"\=@c", "dfishg");
                VerifyReplace("o", "doog", @"\=@c", "dfishfishg");
            }

            [Fact]
            public void NonGreedyRegexReplace()
            {
                VerifyReplace(@"Task<\(.\{-}\)>", "public Task<string> M()", @"\1", "public string M()");
                VerifyReplace(@"a\{-1,2}", "aaaaa", "b", "baaaa", VimRegexReplaceCount.One);
            }

            /// <summary>
            /// Verify that the start and end match patterns ('\zs' and '\ze')
            /// can be used
            /// </summary>
            [Fact]
            public void WithStartEndMatch()
            {
                VerifyReplace(@"\<foo\zsBar\zeBaz\>", "[fooBarBaz]", "Qux", "[fooQuxBaz]");
            }
        }

        public sealed class MiscTest : VimRegexTest
        {
            [Fact]
            public void Case_Simple()
            {
                VerifyMatches(VimRegexOptions.IgnoreCase, "a", "a", "A");
                VerifyMatches("b", "b", "b");
            }

            [Fact]
            public void CreateRegexOptions_NoMagic()
            {
                _globalSettings.Magic = false;
                var options = VimRegexFactory.CreateRegexOptions(_globalSettings);
                Assert.Equal(VimRegexOptions.NoMagic, options & VimRegexOptions.NoMagic);
            }

            /// <summary>
            /// Magic should the default setting
            /// </summary>
            [Fact]
            public void Magic_ShouldBeDefault()
            {
                VerifyMatches(".", "a", "b", "c");
            }

            /// <summary>
            /// The \m should override the NoMagic option on the regex
            /// </summary>
            [Fact]
            public void Magic_MagicSpecifierHasPrecedence()
            {
                VerifyMatches(VimRegexOptions.NoMagic, @"\m.", "a", "b", "c");
            }

            /// <summary>
            /// The \M should oveerride the default VimRegexOptions
            /// </summary>
            [Fact]
            public void Magic_NoMagicSpecifierHasPrecedence()
            {
                VerifyNotMatches(@"\M.", "a", "b", "c");
                VerifyMatches(@"\M\.", "a", "b", "c");
            }

            /// <summary>
            /// The \M should oveerride the default VimRegexOptions
            /// </summary>
            [Fact]
            public void Magic_MagicSpecifierInMiddle()
            {
                VerifyMatches(VimRegexOptions.NoMagic, @"a\m.", "ab", "ac");
            }

            [Fact]
            public void Magic_NoMagicSpecifierInMiddle()
            {
                VerifyNotMatches(@"a\M.", "ab", "ac");
                VerifyMatches(@"a\M.", "a.");
            }

            [Fact]
            public void VeryMagic1()
            {
                VerifyMatches(VimRegexOptions.NoMagic, @"\v.", "a", "b");
            }

            [Fact]
            public void VeryMagic2()
            {
                VerifyNotMatches(@"\V.", "a", "b");
                VerifyMatches(@"\V\.", "a", "b");
            }

            [Fact]
            public void VeryNoMagicDotIsNotSpecial()
            {
                VerifyNotMatches(@"\V.", "a");
                VerifyMatches(@"\V.", ".");
            }

            [Fact]
            public void ItemStar1()
            {
                VerifyMatchIs(@"ab*", "abb", "abb");
                VerifyMatchIs(@"ab*", "cab", "ab");
                VerifyMatchIs(@"ab*", "cabb", "abb");
            }

            [Fact]
            public void ItemStar2()
            {
                VerifyMatchIs(@"\Mab*", "ab*", "ab*");
                VerifyMatchIs(@"\Mab\*", "ab", "ab");
                VerifyMatchIs(@"\Mab\*", "caabb", "a");
                VerifyMatchIs(@"\Mab\*", "cabb", "abb");
            }

            [Fact]
            public void ItemStar3()
            {
                VerifyRegex(@"\mab*", @"ab*");
                VerifyMatchIs(@"\mab*", "abb", "abb");
                VerifyMatchIs(@"\mab*", "cab", "ab");
                VerifyMatchIs(@"\mab*", "cabb", "abb");
            }

            [Fact]
            public void ItemQuestion1()
            {
                VerifyMatchIs(@"ab?", "ab?", "ab?");
                VerifyMatchIs(@"ab\?", "ab", "ab");
                VerifyMatchIs(@"ab\?", "abc", "ab");
                VerifyMatchIs(@"ab\?", "adc", "a");
            }

            [Fact]
            public void ItemQuestion2()
            {
                VerifyMatchIs(@"\Mab?", "ab?", "ab?");
                VerifyMatchIs(@"\Mab\?", "ab", "ab");
                VerifyMatchIs(@"\Mab\?", "abc", "ab");
            }

            [Fact]
            public void ItemQuestion3()
            {
                VerifyMatchIs(@"\vab?", "ad", "a");
                VerifyMatchIs(@"\vab?", "ab", "ab");
                VerifyMatchIs(@"\vab?", "abc", "ab");
            }

            [Fact]
            public void ItemEqual1()
            {
                VerifyMatchIs(@"ab\=", "a", "a");
                VerifyMatchIs(@"ab\=", "ab", "ab");
                VerifyMatchIs(@"ab\=", "abc", "ab");
            }

            [Fact]
            public void ItemEqual2()
            {
                VerifyMatchIs(@"\Mab=", "ab=", "ab=");
                VerifyMatchIs(@"\Mab\=", "ab", "ab");
                VerifyMatchIs(@"\Mab\=", "abc", "ab");
                VerifyMatchIs(@"\Mab\=", "adc", "a");
            }

            [Fact]
            public void ItemEqual3()
            {
                VerifyMatchIs(@"\vab=", "a", "a");
                VerifyMatchIs(@"\vab=", "ab", "ab");
                VerifyMatchIs(@"\vab=", "abc", "ab");
            }

            [Fact]
            public void AtomHat1()
            {
                VerifyMatches(@"^m", "m");
                VerifyMatches(@"^", "aoeu");
            }

            [Fact]
            public void AtomHat2()
            {
                VerifyMatches(@"\M^m", "m");
                VerifyMatches(@"\M^", "aoeu");
            }

            [Fact]
            public void AtomHat3()
            {
                VerifyMatches(@"\v^m", "m");
                VerifyMatches(@"\v^", "aoeu");
            }

            /// <summary>
            /// Only use ^ as magic at the start of a pattern
            /// </summary>
            [Fact]
            public void AtomHat4()
            {
                VerifyMatchIs(@"a^", "a^", "a^");
                VerifyMatchIs(@"\Ma^", "a^", "a^");
            }

            [Fact]
            public void AtomHat5()
            {
                VerifyMatches(@"\V^m", "a^m");
                VerifyMatches(@"\V^", "a^aoeu");
            }

            [Fact]
            public void AtomBackslashHat1()
            {
                VerifyMatchIs(@"\^", "^", "^");
                VerifyMatchIs(@"\^a", "^a", "^a");
                VerifyMatchIs(@"b\^a", "b^a", "b^a");
                VerifyNotMatches(@"\^", "a");
            }

            [Fact]
            public void AtomBackslashHat2()
            {
                VerifyMatches(@"\V\^m", "m");
                VerifyMatches(@"\V\^", "aoeu");
            }

            [Fact]
            public void AtomBackslashHat3()
            {
                VerifyMatchIs(@"\M\^", "^", "^");
                VerifyMatchIs(@"\M\^a", "^a", "^a");
                VerifyMatchIs(@"\Mb\^a", "b^a", "b^a");
                VerifyNotMatches(@"\M\^", "a");
            }

            [Fact]
            public void AtomBackslashHat4()
            {
                VerifyRegex(@"\v\^", @"\^");
                VerifyMatchIs(@"\v\^", "^", "^");
                VerifyMatchIs(@"\v\^a", "^a", "^a");
                VerifyMatchIs(@"\vb\^a", "b^a", "b^a");
                VerifyNotMatches(@"\v\^", "a");
            }

            [Fact]
            public void AtomBackslashUnderscoreHat1()
            {
                VerifyNotRegex(@"\_");
                VerifyNotRegex(@"ab\_");
            }

            [Fact]
            public void AtomBackslashUnderscoreHat2()
            {
                VerifyNotRegex(@"\M\_");
                VerifyNotRegex(@"\M\_");
            }

            [Fact]
            public void AtomBackslashUnderscoreHat3()
            {
                VerifyMatches(@"\_^", "abc");
                VerifyMatches(@"\_^", "");
                VerifyMatches(@"c\?\_^ab", "ab");
            }

            [Fact]
            public void AtomBackslashUnderscoreHat4()
            {
                VerifyMatches(@"\M\_^", "abc");
                VerifyMatches(@"\M\_^", "");
                VerifyMatches(@"\Mc\?\_^ab", "ab");
            }

            [Fact]
            public void AtomDollar1()
            {
                VerifyMatches(@"$", "");
                VerifyMatches(@"$", "aoe");
                VerifyMatches(@"\M$", "aoe");
            }

            [Fact]
            public void AtomDollar2()
            {
                VerifyMatchIs(@"a$", "baaa", "a");
                VerifyMatchIs(@"a*$", "baaa", "aaa");
            }

            [Fact]
            public void AtomDollar3()
            {
                VerifyMatchIs(@"\Ma$", "baaa", "a");
                VerifyMatchIs(@"\Ma\*$", "baaa", "aaa");
            }

            [Fact]
            public void AtomDollar4()
            {
                VerifyMatchIs(@"\Ma$b", "a$bz", "a$b");
                VerifyMatchIs(@"\Ma\*$b", "aa$bz", "aa$b");
            }

            [Fact]
            public void AtomBackslashDollar1()
            {
                VerifyNotMatches(@"\$", "");
                VerifyNotMatches(@"\$", "aoe");
                VerifyNotMatches(@"\M\$", "aoe");
            }

            [Fact]
            public void AtomBackslashDollar2()
            {
                VerifyMatchIs(@"\$", "$", "$");
                VerifyMatchIs(@"\$ab", "$ab", "$ab");
            }

            [Fact]
            public void AtomBackslashUnderscoreDollar1()
            {
                VerifyMatchIs(@"\$", "$", "$");
                VerifyMatchIs(@"\$ab", "$ab", "$ab");
            }

            [Fact]
            public void AtomBackslashUnderscoreDollar2()
            {
                VerifyNotRegex(@"\_");
            }

            [Fact]
            public void AtomBackslashUnderscoreDollar3()
            {
                VerifyMatchIs(@"\Ma\_$", "baaa", "a");
                VerifyMatchIs(@"\Ma\*\_$", "baaa", "aaa");
            }

            [Fact]
            public void WordBoundary1()
            {
                VerifyNotMatches(@"\<word", "aword");
                VerifyNotMatches(@"\M\<word", "aword");
            }

            [Fact]
            public void WordBoundary2()
            {
                VerifyMatches(@"\<word", "a word");
                VerifyMatches(@"\M\<word", "a word");
            }

            [Fact]
            public void WordBoundary3()
            {
                VerifyMatchIs(@"\<word", "a word", "word");
                VerifyMatchIs(@"\M\<word", "a word", "word");
            }

            [Fact]
            public void WordBoundary4()
            {
                VerifyNotMatches(@"word\>", "words");
                VerifyNotMatches(@"\Mword\>", "words");
            }

            [Fact]
            public void WordBoundary5()
            {
                VerifyMatches(@"word\>", "a word again");
                VerifyMatches(@"\Mword\>", "a word again");
            }

            /// <summary>
            /// Boundary at the end of a line
            /// </summary>
            [Fact]
            public void WordBoundary6()
            {
                VerifyMatches(@"word\>", "a word");
                VerifyMatches(@"\Mword\>", "a word");
            }

            [Fact]
            public void Grouping1()
            {
                VerifyMatchIs(@"(a)", "foo(a)", "(a)");
                VerifyMatchIs(@"(abc)", "foo(abc)", "(abc)");
            }

            [Fact]
            public void Grouping2()
            {
                VerifyMatchIs(@"\(ab\)", "foo(ab)", "ab");
                VerifyMatchIs(@"\(ab\)", "abc", "ab");
            }

            [Fact]
            public void Grouping3()
            {
                VerifyMatchIs(@"\v(a)", "foo(a)", "a");
                VerifyMatchIs(@"\v(abc)", "foo(abc)", "abc");
            }

            [Fact]
            public void Grouping4()
            {
                var regex = Create(@"\(");
                Assert.True(regex.IsNone());
            }

            /// <summary>
            /// Make sure that \1 can be used to match the previous group specified
            /// </summary>
            [Fact]
            public void Group_MatchPreviousGroup()
            {
                VerifyMatches(@"\(dog\)::\1", "dog::dog");
                VerifyMatches(@"\(dog\)::cat::\1", "dog::cat::dog");
                VerifyNotMatches(@"\(dog\)::\1", "dog::cat");
            }

            [Fact]
            public void Separator1()
            {
                VerifyMatchIs(@"a\|b", "foob", "b");
                VerifyMatchIs(@"a\|b", "acat", "a");
            }

            [Fact]
            public void Separator2()
            {
                VerifyMatchIs(@"ab\|c", "abod", "ab");
                VerifyMatchIs(@"ab\|c", "babod", "ab");
                VerifyMatchIs(@"ab\|c", "bacod", "c");
            }

            [Fact]
            public void Separator3()
            {
                VerifyMatchIs(@"\vab|c", "abod", "ab");
                VerifyMatchIs(@"\vab|c", "babod", "ab");
                VerifyMatchIs(@"\vab|c", "bacod", "c");
            }

            [Fact]
            public void CharacterSequence1()
            {
                VerifyMatches(@"[abc]", "a", "b", "c");
                VerifyMatches(@"\M\[abc]", "a", "b", "c");
            }

            [Fact]
            public void CharacterSequence2()
            {
                VerifyMatches(@"[a-z]", "a", "b", "c", "z");
                VerifyMatches(@"\M\[a-c]", "a", "b", "c");
            }

            [Fact]
            public void AtomDigits()
            {
                VerifyMatches(@"\d", "1", "2");
                VerifyMatches(@"\M\d", "1", "2");
                VerifyNotMatches(@"\d", "a");
            }

            [Fact]
            public void AtomNonDigits()
            {
                VerifyMatches(@"\D", "a", "b");
                VerifyMatches(@"\M\D", "a", "b");
                VerifyNotMatches(@"\M\D", "1", "2");
            }

            [Fact]
            public void AtomWordCharacter()
            {
                VerifyMatches(@"\w", "a", "A", "_", "1", "4");
                VerifyMatches(@"\M\w", "a", "A", "_", "1", "4");
                VerifyNotMatches(@"\w", "%");
                VerifyNotMatches(@"\M\w", "%");
            }

            [Fact]
            public void AtomNonWordCharacter()
            {
                VerifyNotMatches(@"\W", "a", "A", "_", "1", "4");
                VerifyNotMatches(@"\M\W", "a", "A", "_", "1", "4");
                VerifyMatches(@"\W", "%");
                VerifyMatches(@"\M\W", "%");
            }

            [Fact]
            public void AtomHexDigit()
            {
                VerifyMatches(@"\x", "0123456789abcdef".Select(x => x.ToString()).ToArray());
                VerifyNotMatches(@"\x", "%", "^", "g", "h");
            }

            [Fact]
            public void AtomNonHexDigit()
            {
                VerifyNotMatches(@"\X", "0123456789abcdef".Select(x => x.ToString()).ToArray());
                VerifyMatches(@"\X", "%", "^", "g", "h");
            }

            [Fact]
            public void AtomOctal()
            {
                VerifyMatches(@"\o", "01234567".Select(x => x.ToString()).ToArray());
                VerifyNotMatches(@"\o", "%", "^", "g", "h", "8", "9");
            }

            [Fact]
            public void AtomNonOctal()
            {
                VerifyNotMatches(@"\O", "01234567".Select(x => x.ToString()).ToArray());
                VerifyMatches(@"\O", "%", "^", "g", "h", "8", "9");
            }

            [Fact]
            public void AtomHeadOfWord()
            {
                VerifyMatches(@"\h", s_lowerCaseLetters);
                VerifyMatches(@"\h", s_upperCaseLetters);
                VerifyMatches(@"\h", "_");
                VerifyNotMatches(@"\h", s_digits);
            }

            [Fact]
            public void AtomNonHeadOfWord()
            {
                VerifyNotMatches(@"\H", s_lowerCaseLetters);
                VerifyNotMatches(@"\H", s_upperCaseLetters);
                VerifyNotMatches(@"\H", "_");
                VerifyMatches(@"\H", s_digits);
            }

            [Fact]
            public void AtomAlphabeticChar()
            {
                VerifyMatches(@"\a", s_lowerCaseLetters);
                VerifyMatches(@"\a", s_upperCaseLetters);
                VerifyNotMatches(@"\a", s_digits);
            }

            [Fact]
            public void AtomNonAlphabeticChar()
            {
                VerifyNotMatches(@"\A", s_lowerCaseLetters);
                VerifyNotMatches(@"\A", s_upperCaseLetters);
                VerifyMatches(@"\A", "_");
                VerifyMatches(@"\A", s_digits);
            }

            [Fact]
            public void AtomLowerLetters()
            {
                _globalSettings.IgnoreCase = false;
                VerifyMatches(@"\l", s_lowerCaseLetters);
                VerifyNotMatches(@"\l", s_upperCaseLetters);
                VerifyNotMatches(@"\l", s_digits);
            }

            [Fact]
            public void AtomNonLowerLetters()
            {
                _globalSettings.IgnoreCase = false;
                VerifyNotMatches(@"\L", s_lowerCaseLetters);
                VerifyMatches(@"\L", s_upperCaseLetters);
                VerifyMatches(@"\L", "_");
                VerifyMatches(@"\L", s_digits);
            }

            [Fact]
            public void AtomUpperLetters()
            {
                _globalSettings.IgnoreCase = false;
                VerifyMatches(@"\u", s_upperCaseLetters);
                VerifyNotMatches(@"\u", s_lowerCaseLetters);
                VerifyNotMatches(@"\u", s_digits);
            }

            [Fact]
            public void AtomNonUpperLetters()
            {
                _globalSettings.IgnoreCase = false;
                VerifyNotMatches(@"\U", s_upperCaseLetters);
                VerifyMatches(@"\U", s_lowerCaseLetters);
                VerifyMatches(@"\U", "_");
                VerifyMatches(@"\U", s_digits);
            }

            [Fact]
            public void AtomPlus()
            {
                _globalSettings.Magic = true;
                VerifyMatches(@"a\+", "a", "aa");
                VerifyNotMatches(@"a\+", "b");
                VerifyMatchIs(@"\va+", "aa", "aa");
            }

            [Fact]
            public void AtomCount()
            {
                _globalSettings.Magic = true;
                VerifyMatchIs(@"a\{1}", "aaa", "a");
                VerifyMatchIs(@"a\{1,2}", "aaa", "aa");
                VerifyMatchIs(@"a\{1,3}", "aaa", "aaa");
                VerifyMatchIs(@"a\{2,3}", "aaa", "aaa");
                VerifyNotMatches(@"a\{3}", "a");
            }

            [Fact]
            public void AtomStar_Magic()
            {
                _globalSettings.Magic = true;
                VerifyMatchIs(@"a*", "aa", "aa");
                VerifyMatchIs(@"a\*", "a*", "a*");
            }

            [Fact]
            public void AtomStar_NoMagic()
            {
                _globalSettings.Magic = false;
                VerifyMatchIs(@"a*", "a*", "a*");
                VerifyMatchIs(@"a\*", "aaa", "aaa");
            }

            [Fact]
            public void AtomGroup_GroupAllBut()
            {
                _globalSettings.Magic = true;
                VerifyMatchIs(@"[^""]*b", "acbd", "acb");
                VerifyMatchIs(@"""[^""]*", @"b""cd", @"""cd");
            }

            [Fact]
            public void AtomWhitespace_NoMagic()
            {
                _globalSettings.Magic = false;
                VerifyMatchIs(@"\s", " ", " ");
                VerifyMatchIs(@"hello\sworld", "hello world", "hello world");
                VerifyMatchIs(@"hello\s\*world", "hello   world", "hello   world");
            }

            [Fact]
            public void AtomWhitespace_Magic()
            {
                _globalSettings.Magic = true;
                VerifyMatchIs(@"\s", " ", " ");
                VerifyMatchIs(@"hello\sworld", "hello world", "hello world");
                VerifyMatchIs(@"hello\s*world", "hello   world", "hello   world");
            }

            [Fact]
            public void AtomWhitespace_MultipleWithStarQualifier()
            {
                _globalSettings.Magic = true;
                VerifyMatchIs(@"TCHAR\s\s*buff", "TCHAR buff", "TCHAR buff");
            }

            [Fact]
            public void AtomNonWhitespace_NoMagic()
            {
                _globalSettings.Magic = false;
                VerifyMatchIs(@"\S", "a", "a");
                VerifyMatchIs(@"hello\Sworld", "hello!world", "hello!world");
            }

            [Fact]
            public void AtomNonWhitespace_Magic()
            {
                _globalSettings.Magic = true;
                VerifyMatchIs(@"\S", "a", "a");
                VerifyMatchIs(@"hello\Sworld", "hello!world", "hello!world");
            }

            /// <summary>
            /// The '{' should match as a literal even when magic is on 
            /// </summary>
            [Fact]
            public void AtomOpenCurly_Magic()
            {
                _globalSettings.Magic = true;
                VerifyMatches(@"{", "{");
                VerifyMatches(@"{hello}", "{hello}");
            }

            /// <summary>
            /// The '{' should register as a count open when very magic is on
            /// </summary>
            [Fact]
            public void AtomOpenCurly_VeryMagic()
            {
                _globalSettings.Magic = true;
                VerifyMatches(@"\va{1,3}", "a", "aaa", "aa");
            }

            /// <summary>
            /// Make sure a few items are not actually regexs
            /// </summary>
            [Fact]
            public void AtomOpenCurly_Bad()
            {
                VerifyNotRegex(@"\{");
                VerifyNotRegex(@"\v{");
                VerifyNotRegex(@"\{1");
            }

            /// <summary>
            /// The '}' should register as a literal even when very magic is on. 
            /// </summary>
            [Fact]
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
            [Fact]
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
            [Fact]
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
            [Fact]
            public void Count_ExtraBackslash()
            {
                VerifyMatches(@"a\{2\}", "aa");
                VerifyNotMatches(@"a\{2\}", "a");
            }

            /// <summary>
            /// A \t should be able to match a tab
            /// </summary>
            [Fact]
            public void Tab()
            {
                VerifyMatches(@"\t", "hello\tworld", "\t");
            }

            /// <summary>
            /// A \n should be able to match a newline, any newline 
            /// </summary>
            [Fact]
            public void NewLine_Match()
            {
                VerifyMatches(@"\n", "hello\r\n", "hello\n");
            }

            [Fact]
            public void Newline_DollarSignMatchesEndOfLine()
            {
                VerifyMatches(@"foo$", "foo\r\nbar");
                VerifyMatches(@"foo$", "foo\nbar");
                VerifyMatches(@"foo$", "foo");
            }
        }

        public sealed class OrTest : VimRegexTest
        {
            [Fact]
            public void Simple()
            {
                VerifyRegex(@"a\|b", "a|b");
                VerifyMatches(@"a\|b", "a", "b");
            }

            [Fact]
            public void Grouping()
            {
                VerifyRegex(@"ab\|c", @"ab|c");
                VerifyMatches(@"ab\|c", "ab", "c");
            }

            [Fact]
            public void NewLineOrBrace()
            {
                VerifyRegex(@"^$\|{", @"^" + VimRegexFactory.DollarRegex + @"|\{");
                VerifyMatches(@"^$\|{", "", "blah {");
            }

            /// <summary>
            /// Hat is a zero-width beginning-of-line match
            /// </summary>
            [Fact]
            public void HatFoward()
            {
                VerifyMatchesAt(VimRegexOptions.Default, "^", "abc\r\ndef\r\n",
                    Tuple.Create(0, 0), Tuple.Create(5, 0), Tuple.Create(10, 0));
            }

            /// <summary>
            /// Dollar is a zero-width end-of-line match
            /// </summary>
            [Fact]
            public void DollarFoward()
            {
                VerifyMatchesAt(VimRegexOptions.Default, "$", "abc\r\ndef\r\n",
                    Tuple.Create(3, 0), Tuple.Create(8, 0), Tuple.Create(10, 0));
            }
        }

        public sealed class SearchTest : VimRegexTest
        {
            [Fact]
            public void NormalDigits()
            {
                VerifyMatches(@"\d", "1");
                VerifyMatches(@"\d\+", "100");
                VerifyMatches(VimRegexOptions.NoMagic, @"\d", "1");
                VerifyNotMatches(@"\D", "1");
                VerifyMatches(@"\D", "a", "!@");
            }

            [Fact]
            public void HexDigits()
            {
                VerifyMatches(@"\x", "1", "a", "f", "A", "D");
                VerifyMatches(@"\x\+", "1a");
                VerifyMatches(VimRegexOptions.NoMagic, @"\x", "1");
                VerifyNotMatches(@"\X", "a", "1");
                VerifyMatches(@"\X", "g", "!");
            }

            [Fact]
            public void OctalDigits()
            {
                VerifyMatches(@"\o", "1");
                VerifyMatches(@"\o\+", "100");
                VerifyMatches(VimRegexOptions.NoMagic, @"\o", "1");
                VerifyNotMatches(@"\O", "1");
                VerifyMatches(@"\O", "a", "!@", "8");
            }

            [Fact]
            public void NamedCollection()
            {
                VerifyMatches(@"[[:alpha:]]", "a", "d");
                VerifyNotMatches(@"[[:alpha:]]", "1", "@");
                VerifyMatches(@"[[:alpha:]]\+", "cat", "dog");
            }

            [Fact]
            public void Identifier()
            {
                VerifyMatches(@"\i", "a", "b", "C", "0");
                VerifyMatches(@"\I", "a", "b", "C");
                VerifyNotMatches(@"\I", "0");
            }

            [Fact]
            public void Issue1248()
            {
                VerifyMatches(@"Task<\(.\{-}\)>", "public void Task<string>");
            }

            [Fact]
            public void Issue2036()
            {
                VerifyMatches(@"\v\d\d+", "123");
                VerifyMatches(@"\v\d\d+", "456");
            }
        }

        public sealed class VeryMagic : VimRegexTest
        {
            [Fact]
            public void Digits()
            {
                VerifyRegex(@"\v\d+", @"\d+");
            }

            [Fact]
            public void WordBoundary()
            {
                VerifyRegex(@"\v<is>", @"\bis\b");
                VerifyRegex(@"\v<is", @"\bis");
            }

            [Fact]
            public void EscapedToNothing()
            {
                VerifyRegex(@"\v\^", @"\^");
                VerifyRegex(@"\v\$", @"\$");
                VerifyRegex(@"\v\*", @"\*");
            }

            [Fact]
            public void EscapedParens()
            {
                VerifyRegex(@"\vMethod\(", @"Method\(");
                VerifyMatches(@"\vMethod\(", @"Method(");
            }
        }
    }
}
