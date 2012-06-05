using System;
using System.Linq;
using Vim.Extensions;
using Vim.Interpreter;
using Xunit;

namespace Vim.UnitTest
{
    public class ParserTest
    {
        /// <summary>
        /// Assert that parsing the given line command produces the specific error
        /// </summary>
        protected void AssertParseLineCommandError(string command, string error)
        {
            var parseResult = Parser.ParseLineCommand(command);
            Assert.True(parseResult.IsFailed);
            Assert.Equal(error, parseResult.AsFailed().Item);
        }

        protected LineCommand ParseLineCommand(string text)
        {
            var parseResult = Parser.ParseLineCommand(text);
            Assert.True(parseResult.IsSucceeded);
            return parseResult.AsSucceeded().Item;
        }

        public sealed class StringLiteral : ParserTest
        {
            public string ParseStringLiteral(string text)
            {
                var parser = new Parser(text);
                var parseResult = parser.ParseStringLiteral();
                Assert.True(parseResult.IsSucceeded);
                return parseResult.AsSucceeded().Item.AsConstantValue().Item.AsString().Item;
            }

            [Fact]
            public void Simple()
            {
                Assert.Equal("hello", ParseStringLiteral(@"'hello'"));
            }

            [Fact]
            public void SingleBackslash()
            {
                Assert.Equal(@"\hello", ParseStringLiteral(@"'\hello'"));
            }

            [Fact]
            public void EscapedQuote()
            {
                Assert.Equal(@"'a", ParseStringLiteral(@"'\'a'"));
            }

            /// <summary>
            /// Make sure we can handle a double quote inside a string literal
            /// </summary>
            [Fact]
            public void DoubleQuote()
            {
                Assert.Equal(@"""", ParseStringLiteral(@"'""'"));
            }
        }

        public sealed class StringConstant : ParserTest
        {
            public string ParseStringConstant(string text)
            {
                var parser = new Parser(text);
                parser.Tokenizer.MoveToIndexEx(parser.Tokenizer.Index, NextTokenFlags.AllowDoubleQuote);
                var parseResult = parser.ParseStringConstant();
                Assert.True(parseResult.IsSucceeded);
                return parseResult.AsSucceeded().Item.AsConstantValue().Item.AsString().Item;
            }

            [Fact]
            public void Simple()
            {
                Assert.Equal("hello", ParseStringConstant(@"""hello"""));
            }

            [Fact]
            public void TabEscape()
            {
                Assert.Equal("\t", ParseStringConstant(@"""\t"""));
            }
        }

        public sealed class SubstituteTest : ParserTest
        {
            /// <summary>
            /// Assert the given command parser out to a substitute with the specified values
            /// </summary>
            private void AssertSubstitute(string command, string pattern, string replace, SubstituteFlags? flags = null)
            {
                var subCommand = ParseLineCommand(command).AsSubstitute();
                Assert.Equal(pattern, subCommand.Item2);
                Assert.Equal(replace, subCommand.Item3);

                // Verify flags if it was passed
                if (flags.HasValue)
                {
                    Assert.Equal(flags.Value, subCommand.Item4);
                }
            }

            /// <summary>
            /// Assert the given command parses out to a substitute repeat with the specified values
            /// </summary>
            private void AssertSubstituteRepeat(string command, SubstituteFlags flags)
            {
                var subCommand = ParseLineCommand(command).AsSubstituteRepeat();
                Assert.Equal(flags, subCommand.Item2);
            }

            /// <summary>
            /// Verify the substitute commands.  Simple replaces with no options
            /// </summary>
            [Fact]
            public void Simple()
            {
                AssertSubstitute("s/f/b", "f", "b", SubstituteFlags.None);
                AssertSubstitute("s/foo/bar", "foo", "bar", SubstituteFlags.None);
                AssertSubstitute("s/foo/bar/", "foo", "bar", SubstituteFlags.None);
                AssertSubstitute("s/foo//", "foo", "", SubstituteFlags.None);
                AssertSubstitute("s/foo", "foo", "", SubstituteFlags.None);
            }

            /// <summary>
            /// Support alternate separators in the substitute command
            /// </summary>
            [Fact]
            public void AlternateSeparators()
            {
                AssertSubstitute("s,f,b", "f", "b", SubstituteFlags.None);
                AssertSubstitute("s&f&b", "f", "b", SubstituteFlags.None);
                AssertSubstitute("s,foo,bar", "foo", "bar", SubstituteFlags.None);
                AssertSubstitute("s,foo,bar,", "foo", "bar", SubstituteFlags.None);
                AssertSubstitute("s,foo,,", "foo", "", SubstituteFlags.None);
                AssertSubstitute("s,foo", "foo", "", SubstituteFlags.None);
            }

            /// <summary>
            /// Make sure that we handle escaped separators properly
            /// </summary>
            [Fact]
            public void EscapedSeparator()
            {
                AssertSubstitute(@"s/and\/or/then", "and/or", "then", SubstituteFlags.None);
            }

            /// <summary>
            /// Simple substitute commands which provide specific flags
            /// </summary>
            [Fact]
            public void WithFlags()
            {
                AssertSubstitute("s/foo/bar/g", "foo", "bar", SubstituteFlags.ReplaceAll);
                AssertSubstitute("s/foo/bar/ g", "foo", "bar", SubstituteFlags.ReplaceAll);
                AssertSubstitute("s/foo/bar/i", "foo", "bar", SubstituteFlags.IgnoreCase);
                AssertSubstitute("s/foo/bar/gi", "foo", "bar", SubstituteFlags.ReplaceAll | SubstituteFlags.IgnoreCase);
                AssertSubstitute("s/foo/bar/ig", "foo", "bar", SubstituteFlags.ReplaceAll | SubstituteFlags.IgnoreCase);
                AssertSubstitute("s/foo/bar/n", "foo", "bar", SubstituteFlags.ReportOnly);
                AssertSubstitute("s/foo/bar/e", "foo", "bar", SubstituteFlags.SuppressError);
                AssertSubstitute("s/foo/bar/I", "foo", "bar", SubstituteFlags.OrdinalCase);
                AssertSubstitute("s/foo/bar/&", "foo", "bar", SubstituteFlags.UsePreviousFlags);
                AssertSubstitute("s/foo/bar/&g", "foo", "bar", SubstituteFlags.ReplaceAll | SubstituteFlags.UsePreviousFlags);
                AssertSubstitute("s/foo/bar/c", "foo", "bar", SubstituteFlags.Confirm);
                AssertSubstitute("s/foo/bar/p", "foo", "bar", SubstituteFlags.PrintLast);
                AssertSubstitute("s/foo/bar/#", "foo", "bar", SubstituteFlags.PrintLastWithNumber);
                AssertSubstitute("s/foo/bar/l", "foo", "bar", SubstituteFlags.PrintLastWithList);
                AssertSubstitute("s/foo/bar/ l", "foo", "bar", SubstituteFlags.PrintLastWithList);
            }

            /// <summary>
            /// Without a trailing delimiter a count won't ever be considered
            /// </summary>
            [Fact]
            public void CountMustHaveFinalDelimiter()
            {
                AssertSubstitute("s/a/b 2", "a", "b 2", SubstituteFlags.None);
            }

            /// <summary>
            /// Simple substitute with count
            /// </summary>
            [Fact]
            public void WithCount()
            {
                AssertSubstitute("s/a/b/g 2", "a", "b", SubstituteFlags.ReplaceAll);
            }

            /// <summary>
            /// The backslashes need to be preserved for the regex engine
            /// </summary>
            [Fact]
            public void Backslashes()
            {
                AssertSubstitute(@"s/a/\\\\", "a", @"\\\\", SubstituteFlags.None);
                AssertSubstitute(@"s/a/\\\\/", "a", @"\\\\", SubstituteFlags.None);
            }

            /// <summary>
            /// The & flag can only appear as the first flag.  In any other position it's a parser error
            /// </summary>
            [Fact]
            public void BadUsePreviousFlags()
            {
                AssertParseLineCommandError("s/a/b/g&", Resources.CommandMode_TrailingCharacters);
            }

            /// <summary>
            /// Can't have a space between flag values
            /// </summary>
            [Fact]
            public void BadSpaceBetweenFlags()
            {
                AssertParseLineCommandError("s/a/b/g &", Resources.CommandMode_TrailingCharacters);
            }

            /// <summary>
            /// Make sure we can handle double quotes in both places
            /// </summary>
            [Fact]
            public void DoubleQuotes()
            {
                AssertSubstitute(@"s/""cat""/dog", @"""cat""", "dog");
                AssertSubstitute(@"s/cat/""dog""", @"cat", @"""dog""");
            }

            /// <summary>
            /// Make sure that we properly parse out the group specifier in the replace string
            /// </summary>
            [Fact]
            public void ReplaceHasGroupSpecifier()
            {
                AssertSubstitute(@"s/cat/\1", "cat", @"\1");
                AssertSubstitute(@"s/dog/\2", "dog", @"\2");
                AssertSubstitute(@"s/dog/fish\2", "dog", @"fish\2");
                AssertSubstitute(@"s/dog/\2fish", "dog", @"\2fish");
            }

            /// <summary>
            /// Make sure this scenario isn't treated as a new line.  The backslashes need to all
            /// be preserved and handled by the regex engine
            /// </summary>
            [Fact]
            public void EscapedBackslashInReplace()
            {
                AssertSubstitute(@"s/$/\\n\\/", "$", @"\\n\\");
            }

            [Fact]
            public void RepeatWithCount()
            {
                AssertSubstituteRepeat("& 3", SubstituteFlags.None);
            }

            /// <summary>
            /// Parse the snomagic form of substitute
            /// </summary>
            [Fact]
            public void NoMagic()
            {
                AssertSubstitute("snomagic/a/b", "a", "b", SubstituteFlags.Nomagic);
                AssertSubstitute("snomagic/a/b/g", "a", "b", SubstituteFlags.Nomagic | SubstituteFlags.ReplaceAll);
            }

            /// <summary>
            /// Parse the smagic form of substitute
            /// </summary>
            [Fact]
            public void Magic()
            {
                AssertSubstitute("smagic/a/b", "a", "b", SubstituteFlags.Magic);
                AssertSubstitute("smagic/a/b/g", "a", "b", SubstituteFlags.ReplaceAll | SubstituteFlags.Magic);
            }

            [Fact]
            public void RepeatSimple()
            {
                AssertSubstituteRepeat("s", SubstituteFlags.None);
                AssertSubstituteRepeat("s g", SubstituteFlags.ReplaceAll);
                AssertSubstituteRepeat("& g", SubstituteFlags.ReplaceAll);
                AssertSubstituteRepeat("&&", SubstituteFlags.UsePreviousFlags);
                AssertSubstituteRepeat("&r", SubstituteFlags.UsePreviousSearchPattern);
                AssertSubstituteRepeat("&&g", SubstituteFlags.ReplaceAll | SubstituteFlags.UsePreviousFlags);
                AssertSubstituteRepeat("~", SubstituteFlags.UsePreviousSearchPattern);
                AssertSubstituteRepeat("~ g", SubstituteFlags.UsePreviousSearchPattern | SubstituteFlags.ReplaceAll);
                AssertSubstituteRepeat("~ g 3", SubstituteFlags.UsePreviousSearchPattern | SubstituteFlags.ReplaceAll);
            }

            /// <summary>
            /// Parse the snomagic form of substitute repeat
            /// </summary>
            [Fact]
            public void RepeatNoMagic()
            {
                AssertSubstituteRepeat("snomagic", SubstituteFlags.Nomagic);
                AssertSubstituteRepeat("snomagic g", SubstituteFlags.Nomagic | SubstituteFlags.ReplaceAll);
            }

            /// <summary>
            /// Parse the smagic form of substitute repeat
            /// </summary>
            [Fact]
            public void RepeatMagic()
            {
                AssertSubstituteRepeat("smagic", SubstituteFlags.Magic);
                AssertSubstituteRepeat("smagic g", SubstituteFlags.ReplaceAll | SubstituteFlags.Magic);
            }
        }

        public sealed class SetTest : ParserTest
        {
            /// <summary>
            /// Make sure we can parse out the short version of the ":set" command
            /// </summary>
            [Fact]
            public void ShortName()
            {
                var command = ParseLineCommand("se");
                Assert.True(command.IsSet);
            }

            /// <summary>
            /// Make sure we can parse out the 'all' modifier on the ":set" command
            /// </summary>
            [Fact]
            public void All()
            {
                var command = ParseLineCommand("set all").AsSet();
                Assert.True(command.Item.Single().IsDisplayAllButTerminal);
            }

            /// <summary>
            /// Make sure we can parse out the display setting argument to ":set"
            /// </summary>
            [Fact]
            public void DisplaySetting()
            {
                var command = ParseLineCommand("set example?").AsSet();
                var option = command.Item.Single().AsDisplaySetting();
                Assert.Equal("example", option.Item);
            }

            /// <summary>
            /// Make sure we can parse out the use setting argument to ":set"
            /// </summary>
            [Fact]
            public void UseSetting()
            {
                var command = ParseLineCommand("set example").AsSet();
                var option = command.Item.Single().AsUseSetting();
                Assert.Equal("example", option.Item);
            }

            /// <summary>
            /// Make sure we can parse out the toggle setting argument to ":set"
            /// </summary>
            [Fact]
            public void ToggleOffSetting()
            {
                var command = ParseLineCommand("set noexample").AsSet();
                var option = command.Item.Single().AsToggleOffSetting();
                Assert.Equal("example", option.Item);
            }

            /// <summary>
            /// Make sure we can parse out the invert setting argument to ":set"
            /// </summary>
            [Fact]
            public void InvertSetting()
            {
                var command = ParseLineCommand("set example!").AsSet();
                var option = command.Item.Single().AsInvertSetting();
                Assert.Equal("example", option.Item);
            }

            /// <summary>
            /// Make sure we can parse out the invert setting argument to ":set" using the alternate
            /// syntax
            /// </summary>
            [Fact]
            public void InvertSetting_AlternateSyntax()
            {
                var command = ParseLineCommand("set invexample").AsSet();
                var option = command.Item.Single().AsInvertSetting();
                Assert.Equal("example", option.Item);
            }

            /// <summary>
            /// Make sure we can parse out the assign setting argument to ":set"
            /// </summary>
            [Fact]
            public void AssignSetting()
            {
                var command = ParseLineCommand("set x=y").AsSet();
                var option = command.Item.Single().AsAssignSetting();
                Assert.Equal("x", option.Item1);
                Assert.Equal("y", option.Item2);
            }

            /// <summary>
            /// Make sure we can parse out the assign setting argument to ":set" using the alternate 
            /// syntax
            /// </summary>
            [Fact]
            public void AssignSetting_AlternateSyntax()
            {
                var command = ParseLineCommand("set x:y").AsSet();
                var option = command.Item.Single().AsAssignSetting();
                Assert.Equal("x", option.Item1);
                Assert.Equal("y", option.Item2);
            }

            /// <summary>
            /// It's a legal parse for the value to be empty.  This is used with the terminal settings (t_vb)
            /// </summary>
            [Fact]
            public void AssignNoValue()
            {
                var command = ParseLineCommand("set vb=").AsSet();
                var option = command.Item.Single().AsAssignSetting();
                Assert.Equal("vb", option.Item1);
                Assert.Equal("", option.Item2);
            }

            /// <summary>
            /// Make sure we can parse out the add setting argument to ":set"
            /// </summary>
            [Fact]
            public void AddSetting()
            {
                var command = ParseLineCommand("set x+=y").AsSet();
                var option = command.Item.Single().AsAddSetting();
                Assert.Equal("x", option.Item1);
                Assert.Equal("y", option.Item2);
            }

            /// <summary>
            /// Make sure we can parse out the subtract setting argument to ":set"
            /// </summary>
            [Fact]
            public void SubtractSetting()
            {
                var command = ParseLineCommand("set x-=y").AsSet();
                var option = command.Item.Single().AsSubtractSetting();
                Assert.Equal("x", option.Item1);
                Assert.Equal("y", option.Item2);
            }

            /// <summary>
            /// A space after an = terminates the current set and begins a new one
            /// </summary>
            [Fact]
            public void AssignWithSpaceAfter()
            {
                var command = ParseLineCommand("set vb= ai").AsSet();
                Assert.Equal(2, command.Item.Length);

                var set = command.Item[0];
                Assert.Equal("vb", set.AsAssignSetting().Item1);
                Assert.Equal("", set.AsAssignSetting().Item2);

                set = command.Item[1];
                Assert.Equal("ai", set.AsUseSetting().Item);
            }

            /// <summary>
            /// Make sure we can parse out the multiply setting argument to ":set"
            /// </summary>
            [Fact]
            public void MultiplySetting()
            {
                var command = ParseLineCommand("set x^=y").AsSet();
                var option = command.Item.Single().AsMultiplySetting();
                Assert.Equal("x", option.Item1);
                Assert.Equal("y", option.Item2);
            }

            /// <summary>
            /// Make sure we can parse out a set with a trailing comment
            /// </summary>
            [Fact]
            public void TrailingComment()
            {
                var command = ParseLineCommand("set ai \"hello world");
                Assert.True(command.IsSet);
            }
        }

        public sealed class Address : ParserTest
        {
            private LineRangeSpecifier ParseLineRange(string text)
            {
                var parser = new Parser(text);
                var lineRange = parser.ParseLineRange();
                Assert.False(lineRange.IsNone);
                return lineRange;
            }

            private LineSpecifier ParseLineSpecifier(string text)
            {
                var parser = new Parser(text);
                var option = parser.ParseLineSpecifier();
                Assert.True(option.IsSome());
                return option.Value;
            }

            /// <summary>
            /// Make sure we can parse out the '%' range
            /// </summary>
            [Fact]
            public void EntireBuffer()
            {
                var lineRange = ParseLineRange("%");
                Assert.True(lineRange.IsEntireBuffer);
            }

            /// <summary>
            /// Make sure we can parse out a single line number range
            /// </summary>
            [Fact]
            public void SingleLineNumber()
            {
                var lineRange = ParseLineRange("42");
                Assert.True(lineRange.IsSingleLine);
                Assert.True(lineRange.AsSingleLine().Item.IsNumber(42));
            }

            /// <summary>
            /// Make sure we can parse out a range of the current line and itself
            /// </summary>
            [Fact]
            public void RangeOfCurrentLine()
            {
                var lineRange = ParseLineRange(".,.");
                Assert.True(lineRange.AsRange().Item1.IsCurrentLine);
                Assert.True(lineRange.AsRange().Item2.IsCurrentLine);
                Assert.False(lineRange.AsRange().item3);
            }

            /// <summary>
            /// Make sure we can parse out a range of numbers
            /// </summary>
            [Fact]
            public void RangeOfNumbers()
            {
                var lineRange = ParseLineRange("1,2");
                Assert.True(lineRange.AsRange().Item1.IsNumber(1));
                Assert.True(lineRange.AsRange().Item2.IsNumber(2));
                Assert.False(lineRange.AsRange().item3);
            }

            /// <summary>
            /// Make sure we can parse out a range of numbers with the adjust caret 
            /// option specified
            /// </summary>
            [Fact]
            public void RangeOfNumbersWithAdjustCaret()
            {
                var lineRange = ParseLineRange("1;2");
                Assert.True(lineRange.AsRange().Item1.IsNumber(1));
                Assert.True(lineRange.AsRange().Item2.IsNumber(2));
                Assert.True(lineRange.AsRange().item3);
            }

            /// <summary>
            /// Make sure that it can handle a mark range
            /// </summary>
            [Fact]
            public void Marks()
            {
                var lineRange = ParseLineRange("'a,'b");
                Assert.True(lineRange.AsRange().Item1.IsMarkLine);
                Assert.True(lineRange.AsRange().Item2.IsMarkLine);
            }

            /// <summary>
            /// Make sure that it can handle a mark range with trailing text
            /// </summary>
            [Fact]
            public void MarksWithTrailing()
            {
                var lineRange = ParseLineRange("'a,'bc");
                Assert.True(lineRange.AsRange().Item1.IsMarkLine);
                Assert.True(lineRange.AsRange().Item2.IsMarkLine);
            }

            /// <summary>
            /// Ensure we can parse out a simple next pattern
            /// </summary>
            [Fact]
            public void NextPatternSpecifier()
            {
                var lineSpecifier = ParseLineSpecifier("/dog/");
                Assert.Equal("dog", lineSpecifier.AsNextLineWithPattern().Item);
            }

            /// <summary>
            /// Ensure we can parse out a simple previous pattern
            /// </summary>
            [Fact]
            public void PreviousPatternSpecifier()
            {
                var lineSpecifier = ParseLineSpecifier("?dog?");
                Assert.Equal("dog", lineSpecifier.AsPreviousLineWithPattern().Item);
            }
        }

        public sealed class Map : ParserTest
        {
            private void AssertMap(string command, string lhs, string rhs, params KeyRemapMode[] keyRemapModes)
            {
                AssertMapCore(command, lhs, rhs, false, keyRemapModes);
            }

            private void AssertMapWithRemap(string command, string lhs, string rhs, params KeyRemapMode[] keyRemapModes)
            {
                AssertMapCore(command, lhs, rhs, true, keyRemapModes);
            }

            private void AssertMapCore(string command, string lhs, string rhs, bool allowRemap, params KeyRemapMode[] keyRemapModes)
            {
                var map = ParseLineCommand(command).AsMapKeys();
                Assert.Equal(lhs, map.Item1);
                Assert.Equal(rhs, map.Item2);
                Assert.Equal(keyRemapModes, map.Item3.ToArray());
                Assert.Equal(allowRemap, map.Item4);
            }

            private void AssertUnmap(string command, string keyNotation, params KeyRemapMode[] keyRemapModes)
            {
                var map = ParseLineCommand(command).AsUnmapKeys();
                Assert.Equal(keyNotation, map.Item1);
                Assert.Equal(keyRemapModes, map.Item2.ToArray());
            }

            [Fact]
            public void Default()
            {
                var modes = new KeyRemapMode[] { KeyRemapMode.Normal, KeyRemapMode.Visual, KeyRemapMode.Select, KeyRemapMode.OperatorPending };
                AssertMap("noremap l h", "l", "h", modes);
                AssertMap("nore l h", "l", "h", modes);
                AssertMap("no l h", "l", "h", modes);
            }

            [Fact]
            public void DefaultWithBang()
            {
                var modes = new KeyRemapMode[] { KeyRemapMode.Insert, KeyRemapMode.Command };
                AssertMap("noremap! l h", "l", "h", modes);
                AssertMap("nore! l h", "l", "h", modes);
                AssertMap("no! l h", "l", "h", modes);
            }

            /// <summary>
            /// Make sure that we can handle a double quote in the left side of an argument and 
            /// that it's not interpreted as a comment
            /// </summary>
            [Fact]
            public void DoubleQuotesInLeft()
            {
                AssertMapWithRemap(@"imap "" dog", @"""", "dog", KeyRemapMode.Insert);
                AssertMapWithRemap(@"imap ""h dog", @"""h", "dog", KeyRemapMode.Insert);
                AssertMapWithRemap(@"imap a""h dog", @"a""h", "dog", KeyRemapMode.Insert);
            }

            /// <summary>
            /// Make sure that we can handle a double quote in the right side of an argument and 
            /// that it's not interpreted as a comment
            /// </summary>
            [Fact]
            public void DoubleQuotesInRight()
            {
                AssertMapWithRemap(@"imap d """, "d", @"""", KeyRemapMode.Insert);
                AssertMapWithRemap(@"imap d h""", "d", @"h""", KeyRemapMode.Insert);
                AssertMapWithRemap(@"imap d h""a", "d", @"h""a", KeyRemapMode.Insert);
            }

            [Fact]
            public void Normal()
            {
                AssertMap("nnoremap l h", "l", "h", KeyRemapMode.Normal);
                AssertMap("nnor l h", "l", "h", KeyRemapMode.Normal);
                AssertMap("nn l h", "l", "h", KeyRemapMode.Normal);
            }

            [Fact]
            public void VirtualAndSelect()
            {
                AssertMap("vnoremap a b", "a", "b", KeyRemapMode.Visual, KeyRemapMode.Select);
                AssertMap("vnor a b", "a", "b", KeyRemapMode.Visual, KeyRemapMode.Select);
                AssertMap("vn a b", "a", "b", KeyRemapMode.Visual, KeyRemapMode.Select);
            }

            [Fact]
            public void Visual()
            {
                AssertMap("xnoremap b c", "b", "c", KeyRemapMode.Visual);
            }

            [Fact]
            public void Select()
            {
                AssertMap("snoremap a b", "a", "b", KeyRemapMode.Select);
            }

            [Fact]
            public void OperatorPending()
            {
                AssertMap("onoremap a b", "a", "b", KeyRemapMode.OperatorPending);
            }

            [Fact]
            public void Insert()
            {
                AssertMap("inoremap a b", "a", "b", KeyRemapMode.Insert);
            }

            /// <summary>
            /// Make sure the map commands can handle the special argument
            /// </summary>
            [Fact]
            public void Arguments()
            {
                Action<string, KeyMapArgument> action =
                    (commandText, mapArgument) =>
                    {
                        var command = ParseLineCommand(commandText).AsMapKeys();
                        var mapArguments = command.Item5;
                        Assert.Equal(1, mapArguments.Length);
                        Assert.Equal(mapArgument, mapArguments.Head);
                    };
                action("map <buffer> a b", KeyMapArgument.Buffer);
                action("map <silent> a b", KeyMapArgument.Silent);
                action("imap <silent> a b", KeyMapArgument.Silent);
                action("nmap <silent> a b", KeyMapArgument.Silent);
            }

            /// <summary>
            /// Make sure we can parse out all of the map special argument values
            /// </summary>
            [Fact]
            public void ArgumentsAll()
            {
                var all = new[] { "buffer", "silent", "expr", "unique", "special" };
                foreach (var cur in all)
                {
                    var parser = new Parser("<" + cur + ">");
                    var list = parser.ParseMapArguments();
                    Assert.Equal(1, list.Length);
                }
            }

            /// <summary>
            /// Make sure we can parse out several items in a row and in the correct order
            /// </summary>
            [Fact]
            public void ArgumentsMultiple()
            {
                var text = "<buffer> <silent>";
                var parser = new Parser(text);
                var list = parser.ParseMapArguments().ToList();
                Assert.Equal(
                    new[] { KeyMapArgument.Buffer, KeyMapArgument.Silent },
                    list);
            }

            [Fact]
            public void RemapStandard()
            {
                AssertMapWithRemap("map a bc", "a", "bc", KeyRemapMode.Normal, KeyRemapMode.Visual, KeyRemapMode.Select, KeyRemapMode.OperatorPending);
            }

            [Fact]
            public void RemapNormal()
            {
                AssertMapWithRemap("nmap a b", "a", "b", KeyRemapMode.Normal);
            }

            [Fact]
            public void RemapMany()
            {
                AssertMapWithRemap("vmap a b", "a", "b", KeyRemapMode.Visual, KeyRemapMode.Select);
                AssertMapWithRemap("vm a b", "a", "b", KeyRemapMode.Visual, KeyRemapMode.Select);
                AssertMapWithRemap("xmap a b", "a", "b", KeyRemapMode.Visual);
                AssertMapWithRemap("xm a b", "a", "b", KeyRemapMode.Visual);
                AssertMapWithRemap("smap a b", "a", "b", KeyRemapMode.Select);
                AssertMapWithRemap("omap a b", "a", "b", KeyRemapMode.OperatorPending);
                AssertMapWithRemap("om a b", "a", "b", KeyRemapMode.OperatorPending);
                AssertMapWithRemap("imap a b", "a", "b", KeyRemapMode.Insert);
                AssertMapWithRemap("im a b", "a", "b", KeyRemapMode.Insert);
                AssertMapWithRemap("cmap a b", "a", "b", KeyRemapMode.Command);
                AssertMapWithRemap("cm a b", "a", "b", KeyRemapMode.Command);
                AssertMapWithRemap("lmap a b", "a", "b", KeyRemapMode.Language);
                AssertMapWithRemap("lm a b", "a", "b", KeyRemapMode.Language);
                AssertMapWithRemap("map! a b", "a", "b", KeyRemapMode.Insert, KeyRemapMode.Command);
            }

            /// <summary>
            /// Parse out the unmapping of keys
            /// </summary>
            [Fact]
            public void UnmapSimple()
            {
                AssertUnmap("vunmap a ", "a", KeyRemapMode.Visual, KeyRemapMode.Select);
                AssertUnmap("vunm a ", "a", KeyRemapMode.Visual, KeyRemapMode.Select);
                AssertUnmap("xunmap a", "a", KeyRemapMode.Visual);
                AssertUnmap("xunm a ", "a", KeyRemapMode.Visual);
                AssertUnmap("sunmap a ", "a", KeyRemapMode.Select);
                AssertUnmap("ounmap a ", "a", KeyRemapMode.OperatorPending);
                AssertUnmap("ounm a ", "a", KeyRemapMode.OperatorPending);
                AssertUnmap("iunmap a ", "a", KeyRemapMode.Insert);
                AssertUnmap("iunm a", "a", KeyRemapMode.Insert);
                AssertUnmap("cunmap a ", "a", KeyRemapMode.Command);
                AssertUnmap("cunm a ", "a", KeyRemapMode.Command);
                AssertUnmap("lunmap a ", "a", KeyRemapMode.Language);
                AssertUnmap("lunm a ", "a", KeyRemapMode.Language);
                AssertUnmap("unmap! a ", "a", KeyRemapMode.Insert, KeyRemapMode.Command);
            }
        }

        public sealed class Misc : ParserTest
        {
            /// <summary>
            /// Change directory with an empty path
            /// </summary>
            [Fact]
            public void Parse_ChangeDirectory_Empty()
            {
                var command = ParseLineCommand("cd").AsChangeDirectory();
                Assert.True(command.Item.IsNone());
            }

            /// <summary>
            /// Change directory with a path
            /// </summary>
            [Fact]
            public void Parse_ChangeDirectory_Path()
            {
                var command = ParseLineCommand("cd test.txt").AsChangeDirectory();
                Assert.Equal("test.txt", command.Item.Value);
            }

            /// <summary>
            /// Change directory with a path and a bang.  The bang is ignored but legal in 
            /// the grammar
            /// </summary>
            [Fact]
            public void Parse_ChangeDirectory_PathAndBang()
            {
                var command = ParseLineCommand("cd! test.txt").AsChangeDirectory();
                Assert.Equal("test.txt", command.Item.Value);
            }

            /// <summary>
            /// ChangeLocal directory with an empty path
            /// </summary>
            [Fact]
            public void Parse_ChangeLocalDirectory_Empty()
            {
                var command = ParseLineCommand("lcd").AsChangeLocalDirectory();
                Assert.True(command.Item.IsNone());
            }

            /// <summary>
            /// ChangeLocal directory with a path
            /// </summary>
            [Fact]
            public void Parse_ChangeLocalDirectory_Path()
            {
                var command = ParseLineCommand("lcd test.txt").AsChangeLocalDirectory();
                Assert.Equal("test.txt", command.Item.Value);
            }

            /// <summary>
            /// ChangeLocal directory with a path and a bang.  The bang is ignored but legal in 
            /// the grammar
            /// </summary>
            [Fact]
            public void Parse_ChangeLocalDirectory_PathAndBang()
            {
                var command = ParseLineCommand("lcd! test.txt").AsChangeLocalDirectory();
                Assert.Equal("test.txt", command.Item.Value);
            }

            /// <summary>
            /// Make sure we can parse out the close command
            /// </summary>
            [Fact]
            public void Parse_Close_NoBang()
            {
                var command = ParseLineCommand("close");
                Assert.True(command.IsClose);
            }
            /// <summary>
            /// Make sure we can parse out the close wit bang
            /// </summary>
            [Fact]
            public void Parse_Close_WithBang()
            {
                var command = ParseLineCommand("close!");
                Assert.True(command.IsClose);
                Assert.True(command.AsClose().Item);
            }

            /// <summary>
            /// Make sure that we detect the trailing characters in the close command
            /// </summary>
            [Fact]
            public void Parse_Close_Trailing()
            {
                var parseResult = Parser.ParseLineCommand("close foo");
                Assert.True(parseResult.IsFailed(Resources.CommandMode_TrailingCharacters));
            }

            /// <summary>
            /// A line consisting of only a comment should parse as a nop
            /// </summary>
            [Fact]
            public void Parse_Nop_CommentLine()
            {
                Assert.True(ParseLineCommand(@" "" hello world").IsNop);
            }


            /// <summary>
            /// A line consisting of nothing should parse as a nop
            /// </summary>
            [Fact]
            public void Parse_Nop_EmptyLine()
            {
                Assert.True(ParseLineCommand(@"").IsNop);
            }

            /// <summary>
            /// Make sure we can handle the count argument of :delete
            /// </summary>
            [Fact]
            public void Parse_Delete_WithCount()
            {
                var lineCommand = ParseLineCommand("delete 2");
                var lineRange = lineCommand.AsDelete().Item1;
                Assert.Equal(2, lineRange.AsWithEndCount().Item2.Value);
            }

            [Fact]
            public void Parse_PrintCurrentDirectory()
            {
                var command = ParseLineCommand("pwd");
                Assert.True(command.IsPrintCurrentDirectory);
            }

            [Fact]
            public void Parse_ReadCommand_Simple()
            {
                var command = ParseLineCommand("read !echo bar").AsReadCommand();
                Assert.Equal("echo bar", command.Item2);
            }

            [Fact]
            public void Parse_ReadFile_Simple()
            {
                var command = ParseLineCommand("read test.txt").AsReadFile();
                Assert.Equal("test.txt", command.Item3);
            }

            [Fact]
            public void Parse_Shift_Left()
            {
                var command = ParseLineCommand("<");
                Assert.True(command.IsShiftLeft);
            }

            [Fact]
            public void Parse_Shift_Right()
            {
                var command = ParseLineCommand(">");
                Assert.True(command.IsShiftRight);
            }

            /// <summary>
            /// Parse out a source command with the specified name and no bang flag
            /// </summary>
            [Fact]
            public void Parse_Source_Simple()
            {
                var command = ParseLineCommand("source test.txt").AsSource();
                Assert.False(command.Item1);
                Assert.Equal("test.txt", command.Item2);
            }

            /// <summary>
            /// Parse out a source command with the specified name and bang flag
            /// </summary>
            [Fact]
            public void Parse_Source_WithBang()
            {
                var command = ParseLineCommand("source! test.txt").AsSource();
                Assert.True(command.Item1);
                Assert.Equal("test.txt", command.Item2);
            }

            [Fact]
            public void Parse_Write_Simple()
            {
                var write = ParseLineCommand("w").AsWrite();
                Assert.True(write.Item1.IsNone);
                Assert.False(write.Item2);
                Assert.True(write.Item4.IsNone());
            }

            /// <summary>
            /// Parse out the write command given a file option
            /// </summary>
            [Fact]
            public void Parse_Write_ToFile()
            {
                var write = ParseLineCommand("w example.txt").AsWrite();
                Assert.True(write.Item1.IsNone);
                Assert.False(write.Item2);
                Assert.Equal("example.txt", write.Item4.Value);
            }

            [Fact]
            public void Parse_WriteAll_Simple()
            {
                var writeAll = ParseLineCommand("wall").AsWriteAll();
                Assert.False(writeAll.Item);
            }

            /// <summary>
            /// Parse out the :wall command with the ! option
            /// </summary>
            [Fact]
            public void Parse_WriteAll_WithBang()
            {
                var writeAll = ParseLineCommand("wall!").AsWriteAll();
                Assert.True(writeAll.Item);
            }

            /// <summary>
            /// Verify that we can parse out a yank command with a corresponding range
            /// </summary>
            [Fact]
            public void Parse_Yank_WithRange()
            {
                var yank = ParseLineCommand("'a,'by");
                Assert.True(yank.IsYank);
            }

            /// <summary>
            /// When we pass in a full command name to try expand it shouldn't have any effect
            /// </summary>
            [Fact]
            public void TryExpand_Full()
            {
                var parser = new Parser("");
                Assert.Equal("close", parser.TryExpand("close"));
            }

            /// <summary>
            /// Make sure the abbreviation can be expanded
            /// </summary>
            [Fact]
            public void TryExpand_Abbrevation()
            {
                var parser = new Parser("");
                foreach (var tuple in Parser.s_LineCommandNamePair)
                {
                    if (!String.IsNullOrEmpty(tuple.Item2))
                    {
                        Assert.Equal(tuple.Item1, parser.TryExpand(tuple.Item2));
                    }
                }
            }
        }
    }
}
