using System;
using System.Linq;
using NUnit.Framework;
using Vim.Extensions;
using Vim.Interpreter;

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
            Assert.IsTrue(parseResult.IsFailed);
            Assert.AreEqual(error, parseResult.AsFailed().Item);
        }

        protected LineCommand ParseLineCommand(string text)
        {
            var parseResult = Parser.ParseLineCommand(text);
            Assert.IsTrue(parseResult.IsSucceeded);
            return parseResult.AsSucceeded().Item;
        }

        [TestFixture]
        public sealed class StringLiteral : ParserTest
        {
            public string ParseStringLiteral(string text)
            {
                var parser = new Parser(text);
                var parseResult = parser.ParseStringLiteral();
                Assert.IsTrue(parseResult.IsSucceeded);
                return parseResult.AsSucceeded().Item.AsConstantValue().Item.AsString().Item;
            }

            [Test]
            public void Simple()
            {
                Assert.AreEqual("hello", ParseStringLiteral(@"'hello'"));
            }

            [Test]
            public void SingleBackslash()
            {
                Assert.AreEqual(@"\hello", ParseStringLiteral(@"'\hello'"));
            }

            [Test]
            public void EscapedQuote()
            {
                Assert.AreEqual(@"'a", ParseStringLiteral(@"'\'a'"));
            }

            /// <summary>
            /// Make sure we can handle a double quote inside a string literal
            /// </summary>
            [Test]
            public void DoubleQuote()
            {
                Assert.AreEqual(@"""", ParseStringLiteral(@"'""'"));
            }
        }

        [TestFixture]
        public sealed class StringConstant : ParserTest
        {
            public string ParseStringConstant(string text)
            {
                var parser = new Parser(text);
                parser.Tokenizer.MoveToIndexEx(parser.Tokenizer.Index, NextTokenFlags.AllowDoubleQuote);
                var parseResult = parser.ParseStringConstant();
                Assert.IsTrue(parseResult.IsSucceeded);
                return parseResult.AsSucceeded().Item.AsConstantValue().Item.AsString().Item;
            }

            [Test]
            public void Simple()
            {
                Assert.AreEqual("hello", ParseStringConstant(@"""hello"""));
            }

            [Test]
            public void TabEscape()
            {
                Assert.AreEqual("\t", ParseStringConstant(@"""\t"""));
            }
        }

        [TestFixture]
        public sealed class SubstituteTest : ParserTest
        {
            /// <summary>
            /// Assert the given command parser out to a substitute with the specified values
            /// </summary>
            private void AssertSubstitute(string command, string pattern, string replace, SubstituteFlags? flags = null)
            {
                var subCommand = ParseLineCommand(command).AsSubstitute();
                Assert.AreEqual(pattern, subCommand.Item2);
                Assert.AreEqual(replace, subCommand.Item3);

                // Verify flags if it was passed
                if (flags.HasValue)
                {
                    Assert.AreEqual(flags.Value, subCommand.Item4);
                }
            }

            /// <summary>
            /// Assert the given command parses out to a substitute repeat with the specified values
            /// </summary>
            private void AssertSubstituteRepeat(string command, SubstituteFlags flags)
            {
                var subCommand = ParseLineCommand(command).AsSubstituteRepeat();
                Assert.AreEqual(flags, subCommand.Item2);
            }

            /// <summary>
            /// Verify the substitute commands.  Simple replaces with no options
            /// </summary>
            [Test]
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
            [Test]
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
            [Test]
            public void EscapedSeparator()
            {
                AssertSubstitute(@"s/and\/or/then", "and/or", "then", SubstituteFlags.None);
            }

            /// <summary>
            /// Simple substitute commands which provide specific flags
            /// </summary>
            [Test]
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
            [Test]
            public void CountMustHaveFinalDelimiter()
            {
                AssertSubstitute("s/a/b 2", "a", "b 2", SubstituteFlags.None);
            }

            /// <summary>
            /// Simple substitute with count
            /// </summary>
            [Test]
            public void WithCount()
            {
                AssertSubstitute("s/a/b/g 2", "a", "b", SubstituteFlags.ReplaceAll);
            }

            /// <summary>
            /// The backslashes need to be preserved for the regex engine
            /// </summary>
            [Test]
            public void Backslashes()
            {
                AssertSubstitute(@"s/a/\\\\", "a", @"\\\\", SubstituteFlags.None);
                AssertSubstitute(@"s/a/\\\\/", "a", @"\\\\", SubstituteFlags.None);
            }

            /// <summary>
            /// The & flag can only appear as the first flag.  In any other position it's a parser error
            /// </summary>
            [Test]
            public void BadUsePreviousFlags()
            {
                AssertParseLineCommandError("s/a/b/g&", Resources.CommandMode_TrailingCharacters);
            }

            /// <summary>
            /// Can't have a space between flag values
            /// </summary>
            [Test]
            public void BadSpaceBetweenFlags()
            {
                AssertParseLineCommandError("s/a/b/g &", Resources.CommandMode_TrailingCharacters);
            }

            /// <summary>
            /// Make sure that we properly parse out the group specifier in the replace string
            /// </summary>
            [Test]
            public void ReplaceHasGroupSpecifier()
            {
                AssertSubstitute(@"s/cat/\1", "cat", @"\1");
                AssertSubstitute(@"s/dog/\2", "dog", @"\2");
                AssertSubstitute(@"s/dog/fish\2", "dog", @"fish\2");
                AssertSubstitute(@"s/dog/\2fish", "dog", @"\2fish");
            }

            [Test]
            public void RepeatWithCount()
            {
                AssertSubstituteRepeat("& 3", SubstituteFlags.None);
            }

            /// <summary>
            /// Parse the snomagic form of substitute
            /// </summary>
            [Test]
            public void NoMagic()
            {
                AssertSubstitute("snomagic/a/b", "a", "b", SubstituteFlags.Nomagic);
                AssertSubstitute("snomagic/a/b/g", "a", "b", SubstituteFlags.Nomagic | SubstituteFlags.ReplaceAll);
            }

            /// <summary>
            /// Parse the smagic form of substitute
            /// </summary>
            [Test]
            public void Magic()
            {
                AssertSubstitute("smagic/a/b", "a", "b", SubstituteFlags.Magic);
                AssertSubstitute("smagic/a/b/g", "a", "b", SubstituteFlags.ReplaceAll | SubstituteFlags.Magic);
            }

            [Test]
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
            [Test]
            public void RepeatNoMagic()
            {
                AssertSubstituteRepeat("snomagic", SubstituteFlags.Nomagic);
                AssertSubstituteRepeat("snomagic g", SubstituteFlags.Nomagic | SubstituteFlags.ReplaceAll);
            }

            /// <summary>
            /// Parse the smagic form of substitute repeat
            /// </summary>
            [Test]
            public void RepeatMagic()
            {
                AssertSubstituteRepeat("smagic", SubstituteFlags.Magic);
                AssertSubstituteRepeat("smagic g", SubstituteFlags.ReplaceAll | SubstituteFlags.Magic);
            }
        }

        [TestFixture]
        public sealed class Misc : ParserTest
        {
            private LineRangeSpecifier ParseLineRange(string text)
            {
                var parser = new Parser(text);
                var lineRange = parser.ParseLineRange();
                Assert.IsFalse(lineRange.IsNone);
                return lineRange;
            }

            private LineSpecifier ParseLineSpecifier(string text)
            {
                var parser = new Parser(text);
                var option = parser.ParseLineSpecifier();
                Assert.IsTrue(option.IsSome());
                return option.Value;
            }

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
                Assert.AreEqual(lhs, map.Item1);
                Assert.AreEqual(rhs, map.Item2);
                CollectionAssert.AreEqual(keyRemapModes, map.Item3.ToArray());
                Assert.AreEqual(allowRemap, map.Item4);
            }

            private void AssertUnmap(string command, string keyNotation, params KeyRemapMode[] keyRemapModes)
            {
                var map = ParseLineCommand(command).AsUnmapKeys();
                Assert.AreEqual(keyNotation, map.Item1);
                CollectionAssert.AreEqual(keyRemapModes, map.Item2.ToArray());
            }

            /// <summary>
            /// Change directory with an empty path
            /// </summary>
            [Test]
            public void Parse_ChangeDirectory_Empty()
            {
                var command = ParseLineCommand("cd").AsChangeDirectory();
                Assert.IsTrue(command.Item.IsNone());
            }

            /// <summary>
            /// Change directory with a path
            /// </summary>
            [Test]
            public void Parse_ChangeDirectory_Path()
            {
                var command = ParseLineCommand("cd test.txt").AsChangeDirectory();
                Assert.AreEqual("test.txt", command.Item.Value);
            }

            /// <summary>
            /// Change directory with a path and a bang.  The bang is ignored but legal in 
            /// the grammar
            /// </summary>
            [Test]
            public void Parse_ChangeDirectory_PathAndBang()
            {
                var command = ParseLineCommand("cd! test.txt").AsChangeDirectory();
                Assert.AreEqual("test.txt", command.Item.Value);
            }

            /// <summary>
            /// ChangeLocal directory with an empty path
            /// </summary>
            [Test]
            public void Parse_ChangeLocalDirectory_Empty()
            {
                var command = ParseLineCommand("lcd").AsChangeLocalDirectory();
                Assert.IsTrue(command.Item.IsNone());
            }

            /// <summary>
            /// ChangeLocal directory with a path
            /// </summary>
            [Test]
            public void Parse_ChangeLocalDirectory_Path()
            {
                var command = ParseLineCommand("lcd test.txt").AsChangeLocalDirectory();
                Assert.AreEqual("test.txt", command.Item.Value);
            }

            /// <summary>
            /// ChangeLocal directory with a path and a bang.  The bang is ignored but legal in 
            /// the grammar
            /// </summary>
            [Test]
            public void Parse_ChangeLocalDirectory_PathAndBang()
            {
                var command = ParseLineCommand("lcd! test.txt").AsChangeLocalDirectory();
                Assert.AreEqual("test.txt", command.Item.Value);
            }

            /// <summary>
            /// Make sure we can parse out the close command
            /// </summary>
            [Test]
            public void Parse_Close_NoBang()
            {
                var command = ParseLineCommand("close");
                Assert.IsTrue(command.IsClose);
            }
            /// <summary>
            /// Make sure we can parse out the close wit bang
            /// </summary>
            [Test]
            public void Parse_Close_WithBang()
            {
                var command = ParseLineCommand("close!");
                Assert.IsTrue(command.IsClose);
                Assert.IsTrue(command.AsClose().Item);
            }

            /// <summary>
            /// Make sure that we detect the trailing characters in the close command
            /// </summary>
            [Test]
            public void Parse_Close_Trailing()
            {
                var parseResult = Parser.ParseLineCommand("close foo");
                Assert.IsTrue(parseResult.IsFailed(Resources.CommandMode_TrailingCharacters));
            }

            /// <summary>
            /// A line consisting of only a comment should parse as a nop
            /// </summary>
            [Test]
            public void Parse_Nop_CommentLine()
            {
                Assert.IsTrue(ParseLineCommand(@" "" hello world").IsNop);
            }


            /// <summary>
            /// A line consisting of nothing should parse as a nop
            /// </summary>
            [Test]
            public void Parse_Nop_EmptyLine()
            {
                Assert.IsTrue(ParseLineCommand(@"").IsNop);
            }

            /// <summary>
            /// Make sure we can handle the count argument of :delete
            /// </summary>
            [Test]
            public void Parse_Delete_WithCount()
            {
                var lineCommand = ParseLineCommand("delete 2");
                var lineRange = lineCommand.AsDelete().Item1;
                Assert.AreEqual(2, lineRange.AsWithEndCount().Item2.Value);
            }

            /// <summary>
            /// Make sure we can parse out the '%' range
            /// </summary>
            [Test]
            public void Parse_LineRange_EntireBuffer()
            {
                var lineRange = ParseLineRange("%");
                Assert.IsTrue(lineRange.IsEntireBuffer);
            }

            /// <summary>
            /// Make sure we can parse out a single line number range
            /// </summary>
            [Test]
            public void Parse_LineRange_SingleLineNumber()
            {
                var lineRange = ParseLineRange("42");
                Assert.IsTrue(lineRange.IsSingleLine);
                Assert.IsTrue(lineRange.AsSingleLine().Item.IsNumber(42));
            }

            /// <summary>
            /// Make sure we can parse out a range of the current line and itself
            /// </summary>
            [Test]
            public void Parse_LineRange_RangeOfCurrentLine()
            {
                var lineRange = ParseLineRange(".,.");
                Assert.IsTrue(lineRange.AsRange().Item1.IsCurrentLine);
                Assert.IsTrue(lineRange.AsRange().Item2.IsCurrentLine);
                Assert.IsFalse(lineRange.AsRange().item3);
            }

            /// <summary>
            /// Make sure we can parse out a range of numbers
            /// </summary>
            [Test]
            public void Parse_LineRange_RangeOfNumbers()
            {
                var lineRange = ParseLineRange("1,2");
                Assert.IsTrue(lineRange.AsRange().Item1.IsNumber(1));
                Assert.IsTrue(lineRange.AsRange().Item2.IsNumber(2));
                Assert.IsFalse(lineRange.AsRange().item3);
            }

            /// <summary>
            /// Make sure we can parse out a range of numbers with the adjust caret 
            /// option specified
            /// </summary>
            [Test]
            public void Parse_LineRange_RangeOfNumbersWithAdjustCaret()
            {
                var lineRange = ParseLineRange("1;2");
                Assert.IsTrue(lineRange.AsRange().Item1.IsNumber(1));
                Assert.IsTrue(lineRange.AsRange().Item2.IsNumber(2));
                Assert.IsTrue(lineRange.AsRange().item3);
            }

            /// <summary>
            /// Make sure that it can handle a mark range
            /// </summary>
            [Test]
            public void Parse_LineRange_Marks()
            {
                var lineRange = ParseLineRange("'a,'b");
                Assert.IsTrue(lineRange.AsRange().Item1.IsMarkLine);
                Assert.IsTrue(lineRange.AsRange().Item2.IsMarkLine);
            }

            /// <summary>
            /// Make sure that it can handle a mark range with trailing text
            /// </summary>
            [Test]
            public void Parse_LineRange_MarksWithTrailing()
            {
                var lineRange = ParseLineRange("'a,'bc");
                Assert.IsTrue(lineRange.AsRange().Item1.IsMarkLine);
                Assert.IsTrue(lineRange.AsRange().Item2.IsMarkLine);
            }

            /// <summary>
            /// Ensure we can parse out a simple next pattern
            /// </summary>
            [Test]
            public void Parse_LineSpecifier_NextPattern()
            {
                var lineSpecifier = ParseLineSpecifier("/dog/");
                Assert.AreEqual("dog", lineSpecifier.AsNextLineWithPattern().Item);
            }

            /// <summary>
            /// Ensure we can parse out a simple previous pattern
            /// </summary>
            [Test]
            public void Parse_LineSpecifier_PreviousPattern()
            {
                var lineSpecifier = ParseLineSpecifier("?dog?");
                Assert.AreEqual("dog", lineSpecifier.AsPreviousLineWithPattern().Item);
            }

            [Test]
            public void Parse_Map_Default()
            {
                var modes = new KeyRemapMode[] { KeyRemapMode.Normal, KeyRemapMode.Visual, KeyRemapMode.Select, KeyRemapMode.OperatorPending };
                AssertMap("noremap l h", "l", "h", modes);
                AssertMap("nore l h", "l", "h", modes);
                AssertMap("no l h", "l", "h", modes);
            }

            [Test]
            public void Parse_Map_DefaultWithBang()
            {
                var modes = new KeyRemapMode[] { KeyRemapMode.Insert, KeyRemapMode.Command };
                AssertMap("noremap! l h", "l", "h", modes);
                AssertMap("nore! l h", "l", "h", modes);
                AssertMap("no! l h", "l", "h", modes);
            }

            [Test]
            public void Parse_Map_Normal()
            {
                AssertMap("nnoremap l h", "l", "h", KeyRemapMode.Normal);
                AssertMap("nnor l h", "l", "h", KeyRemapMode.Normal);
                AssertMap("nn l h", "l", "h", KeyRemapMode.Normal);
            }

            [Test]
            public void Parse_Map_VirtualAndSelect()
            {
                AssertMap("vnoremap a b", "a", "b", KeyRemapMode.Visual, KeyRemapMode.Select);
                AssertMap("vnor a b", "a", "b", KeyRemapMode.Visual, KeyRemapMode.Select);
                AssertMap("vn a b", "a", "b", KeyRemapMode.Visual, KeyRemapMode.Select);
            }

            [Test]
            public void Parse_Map_Visual()
            {
                AssertMap("xnoremap b c", "b", "c", KeyRemapMode.Visual);
            }

            [Test]
            public void Parse_Map_Select()
            {
                AssertMap("snoremap a b", "a", "b", KeyRemapMode.Select);
            }

            [Test]
            public void Parse_Map_OperatorPending()
            {
                AssertMap("onoremap a b", "a", "b", KeyRemapMode.OperatorPending);
            }

            [Test]
            public void Parse_Map_Insert()
            {
                AssertMap("inoremap a b", "a", "b", KeyRemapMode.Insert);
            }

            /// <summary>
            /// Make sure the map commands can handle the special argument
            /// </summary>
            [Test]
            public void Parse_Map_Arguments()
            {
                Action<string, KeyMapArgument> action =
                    (commandText, mapArgument) =>
                    {
                        var command = ParseLineCommand(commandText).AsMapKeys();
                        var mapArguments = command.Item5;
                        Assert.AreEqual(1, mapArguments.Length);
                        Assert.AreEqual(mapArgument, mapArguments.Head);
                    };
                action("map <buffer> a b", KeyMapArgument.Buffer);
                action("map <silent> a b", KeyMapArgument.Silent);
                action("imap <silent> a b", KeyMapArgument.Silent);
                action("nmap <silent> a b", KeyMapArgument.Silent);
            }

            /// <summary>
            /// Make sure we can parse out all of the map special argument values
            /// </summary>
            [Test]
            public void ParseMapArguments_All()
            {
                var all = new[] { "buffer", "silent", "expr", "unique", "special" };
                foreach (var cur in all)
                {
                    var parser = new Parser("<" + cur + ">");
                    var list = parser.ParseMapArguments();
                    Assert.AreEqual(1, list.Length);
                }
            }

            /// <summary>
            /// Make sure we can parse out several items in a row and in the correct order
            /// </summary>
            [Test]
            public void ParseMapArguments_Multiple()
            {
                var text = "<buffer> <silent>";
                var parser = new Parser(text);
                var list = parser.ParseMapArguments().ToList();
                CollectionAssert.AreEquivalent(
                    new[] { KeyMapArgument.Buffer, KeyMapArgument.Silent },
                    list);
            }

            [Test]
            public void Parse_MapWithRemap_Standard()
            {
                AssertMapWithRemap("map a bc", "a", "bc", KeyRemapMode.Normal, KeyRemapMode.Visual, KeyRemapMode.Select, KeyRemapMode.OperatorPending);
            }

            [Test]
            public void Parse_MapWithRemap_Normal()
            {
                AssertMapWithRemap("nmap a b", "a", "b", KeyRemapMode.Normal);
            }

            [Test]
            public void Parse_MapWithRemap_Many()
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

            [Test]
            public void Parse_PrintCurrentDirectory()
            {
                var command = ParseLineCommand("pwd");
                Assert.IsTrue(command.IsPrintCurrentDirectory);
            }

            [Test]
            public void Parse_ReadCommand_Simple()
            {
                var command = ParseLineCommand("read !echo bar").AsReadCommand();
                Assert.AreEqual("echo bar", command.Item2);
            }

            [Test]
            public void Parse_ReadFile_Simple()
            {
                var command = ParseLineCommand("read test.txt").AsReadFile();
                Assert.AreEqual("test.txt", command.Item3);
            }

            /// <summary>
            /// Make sure we can parse out the short version of the ":set" command
            /// </summary>
            [Test]
            public void Parse_Set_ShortName()
            {
                var command = ParseLineCommand("se");
                Assert.IsTrue(command.IsSet);
            }

            /// <summary>
            /// Make sure we can parse out the 'all' modifier on the ":set" command
            /// </summary>
            [Test]
            public void Parse_Set_All()
            {
                var command = ParseLineCommand("set all").AsSet();
                Assert.IsTrue(command.Item.Single().IsDisplayAllButTerminal);
            }

            /// <summary>
            /// Make sure we can parse out the display setting argument to ":set"
            /// </summary>
            [Test]
            public void Parse_Set_DisplaySetting()
            {
                var command = ParseLineCommand("set example?").AsSet();
                var option = command.Item.Single().AsDisplaySetting();
                Assert.AreEqual("example", option.Item);
            }

            /// <summary>
            /// Make sure we can parse out the use setting argument to ":set"
            /// </summary>
            [Test]
            public void Parse_Set_UseSetting()
            {
                var command = ParseLineCommand("set example").AsSet();
                var option = command.Item.Single().AsUseSetting();
                Assert.AreEqual("example", option.Item);
            }

            /// <summary>
            /// Make sure we can parse out the toggle setting argument to ":set"
            /// </summary>
            [Test]
            public void Parse_Set_ToggleOffSetting()
            {
                var command = ParseLineCommand("set noexample").AsSet();
                var option = command.Item.Single().AsToggleOffSetting();
                Assert.AreEqual("example", option.Item);
            }

            /// <summary>
            /// Make sure we can parse out the invert setting argument to ":set"
            /// </summary>
            [Test]
            public void Parse_Set_InvertSetting()
            {
                var command = ParseLineCommand("set example!").AsSet();
                var option = command.Item.Single().AsInvertSetting();
                Assert.AreEqual("example", option.Item);
            }

            /// <summary>
            /// Make sure we can parse out the invert setting argument to ":set" using the alternate
            /// syntax
            /// </summary>
            [Test]
            public void Parse_Set_InvertSetting_AlternateSyntax()
            {
                var command = ParseLineCommand("set invexample").AsSet();
                var option = command.Item.Single().AsInvertSetting();
                Assert.AreEqual("example", option.Item);
            }

            /// <summary>
            /// Make sure we can parse out the assign setting argument to ":set"
            /// </summary>
            [Test]
            public void Parse_Set_AssignSetting()
            {
                var command = ParseLineCommand("set x=y").AsSet();
                var option = command.Item.Single().AsAssignSetting();
                Assert.AreEqual("x", option.Item1);
                Assert.AreEqual("y", option.Item2);
            }

            /// <summary>
            /// Make sure we can parse out the assign setting argument to ":set" using the alternate 
            /// syntax
            /// </summary>
            [Test]
            public void Parse_Set_AssignSetting_AlternateSyntax()
            {
                var command = ParseLineCommand("set x:y").AsSet();
                var option = command.Item.Single().AsAssignSetting();
                Assert.AreEqual("x", option.Item1);
                Assert.AreEqual("y", option.Item2);
            }

            /// <summary>
            /// Make sure we can parse out the add setting argument to ":set"
            /// </summary>
            [Test]
            public void Parse_Set_AddSetting()
            {
                var command = ParseLineCommand("set x+=y").AsSet();
                var option = command.Item.Single().AsAddSetting();
                Assert.AreEqual("x", option.Item1);
                Assert.AreEqual("y", option.Item2);
            }

            /// <summary>
            /// Make sure we can parse out the subtract setting argument to ":set"
            /// </summary>
            [Test]
            public void Parse_Set_SubtractSetting()
            {
                var command = ParseLineCommand("set x-=y").AsSet();
                var option = command.Item.Single().AsSubtractSetting();
                Assert.AreEqual("x", option.Item1);
                Assert.AreEqual("y", option.Item2);
            }

            /// <summary>
            /// Make sure we can parse out the multiply setting argument to ":set"
            /// </summary>
            [Test]
            public void Parse_Set_MultiplySetting()
            {
                var command = ParseLineCommand("set x^=y").AsSet();
                var option = command.Item.Single().AsMultiplySetting();
                Assert.AreEqual("x", option.Item1);
                Assert.AreEqual("y", option.Item2);
            }

            /// <summary>
            /// Make sure we can parse out a set with a trailing comment
            /// </summary>
            [Test]
            public void Parse_Set_TrailingComment()
            {
                var command = ParseLineCommand("set ai \"hello world");
                Assert.IsTrue(command.IsSet);
            }

            [Test]
            public void Parse_Shift_Left()
            {
                var command = ParseLineCommand("<");
                Assert.IsTrue(command.IsShiftLeft);
            }

            [Test]
            public void Parse_Shift_Right()
            {
                var command = ParseLineCommand(">");
                Assert.IsTrue(command.IsShiftRight);
            }

            /// <summary>
            /// Parse out a source command with the specified name and no bang flag
            /// </summary>
            [Test]
            public void Parse_Source_Simple()
            {
                var command = ParseLineCommand("source test.txt").AsSource();
                Assert.IsFalse(command.Item1);
                Assert.AreEqual("test.txt", command.Item2);
            }

            /// <summary>
            /// Parse out a source command with the specified name and bang flag
            /// </summary>
            [Test]
            public void Parse_Source_WithBang()
            {
                var command = ParseLineCommand("source! test.txt").AsSource();
                Assert.IsTrue(command.Item1);
                Assert.AreEqual("test.txt", command.Item2);
            }

            /// <summary>
            /// Parse out the unmapping of keys
            /// </summary>
            [Test]
            public void Parse_UnmapKeys_Simple()
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

            [Test]
            public void Parse_Write_Simple()
            {
                var write = ParseLineCommand("w").AsWrite();
                Assert.IsTrue(write.Item1.IsNone);
                Assert.IsFalse(write.Item2);
                Assert.IsTrue(write.Item4.IsNone());
            }

            /// <summary>
            /// Parse out the write command given a file option
            /// </summary>
            [Test]
            public void Parse_Write_ToFile()
            {
                var write = ParseLineCommand("w example.txt").AsWrite();
                Assert.IsTrue(write.Item1.IsNone);
                Assert.IsFalse(write.Item2);
                Assert.AreEqual("example.txt", write.Item4.Value);
            }

            [Test]
            public void Parse_WriteAll_Simple()
            {
                var writeAll = ParseLineCommand("wall").AsWriteAll();
                Assert.IsFalse(writeAll.Item);
            }

            /// <summary>
            /// Parse out the :wall command with the ! option
            /// </summary>
            [Test]
            public void Parse_WriteAll_WithBang()
            {
                var writeAll = ParseLineCommand("wall!").AsWriteAll();
                Assert.IsTrue(writeAll.Item);
            }

            /// <summary>
            /// Verify that we can parse out a yank command with a corresponding range
            /// </summary>
            [Test]
            public void Parse_Yank_WithRange()
            {
                var yank = ParseLineCommand("'a,'by");
                Assert.IsTrue(yank.IsYank);
            }

            /// <summary>
            /// When we pass in a full command name to try expand it shouldn't have any effect
            /// </summary>
            [Test]
            public void TryExpand_Full()
            {
                var parser = new Parser("");
                Assert.AreEqual("close", parser.TryExpand("close"));
            }

            /// <summary>
            /// Make sure the abbreviation can be expanded
            /// </summary>
            [Test]
            public void TryExpand_Abbrevation()
            {
                var parser = new Parser("");
                foreach (var tuple in Parser.s_LineCommandNamePair)
                {
                    if (!String.IsNullOrEmpty(tuple.Item2))
                    {
                        Assert.AreEqual(tuple.Item1, parser.TryExpand(tuple.Item2));
                    }
                }
            }
        }
    }
}
