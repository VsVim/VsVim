using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.FSharp.Collections;
using Vim.Extensions;
using Vim.Interpreter;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class ParserTest
    {
        protected Parser CreateParser(params string[] lines)
        {
            return new Parser(new GlobalSettings(), VimUtil.CreateVimData(), lines);
        }

        protected Parser CreateParserOfLines(string text)
        {
            var lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            return CreateParser(lines);
        }

        /// <summary>
        /// Assert that parsing the given line command produces the specific error
        /// </summary>
        protected void AssertParseLineCommandError(string command, string error)
        {
            var lineCommand = VimUtil.ParseLineCommand(command);
            Assert.True(lineCommand.IsParseError);
            Assert.Equal(error, lineCommand.AsParseError().Item);
        }

        protected LineCommand ParseLineCommand(string text)
        {
            return VimUtil.ParseLineCommand(text);
        }

        public sealed class CallTest : ParserTest
        {
            [Fact]
            public void Normal()
            {
                Assert.True(ParseLineCommand(@":call Foo()").IsCall);
            }

            [Fact]
            public void SidTest()
            {
                Assert.True(ParseLineCommand(@":call <SID>Foo()").IsCall);
            }

            [Fact]
            public void OtherScriptLocalPrefix()
            {
                Assert.True(ParseLineCommand(@":call s:Foo()").IsCall);
            }
        }

        public sealed class StringLiteralTest : ParserTest
        {
            public string ParseStringLiteral(string text)
            {
                var parser = CreateParser(text);
                var parseResult = parser.ParseStringLiteral();
                Assert.True(parseResult.IsSucceeded);
                return parseResult.AsSucceeded().Item.AsString().Item;
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

        public sealed class AutoCommandTest : ParserTest
        {
            private AutoCommandDefinition AssertAddAutoCommand(string line)
            {
                var lineCommand = ParseLineCommand(line);
                Assert.True(lineCommand.IsAddAutoCommand);
                return lineCommand.AsAddAutoCommand().Item;
            }

            [Fact]
            public void BufEnterNoGroup()
            {
                var autoCommand = AssertAddAutoCommand("autocmd BufEnter *.html set ts=4");
                Assert.Equal(EventKind.BufEnter, autoCommand.EventKinds.Single());
                Assert.Equal("*.html", autoCommand.Patterns.Single());
                Assert.Equal("set ts=4", autoCommand.LineCommandText);
            }

            [Fact]
            public void EventKindIsCaseInsensitive()
            {
                var autoCommand = AssertAddAutoCommand("autocmd bufenter *.html set ts=4");
                Assert.Equal(EventKind.BufEnter, autoCommand.EventKinds.Single());
                Assert.Equal("*.html", autoCommand.Patterns.Single());
                Assert.Equal("set ts=4", autoCommand.LineCommandText);
            }

            [Fact]
            public void TwoEventKinds()
            {
                var autoCommand = AssertAddAutoCommand("autocmd BufEnter,BufAdd *.html set ts=4");
                Assert.Equal(
                    new[] { EventKind.BufEnter, EventKind.BufAdd },
                    autoCommand.EventKinds);
                Assert.Equal("*.html", autoCommand.Patterns.Single());
                Assert.Equal("set ts=4", autoCommand.LineCommandText);
            }

            [Fact]
            public void ManyEventKinds()
            {
                var autoCommand = AssertAddAutoCommand("autocmd BufEnter,BufAdd,BufCreate *.html set ts=4");
                Assert.Equal(
                    new[] { EventKind.BufEnter, EventKind.BufAdd, EventKind.BufCreate },
                    autoCommand.EventKinds);
                Assert.Equal("*.html", autoCommand.Patterns.Single());
                Assert.Equal("set ts=4", autoCommand.LineCommandText);
            }

            [Fact]
            public void ManyPatterns()
            {
                var autoCommand = AssertAddAutoCommand("autocmd BufEnter *.html,*.cs set ts=4");
                Assert.Equal(EventKind.BufEnter, autoCommand.EventKinds.Single());
                Assert.Equal(new[] { "*.html", "*.cs" }, autoCommand.Patterns);
                Assert.Equal("set ts=4", autoCommand.LineCommandText);
            }
        }

        public sealed class DisplayLetTest : ParserTest
        {
            private List<VariableName> Parse(string line)
            {
                var lineCommand = ParseLineCommand(line);
                Assert.True(lineCommand.IsDisplayLet);
                return ((LineCommand.DisplayLet)lineCommand).Item.ToList();
            }

            [Fact]
            public void Empty()
            {
                var list = Parse("let");
                Assert.Equal(0, list.Count);
            }

            [Fact]
            public void SingleVariable()
            {
                var list = Parse("let x");
                Assert.Equal(new[] { "x" }, list.Select(x => x.Name));
            }

            [Fact]
            public void MultiVariable()
            {
                var list = Parse("let x y");
                Assert.Equal(new[] { "x", "y" }, list.Select(x => x.Name));
            }
        }

        /// <summary>
        /// Handle edge cases in the parser.  Mostly an implementation test of the critical functions around lines
        /// </summary>
        public abstract class EdgeCasesTest : ParserTest
        {
            public sealed class CreationTest : EdgeCasesTest
            {
                [Fact]
                public void AllBlankLines()
                {
                    var parser = CreateParser("", "", "    ");
                    Assert.True(parser.IsDone);
                }

                [Fact]
                public void AllCommentLines()
                {
                    var parser = CreateParser(@""" this is a comment", @"    "" another comment");
                    Assert.True(parser.IsDone);
                }

                [Fact]
                public void AllCommentAndBlankLines()
                {
                    var parser = CreateParser(@""" this is a comment", "   ");
                    Assert.True(parser.IsDone);
                }

                [Fact]
                public void MovePasteInitialBlanks()
                {
                    var parser = CreateParser("   ", "cat", "dog");
                    Assert.Equal("cat", parser.Tokenizer.CurrentToken.TokenText);
                }
            }

            public sealed class MoveNextLineTest : EdgeCasesTest
            {
                [Fact]
                public void PastBlank()
                {
                    var parser = CreateParser("cat", " ", "dog");
                    Assert.True(parser.MoveToNextLine());
                    Assert.Equal("dog", parser.Tokenizer.CurrentToken.TokenText);
                }

                [Fact]
                public void PastComment()
                {
                    var parser = CreateParser("cat", @""" comment ", "dog");
                    Assert.True(parser.MoveToNextLine());
                    Assert.Equal("dog", parser.Tokenizer.CurrentToken.TokenText);
                }

                [Fact]
                public void OnlyBlankLinesRemaining()
                {
                    var parser = CreateParser("cat", @""" comment ");
                    Assert.False(parser.MoveToNextLine());
                    Assert.True(parser.IsDone);
                }
            }

            /// <summary>
            /// Need to be able to handle the colon being the first thing in a parser command
            /// </summary>
            public sealed class ColonPrefixTest : EdgeCasesTest
            {
                [Fact]
                public void Simple()
                {
                    var parser = CreateParser(":let x = 4");
                    var lineCommand = parser.ParseNextCommand();
                    Assert.True(lineCommand.IsLet);
                }

                [Fact]
                public void Multiline()
                {
                    var parser = CreateParser(":let x = 4", ":set hlsearch");
                    var lineCommand = parser.ParseNextCommand();
                    Assert.True(lineCommand.IsLet);

                    lineCommand = parser.ParseNextCommand();
                    Assert.True(lineCommand.IsSet);
                }
            }
        }

        public sealed class IfTest : ParserTest
        {
            private void AssertIf(LineCommand lineCommand, params int[] expected)
            {
                Assert.True(lineCommand.IsIf);
                AssertIf(lineCommand.AsIf().Item.ToList(), expected, 0);
            }

            private void AssertIf(List<ConditionalBlock> conditionalBlockList, int[] expected, int index)
            {
                Assert.True(index < expected.Length);
                Assert.Equal(conditionalBlockList[index].LineCommands.Length, expected[index]);
            }

            private void AssertBadParse(params string[] lines)
            {
                var parser = VimUtil.CreateParser();
                parser.Reset(lines);
                var result = parser.ParseSingleCommand();
                Assert.True(result.IsParseError);
            }

            private LineCommand Parse(params string[] lines)
            {
                var parser = VimUtil.CreateParser();
                parser.Reset(lines);
                return parser.ParseSingleCommand();
            }

            [Fact]
            public void SimpleIfOnly()
            {
                var lineCommand = Parse("if 42", "set ts=4", "endif");
                AssertIf(lineCommand, 1);
            }

            [Fact]
            public void SimpleIfMultiStatements()
            {
                var lineCommand = Parse("if 42", "set ts=4", "set ts=8", "endif");
                AssertIf(lineCommand, 2);
            }

            [Fact]
            public void IfWithElse()
            {
                var lineCommand = Parse("if 42", "set ts=4", "else", "set ts=8", "endif");
                AssertIf(lineCommand, 1, 1);
            }

            [Fact]
            public void IfWithElseIf()
            {
                var lineCommand = Parse("if 42", "set ts=4", "elseif 42", "set ts=8", "endif");
                AssertIf(lineCommand, 1, 1);
            }

            [Fact]
            public void IfWithElseIfElse()
            {
                var lineCommand = Parse("if 42", "set ts=4", "elseif 42", "set ts=8", "else", "set ts=10", "endif");
                AssertIf(lineCommand, 1, 1, 1);
            }

            [Fact]
            public void BadIfNoEndif()
            {
                AssertBadParse("if 42", "set ts=2");
            }

            [Fact]
            public void BadElseNoEndIf()
            {
                AssertBadParse("if 42", "set ts=2", "else");
            }

            [Fact]
            public void BadElseIfAfterElse()
            {
                AssertBadParse("if 42", "set ts=2", "else", "set ts=2", "elseif 42", "endif");
            }

            [Fact]
            public void BadElseAfterElse()
            {
                AssertBadParse("if 42", "else", "else", "endif");
            }
        }

        public abstract class FunctionTest : ParserTest
        {
            private void AssertFunc(string functionText, string name = null, int lineCount = -1, bool? isForced = null)
            {
                var parser = CreateParserOfLines(functionText);
                var lineCommand = parser.ParseNextCommand();
                Assert.True(lineCommand is LineCommand.Function);
                var func = ((LineCommand.Function)lineCommand).Item;
                if (name != null)
                {
                    Assert.Equal(name, func.Definition.Name);
                }

                if (lineCount >= 0)
                {
                    Assert.Equal(lineCount, func.LineCommands.Length);
                }

                if (isForced.HasValue)
                {
                    Assert.Equal(isForced.Value, func.Definition.IsForced);
                }
            }

            private void AssertNotFunc(string functionText)
            {
                var parser = CreateParserOfLines(functionText);
                var lineCommand = parser.ParseNextCommand();
                Assert.True(lineCommand.IsParseError);
            }

            private Function ParseFunction(string functionText)
            {
                var parser = CreateParserOfLines(functionText);
                var lineCommand = parser.ParseNextCommand();
                Assert.True(lineCommand.IsFunction);
                return ((LineCommand.Function)lineCommand).Item;
            }

            private FunctionDefinition ParseFunctionDefinition(string definitionText)
            {
                var parser = CreateParserOfLines(definitionText);
                var lineCommand = parser.ParseNextLine();
                Assert.True(lineCommand.IsFunctionStart);
                return ((LineCommand.FunctionStart)lineCommand).Item.Value;
            }

            private void AssertBadFunctionDefinition(string definitionText)
            {
                var parser = CreateParserOfLines(definitionText);
                var lineCommand = parser.ParseNextLine();
                Assert.True(lineCommand.IsFunctionStart);
                Assert.True(((LineCommand.FunctionStart)lineCommand).Item.IsNone());
            }

            public sealed class CompleteTest : FunctionTest
            {
                [Fact]
                public void NoLines()
                {
                    var text = @"
function First()
endfunction";
                    AssertFunc(text, "First", 0, isForced: false);
                }

                /// <summary>
                /// A ! is allowed to follow the function word 
                /// </summary>
                [Fact]
                public void BangAfterFunction()
                {
                    var text = @"
function! First()
endfunction";
                    AssertFunc(text, "First", 0, isForced: true);
                }

                /// <summary>
                /// A ! is allowed to follow the function word but it must be directly beside it
                /// </summary>
                [Fact]
                public void BangAfterFunctionWithSpace()
                {
                    var text = @"
function ! First()
endfunction";
                    AssertNotFunc(text);
                }

                [Fact]
                public void NameMustStartWithCap()
                {
                    var text = @"
function first()
endfunction";
                    AssertNotFunc(text);
                }

                [Fact]
                public void NameCanBeLowerAfterColon()
                {
                    var text = @"
function s:first()
endfunction";
                    AssertFunc(text, "first", 0);
                }

                /// <summary>
                /// Make sure the code can handle blank lines 
                /// </summary>
                [Fact]
                public void BlankLineCommands()
                {
                    var text = @"
function s:first()



endfunction";
                    AssertFunc(text, "first");
                }

                /// <summary>
                /// A parse error inside a function should still produce a function value.  The bad command should 
                /// just be stored inside the function 
                /// </summary>
                [Fact]
                public void InternalParseErrors()
                {
                    var text = @"
function Test() 
  this is a parse error
  let y = 13
endfunction
let x = 42
";

                    var parser = CreateParserOfLines(text);

                    // The first command should be the completed function.  
                    var functionCommand = parser.ParseNextCommand();
                    Assert.True(functionCommand.IsFunction);
                    var functionCommands = ((LineCommand.Function)functionCommand).Item.LineCommands;
                    Assert.True(functionCommands[0].IsParseError);
                    Assert.True(functionCommands[1].IsLet);


                    // Now parse out the :let command
                    var letCommand = parser.ParseNextCommand();
                    Assert.True(letCommand.IsLet);
                }

                [Fact]
                public void IsSeveral()
                {
                    var text = @"
function Test() dict abort
endfunction
";

                    var function = ParseFunction(text);
                    Assert.True(function.Definition.IsDictionary);
                    Assert.True(function.Definition.IsAbort);
                }

                [Fact]
                public void Issue1086()
                {
                    var text = @"
function! <SID>StripTrailingWhitespace()
    "" Preparation: save last search, and cursor position.
    let _s=@/
    let l = line(""."")
    let c = col(""."")
    "" Do the business:
    %s/\s\+$//e
    "" Clean up: restore previous search history, and cursor position
    let @/=_s
    call cursor(l, c)
endfunction
let x = 42
";

                    var parser = CreateParserOfLines(text);

                    // For the moment we are unable to parse this function because it has inner commands that we 
                    // don't support.  However we should still parse the function as a function even though we
                    // don't support the inner commands
                    var functionCommand = parser.ParseNextCommand();
                    Assert.True(functionCommand.IsFunction);

                    // Now parse out the :let command
                    var letCommand = parser.ParseNextCommand();
                    Assert.True(letCommand.IsLet);
                }
            }

            public sealed class DefinitionTest : FunctionTest
            {
                [Fact]
                public void IsAbort()
                {
                    var definition = ParseFunctionDefinition(@"function Test() abort");
                    Assert.True(definition.IsAbort);
                }

                [Fact]
                public void IsRange()
                {
                    var definition = ParseFunctionDefinition(@"function Test() range");
                    Assert.True(definition.IsRange);
                }

                [Fact]
                public void IsDict()
                {
                    var definition = ParseFunctionDefinition(@"function Test() dict");
                    Assert.True(definition.IsDictionary);
                }

                [Fact]
                public void IsForced()
                {
                    var definition = ParseFunctionDefinition(@"function! Test() dict");
                    Assert.True(definition.IsForced);
                }

                /// <summary>
                /// The bang must be immediately after the function.  There can be no spaces
                /// </summary>
                [Fact]
                public void BangMustBeAfterFunction()
                {
                    AssertBadFunctionDefinition(@"function ! Test()"); 
                }

                /// <summary>
                /// A function can begin with a lower case if it is a script local function
                /// </summary>
                [Fact]
                public void ScriptLocal()
                {
                    var definition = ParseFunctionDefinition(@"function s:test()");
                    Assert.True(definition.IsScriptLocal);
                }

                [Fact]
                public void ScriptIdPrefix()
                {
                    var definition = ParseFunctionDefinition(@"function <SID>test()");
                    Assert.True(definition.IsScriptLocal);
                }
            }
        }

        public sealed class NumberTest : ParserTest
        {
            private VariableValue ParseNumberValue(string text)
            {
                var parser = CreateParser(text);
                var parseResult = parser.ParseNumberConstant();
                Assert.True(parseResult.IsSucceeded);
                return parseResult.AsSucceeded().Item.AsConstantValue().Item;
            }

            private int ParseNumber(string text)
            {
                return ParseNumberValue(text).AsNumber().Item;
            }

            [Fact]
            public void SimpleDecimal()
            {
                Assert.Equal(42, ParseNumber("42"));
            }

            [Fact]
            public void SimpleHex()
            {
                Assert.Equal(0x1a, ParseNumber("0x1a"));
            }

            [Fact]
            public void HexWithLetterStart()
            {
                Assert.Equal(0xf0, ParseNumber("0xf0"));
            }

            [Fact]
            public void HexWithAllLetters()
            {
                Assert.Equal(0xfa, ParseNumber("0xfa"));
            }
        }

        public sealed class QuickFixTest : ParserTest
        {
            [Fact]
            public void NextSimple()
            {
                var quickFix = ParseLineCommand("cn").AsQuickFixNext();
                Assert.True(quickFix.Item1.IsNone());
                Assert.False(quickFix.item2);
            }

            [Fact]
            public void NextWithArgs()
            {
                var quickFix = ParseLineCommand("2cn!").AsQuickFixNext();
                Assert.Equal(2, quickFix.Item1.Value);
                Assert.True(quickFix.Item2);
            }

            [Fact]
            public void PreviousSimple()
            {
                var quickFix = ParseLineCommand("cp").AsQuickFixPrevious();
                Assert.True(quickFix.Item1.IsNone());
                Assert.False(quickFix.item2);
            }

            [Fact]
            public void PreviousWithArgs()
            {
                var quickFix = ParseLineCommand("2cp!").AsQuickFixPrevious();
                Assert.Equal(2, quickFix.Item1.Value);
                Assert.True(quickFix.Item2);
            }
        }

        public sealed class StringConstant : ParserTest
        {
            public string ParseStringConstant(string text)
            {
                var parser = CreateParser(text);
                parser.Tokenizer.TokenizerFlags = TokenizerFlags.AllowDoubleQuote;
                var parseResult = parser.ParseStringConstant();
                Assert.True(parseResult.IsSucceeded);
                return parseResult.AsSucceeded().Item.AsString().Item;
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

            [Fact]
            public void SpaceBeforeCommandName()
            {
                AssertSubstitute(" s/ /", " ", "");
            }

            [Fact]
            public void SpaceBetweenNameAndPattern()
            {
                AssertSubstitute("s / /", " ", "");
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
                var parser = CreateParser(text);
                var lineRange = parser.ParseLineRange();
                Assert.False(lineRange.IsNone);
                return lineRange;
            }

            private LineSpecifier ParseLineSpecifier(string text)
            {
                var parser = CreateParser(text);
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
                    var parser = CreateParser("<" + cur + ">");
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
                var parser = CreateParser(text);
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
                var lineCommand = VimUtil.ParseLineCommand("close foo");
                Assert.True(lineCommand.IsParseError(Resources.CommandMode_TrailingCharacters));
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
                var parser = CreateParser("");
                Assert.Equal("close", parser.TryExpand("close"));
            }

            /// <summary>
            /// Make sure the abbreviation can be expanded
            /// </summary>
            [Fact]
            public void TryExpand_Abbrevation()
            {
                var parser = CreateParser("");
                foreach (var tuple in Parser.s_LineCommandNamePair)
                {
                    if (!String.IsNullOrEmpty(tuple.Item2))
                    {
                        Assert.Equal(tuple.Item1, parser.TryExpand(tuple.Item2));
                    }
                }
            }

            [Fact]
            public void Version()
            {
                var command = ParseLineCommand("version");
                Assert.True(command.IsVersion);
            }

            [Fact]
            public void Version_Short()
            {
                var command = ParseLineCommand("ve");
                Assert.True(command.IsVersion);
            }

            [Fact]
            public void Registers()
            {
                Action<string> check = (text) =>
                {
                    var command = ParseLineCommand(text);
                    Assert.True(command.IsDisplayRegisters);
                };

                check("reg b 1");
                check("reg");
                check("reg a");
            }
        }
    }
}
