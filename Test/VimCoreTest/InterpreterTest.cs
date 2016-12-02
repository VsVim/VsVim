using System;
using System.Collections.Generic;
using System.Linq;
using EditorUtils;
using Microsoft.FSharp.Collections;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.Extensions;
using Vim.Interpreter;
using Vim.UnitTest.Mock;
using Xunit;
using Microsoft.FSharp.Core;

namespace Vim.UnitTest
{
    public abstract class InterpreterTest : VimTestBase
    {
        private IVimBufferData _vimBufferData;
        private IVimBuffer _vimBuffer;
        private IVimTextBuffer _vimTextBuffer;
        private ITextBuffer _textBuffer;
        private ITextView _textView;
        private IVimData _vimData;
        private VimInterpreter _interpreter;
        private TestableStatusUtil _statusUtil;
        private IVimGlobalSettings _globalSettings;
        private IVimLocalSettings _localSettings;
        private IVimWindowSettings _windowSettings;
        private IKeyMap _keyMap;

        /// <summary>
        /// A valid directory in the file system
        /// </summary>
        private static string ValidDirectoryPath
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); }
        }

        /// <summary>
        /// An invalid directory in the file system
        /// </summary>
        private static string InvalidDirectoryPath
        {
            get { return @"q:\invalid\path"; }
        }

        protected virtual void Create(params string[] lines)
        {
            _statusUtil = new TestableStatusUtil();
            _vimData = Vim.VimData;
            _vimBufferData = CreateVimBufferData(
                CreateTextView(lines),
                statusUtil: _statusUtil);
            _vimBuffer = CreateVimBuffer(_vimBufferData);
            _vimTextBuffer = _vimBufferData.VimTextBuffer;
            _windowSettings = _vimBufferData.WindowSettings;
            _localSettings = _vimBufferData.LocalSettings;
            _globalSettings = _localSettings.GlobalSettings;
            _textBuffer = _vimBufferData.TextBuffer;
            _textView = _vimBufferData.TextView;
            _interpreter = new global::Vim.Interpreter.VimInterpreter(
                _vimBuffer,
                CommonOperationsFactory.GetCommonOperations(_vimBufferData),
                FoldManagerFactory.GetFoldManager(_vimBufferData.TextView),
                new FileSystem(),
                BufferTrackingService);
            _keyMap = Vim.KeyMap;
        }

        /// <summary>
        /// Parse and run the specified command
        /// </summary>
        private void ParseAndRun(string command)
        {
            if (command.Length > 0 && command[0] == ':')
            {
                command = command.Substring(1);
            }

            var lineCommand = VimUtil.ParseLineCommand(command);
            _interpreter.RunLineCommand(lineCommand);
        }

        public sealed class CallTest : InterpreterTest
        {
            [Fact]
            public void NoArguments()
            {
                Create();
                ParseAndRun("call target()");
                Assert.Equal(Resources.Interpreter_CallNotSupported("target"), _statusUtil.LastError);
            }

            [Fact]
            public void OneArguments()
            {
                Create();
                ParseAndRun("call target(a)");
                Assert.Equal(Resources.Interpreter_CallNotSupported("target"), _statusUtil.LastError);
            }

            [Fact]
            public void TwoArguments()
            {
                Create();
                ParseAndRun("call target(a, b)");
                Assert.Equal(Resources.Interpreter_CallNotSupported("target"), _statusUtil.LastError);
            }
        }

        public sealed class CopyTest : InterpreterTest
        {
            /// <summary>
            /// Copy to a single line
            /// </summary>
            [Fact]
            public void ToSingleLine()
            {
                Create("cat", "dog", "fish", "tree");
                _textView.MoveCaretToLine(2);
                ParseAndRun("co 1");
                Assert.Equal(
                    new[] { "cat", "fish", "dog", "fish", "tree" },
                    _textBuffer.GetLines().ToArray());
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// When copying to a line range the paste should come after the first line and
            /// not the last as is common with other commands
            /// </summary>
            [Fact]
            public void ToLineRange()
            {
                Create("cat", "dog", "fish", "tree");
                _textView.MoveCaretToLine(3);
                ParseAndRun("co 1,3");
                Assert.Equal(
                    new[] { "cat", "tree", "dog", "fish", "tree" },
                    _textBuffer.GetLines().ToArray());
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Copy to a single line plus a count should be copied to line + count 
            /// </summary>
            [Fact]
            public void ToSingleLineAndCount()
            {
                Create("cat", "dog", "fish", "bear", "tree");
                _textView.MoveCaretToLine(4);
                ParseAndRun("co 1 2");
                Assert.Equal(
                    new[] { "cat", "dog", "fish", "tree", "bear", "tree" },
                    _textBuffer.GetLines().ToArray());
                Assert.Equal(_textBuffer.GetLine(3).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// When copying to a line range and a count the count is simply ignored
            /// </summary>
            [Fact]
            public void ToLineRangeAndCount()
            {
                Create("cat", "dog", "fish", "bear", "tree");
                _textView.MoveCaretToLine(4);
                ParseAndRun("co 1,3 2");
                Assert.Equal(
                    new[] { "cat", "tree", "dog", "fish", "bear", "tree" },
                    _textBuffer.GetLines().ToArray());
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Copy to the first line
            /// </summary>
            [Fact]
            public void ToFirstLine()
            {
                Create("cat", "dog", "fish", "bear", "tree");
                _textView.MoveCaretToLine(3);
                ParseAndRun("co -4");

                Assert.Equal(
                    new[] { "bear", "cat", "dog", "fish", "bear", "tree" },
                    _textBuffer.GetLines().ToArray());

                Assert.Equal(_textBuffer.GetLine(0).Start, _textView.GetCaretPoint());
            }
        }

        public sealed class DisplayMarkTest : InterpreterTest
        {
            private static readonly FSharpList<Mark> s_emptyList = FSharpList<Mark>.Empty;

            public DisplayMarkTest()
            {
            }

            protected override void Create(params string[] lines)
            {
                base.Create(lines);
                _textBuffer.Properties[MockVimHost.FileNameKey] = "test.txt";
            }

            public void Verify(char mark, int line, int column, int index = 1)
            {
                var msg = String.Format(" {0}  {1,5}{2,5} test.txt", mark, line, column);
                Assert.Equal(msg, _statusUtil.LastStatusLong[index]);
            }

            [Fact]
            public void SingleLocalMark()
            {
                Create("cat dog");
                _vimTextBuffer.SetLocalMark(LocalMark.NewLetter(Letter.C), 0, 1);
                _interpreter.RunDisplayMarks(s_emptyList);
                Verify('c', 1, 1, 2);
            }

            /// <summary>
            /// The local marks should be displayed in alphabetical order 
            /// </summary>
            [Fact]
            public void MultipleLocalMarks()
            {
                Create("cat dog");
                _vimTextBuffer.SetLocalMark(LocalMark.NewLetter(Letter.B), 0, 1);
                _vimTextBuffer.SetLocalMark(LocalMark.NewLetter(Letter.A), 0, 2);
                _interpreter.RunDisplayMarks(s_emptyList);
                Verify('a', line: 1, column: 2, index: 2);
                Verify('b', line: 1, column: 1, index: 3);
            }
        }

        public sealed class EditTest : InterpreterTest
        {
            private bool _didReloadRun;

            public EditTest()
            {
                Create("");
                VimHost.ReloadFunc = (textView) =>
                {
                    Assert.Same(textView, _vimBuffer.TextView);
                    _didReloadRun = true;
                    return true;
                };
            }

            [Fact]
            public void ReloadNonDirty()
            {
                _textBuffer.SetText("cat dog");
                ParseAndRun("e");
                VimHost.IsDirtyFunc = delegate { return false; };
                Assert.True(_didReloadRun);
            }

            [Fact]
            public void ReloadDirty()
            {
                _textBuffer.SetText("cat dog");
                VimHost.IsDirtyFunc = delegate { return true; };
                ParseAndRun("e");
                Assert.False(_didReloadRun);
            }

            [Fact]
            public void ReloadDirtyForce()
            {
                _textBuffer.SetText("cat dog");
                VimHost.IsDirtyFunc = delegate { return true; };
                ParseAndRun("e!");
                Assert.True(_didReloadRun);
            }
        }

        public sealed class SourceTest : InterpreterTest
        {
            private readonly Mock<IFileSystem> _fileSystem;

            public SourceTest()
            {
                Create();
                _fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
                _interpreter._fileSystem = _fileSystem.Object;
                ((Vim)Vim).FileSystem = _fileSystem.Object;
            }

            private void SetText(string fileName, params string[] lines)
            {
                _fileSystem
                    .Setup(x => x.ReadAllLines(fileName))
                    .Returns(FSharpOption.Create(lines));
            }

            [Fact]
            public void Simple()
            {
                SetText("test.txt", ":set ts=12");
                ParseAndRun(":source test.txt");
                Assert.Equal(12, _vimBuffer.LocalSettings.TabStop);
            }

            [Fact]
            public void Issue1595()
            {
                SetText("test.txt", ":set ts=12");
                ParseAndRun(":source test.txt\t\"several windows key bindings");
                Assert.Equal(12, _vimBuffer.LocalSettings.TabStop);
            }

            [Fact]
            public void Issue1699()
            {
                var filePath = "file<name>.txt";
                _fileSystem
                    .Setup(x => x.ReadAllLines(filePath))
                    .Returns(FSharpOption<string[]>.None);
                ParseAndRun(string.Format(":source {0}", filePath));
                Assert.Equal(Resources.CommandMode_CouldNotOpenFile(filePath), _statusUtil.LastError);
            }
        }

        public sealed class SubstituteTest : InterpreterTest
        {
            /// <summary>
            /// When an empty string is provided for the pattern string then the pattern from the last
            /// substitute
            /// </summary>
            [Fact]
            public void EmptySearchUsesLastSearch()
            {
                Create("cat tree");
                Vim.VimData.LastSearchData = new SearchData("cat", new SearchPath(0));
                ParseAndRun("s//dog/");
                Assert.Equal("dog tree", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Make sure that back slashes are properly handled in the replace 
            /// </summary>
            [Fact]
            public void Backslashes()
            {
                Create("cat");
                ParseAndRun(@"s/a/\\\\");
                Assert.Equal(@"c\\t", _textBuffer.GetLine(0).GetText());
            }

            [Fact]
            public void DoubleQuotesPattern()
            {
                Create(@"""cat""");
                ParseAndRun(@"s/""cat""/dog");
                Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
            }

            [Fact]
            public void DoubleQuotesReplace()
            {
                Create(@"cat");
                ParseAndRun(@"s/cat/""dog""");
                Assert.Equal(@"""dog""", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// This is a bit of a special case around the escape sequence for a new line.  The escape
            /// actually escapes the backslash and doesn't add a new line
            /// </summary>
            [Fact]
            public void EscapedLooksLikeNewLine()
            {
                Create("cat", "dog");
                ParseAndRun(@"s/$/\\n\\/");
                Assert.Equal(@"cat\n\", _textBuffer.GetLine(0).GetText());
                Assert.Equal(@"dog", _textBuffer.GetLine(1).GetText());
            }

            /// <summary>
            /// The $ marker needs to be treated as a zero width assertion.  Don't replace the new line
            /// just the rest of the string
            /// </summary>
            [Fact]
            public void WordAndEndOfLine()
            {
                Create("cat cat", "fish");
                ParseAndRun(@"s/cat$/dog/");
                Assert.Equal("cat dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal("fish", _textBuffer.GetLine(1).GetText());
            }

            /// <summary>
            /// Matching $ as part of the regex is a zero width match.  It can't be used to delete the 
            /// end of the line
            /// </summary>
            [Fact]
            public void EndOfLineIsZeroWidth()
            {
                Create("cat", "dog", "fish");
                ParseAndRun(@"%s/$//");
                Assert.Equal(3, _textBuffer.CurrentSnapshot.LineCount);
            }

            /// <summary>
            /// Make sure a replace here at the end of the line happens after
            /// </summary>
            [Fact]
            public void EndOfLineIsZeroWidth2()
            {
                Create("cat", "dog", "fish");
                ParseAndRun(@"%s/$/ hat/");
                Assert.Equal(
                    new[] { "cat hat", "dog hat", "fish hat" },
                    _textBuffer.GetLines().ToArray());
            }

            [Fact]
            public void EndOfLineWithGroupReplace()
            {
                Create(
                    @"  { ""cat"",  CAT_VALUE /* 100 */ },",
                    @"  { ""dog"",  BAT_VALUE /* 101 */ },",
                    @"  { ""bat"",  BAT_VALUE /* 102 */ }");
                ParseAndRun(@"%s/\(\s\+\){\s*\(""\w\+"",\).*$/\1\2/");
                Assert.Equal(3, _textBuffer.CurrentSnapshot.LineCount);
                Assert.Equal(@"  ""cat"",", _textBuffer.GetLine(0).GetText());
                Assert.Equal(@"  ""dog"",", _textBuffer.GetLine(1).GetText());
                Assert.Equal(@"  ""bat"",", _textBuffer.GetLine(2).GetText());
            }

            /// <summary>
            /// The \n character is not zero width and can be used to delete the new line
            /// </summary>
            [Fact]
            public void NewLineIsNotZeroWidth()
            {
                Create("cat", "dog", "fish");
                ParseAndRun(@"s/\n//");
                Assert.Equal("catdog", _textBuffer.GetLine(0).GetText());
                Assert.Equal("fish", _textBuffer.GetLine(1).GetText());
            }
        }

        public sealed class RunCommandTest : InterpreterTest
        {
            private string _command;
            private string _arguments;

            public RunCommandTest()
            {
                VimHost.RunCommandFunc = (command, arguments, vimData) =>
                    {
                        _command = command;
                        _arguments = arguments;

                        return string.Empty;
                    };
                Create();
            }

            [Fact]
            public void SimpleArgument()
            {
                ParseAndRun(":!echo test");
                Assert.Equal("/c echo test", _arguments);
            }

            /// <summary>
            /// Quotes should not be interpreted as comments when parsing out the arguments of 
            /// a shell command
            /// </summary>
            [Fact]
            public void QuotedArgument()
            {
                ParseAndRun(@":!echo ""test""");
                Assert.Equal(@"/c echo ""test""", _arguments);
            }
        }

        public sealed class RunSearhTest : InterpreterTest
        {
            [Fact]
            public void ForwardSearchUpdatesLastPattern()
            {
                Create("cat", "dog");
                ParseAndRun(":/dog");
                Assert.Equal("dog", _vimData.LastSearchData.Pattern);
                Assert.Equal(SearchPath.Forward, _vimData.LastSearchData.Path);
            }

            [Fact]
            public void BackwardSearchUpdatesLastPattern()
            {
                Create("cat", "dog");
                ParseAndRun(":?dog");
                Assert.Equal("dog", _vimData.LastSearchData.Pattern);
                Assert.Equal(SearchPath.Backward, _vimData.LastSearchData.Path);
            }
        }

        public sealed class RunScriptTest : InterpreterTest
        {
            private void RunScript(params string[] lines)
            {
                _interpreter.RunScript(lines);
            }

            [Fact]
            public void Simple()
            {
                Create("");
                RunScript(
                    ":set incsearch",
                    ":set ts=4");
                Assert.True(_globalSettings.IncrementalSearch);
                Assert.Equal(4, _localSettings.TabStop);
            }

            [Fact]
            public void SimpleWithoutColons()
            {
                Create("");
                RunScript(
                    "set incsearch",
                    "set ts=4");
                Assert.True(_globalSettings.IncrementalSearch);
                Assert.Equal(4, _localSettings.TabStop);
            }

            /// <summary>
            /// Make sure we handle blank lines in the script just fine
            /// </summary>
            [Fact]
            public void HasBlankLine()
            {
                Create("");
                RunScript(
                    "set incsearch",
                    "",
                    "set ts=4");
                Assert.True(_globalSettings.IncrementalSearch);
                Assert.Equal(4, _localSettings.TabStop);
            }

            [Fact]
            public void SyntaxError()
            {
                Create("");
                RunScript(
                    "set incsearch",
                    "'",
                    "set ts=4");
                Assert.True(_globalSettings.IncrementalSearch);
                Assert.Equal(4, _localSettings.TabStop);
            }
        }

        public sealed class SetTest : InterpreterTest
        {
            /// <summary>
            /// Print out the modified settings
            /// </summary>
            [Fact]
            public void PrintModifiedSettings()
            {
                Create("");
                _localSettings.ExpandTab = true;
                ParseAndRun("set");
                Assert.Equal("notimeout" + Environment.NewLine + "expandtab", _statusUtil.LastStatus);
            }

            /// <summary>
            /// Test the assignment of a string value
            /// </summary>
            [Fact]
            public void Assign_StringValue_Global()
            {
                Create("");
                ParseAndRun(@"set sections=cat");
                Assert.Equal("cat", _globalSettings.Sections);
            }

            /// <summary>
            /// Test the assignment of a local string value
            /// </summary>
            [Fact]
            public void Assign_StringValue_Local()
            {
                Create("");
                ParseAndRun(@"set nrformats=alpha");
                Assert.Equal("alpha", _localSettings.NumberFormats);
            }

            /// <summary>
            /// Make sure we can use set for a numbered value setting
            /// </summary>
            [Fact]
            public void Assign_NumberValue()
            {
                Create("");
                ParseAndRun(@"set ts=42");
                Assert.Equal(42, _localSettings.TabStop);
            }

            /// <summary>
            /// Assign multiple values and verify it works
            /// </summary>
            [Fact]
            public void Assign_Many()
            {
                Create("");
                ParseAndRun(@"set ai vb ts=42");
                Assert.Equal(42, _localSettings.TabStop);
                Assert.True(_localSettings.AutoIndent);
                Assert.True(_globalSettings.VisualBell);
            }

            /// <summary>
            /// Make sure that if there are mulitple assignments and one is unsupported that the
            /// others work
            /// 
            /// Raised in issue #764
            /// </summary>
            [Fact]
            public void Assign_ManyUnsupported()
            {
                Create("");
                ParseAndRun(@"set vb t_vb=");
                Assert.True(_globalSettings.VisualBell);
            }

            /// <summary>
            /// Toggle a toggle option off
            /// </summary>
            [Fact]
            public void Toggle_Off()
            {
                Create("");
                _localSettings.ExpandTab = true;
                ParseAndRun(@"set noet");
                Assert.False(_localSettings.ExpandTab);
            }

            /// <summary>
            /// Invert a toggle setting to on
            /// </summary>
            [Fact]
            public void Toggle_InvertOn()
            {
                Create("");
                _localSettings.ExpandTab = false;
                ParseAndRun(@"set et!");
                Assert.True(_localSettings.ExpandTab);
            }

            /// <summary>
            /// Make sure we can deal with a trailing comment
            /// </summary>
            [Fact]
            public void Toggle_TrailingComment()
            {
                Create("");
                _localSettings.AutoIndent = false;
                ParseAndRun(@"set ai ""what's going on?");
                Assert.True(_localSettings.AutoIndent);
            }

            /// <summary>
            /// Invert a toggle setting to off
            /// </summary>
            [Fact]
            public void Toggle_InvertOff()
            {
                Create("");
                _localSettings.ExpandTab = true;
                ParseAndRun(@"set et!");
                Assert.False(_localSettings.ExpandTab);
            }

            /// <summary>
            /// Make sure that we can handle window settings as well in the interpreter
            /// </summary>
            [Fact]
            public void Toggle_WindowSetting()
            {
                Create("");
                ParseAndRun(@"set cursorline");
                Assert.True(_windowSettings.CursorLine);
            }

            [Fact]
            public void DisplaySingleToggleOn()
            {
                Create("");
                _vimBuffer.LocalSettings.ExpandTab = true;
                ParseAndRun(@"set et?");
                Assert.Equal("expandtab", _statusUtil.LastStatus);
            }

            [Fact]
            public void DisplaySingleToggleOff()
            {
                Create("");
                _vimBuffer.LocalSettings.ExpandTab = false;
                ParseAndRun(@"set et?");
                Assert.Equal("noexpandtab", _statusUtil.LastStatus);
            }

            /// <summary>
            /// Check that we don't throw on an invalid setting name
            /// </summary>
            [Fact]
            public void DisplaySettingFake()
            {
                Create("");
                _vimBuffer.LocalSettings.ExpandTab = false;
                ParseAndRun(@"set blah?");
                Assert.Equal(Resources.CommandMode_UnknownOption("blah"), _statusUtil.LastError);
            }

            [Fact]
            public void CommaSettingSingle()
            {
                Create("");
                ParseAndRun(@"set slm=mouse");
                Assert.Equal(SelectModeOptions.Mouse, _globalSettings.SelectModeOptions);
            }

            [Fact]
            public void CommaSettingMultiple()
            {
                Create("");
                ParseAndRun(@"set slm=mouse,key");
                Assert.Equal(SelectModeOptions.Mouse | SelectModeOptions.Keyboard, _globalSettings.SelectModeOptions);
            }

            /// <summary>
            /// In this scenario the quotes should be interpreted as a comment
            /// </summary>
            [Fact]
            public void CommaSettingWithQuotes()
            {
                Create("");
                ParseAndRun(@"set slm=""mouse,key""");
                Assert.Equal(SelectModeOptions.None, _globalSettings.SelectModeOptions);
            }

            [Fact]
            public void NumberValue()
            {
                Create("");
                ParseAndRun(@"set ts=3");
                Assert.Equal(3, _localSettings.TabStop);
            }
        }

        public sealed class HelpTest : InterpreterTest
        {
            [Fact]
            public void LinksToWikiWhenNoTopicSpecified()
            {
                Create("");
                ParseAndRun(@"help");
                Assert.Contains("https://github.com/jaredpar/VsVim/wiki", _statusUtil.LastStatus);
            }

            [Fact]
            public void LinksToWikiWhenTopicIsSpecified()
            {
                Create("");
                ParseAndRun(@"help :vsc");
                Assert.Contains("https://github.com/jaredpar/VsVim/wiki", _statusUtil.LastStatus);
            }
        }


        public sealed class HistoryTest : InterpreterTest
        {
            /// <summary>
            /// Pedantically measure the spaces that are involved in the history command. 
            /// </summary>
            [Fact]
            public void PedanticSimple()
            {
                Create("");
                _vimData.CommandHistory.AddRange("cat", "dog");
                ParseAndRun("history");
                var expected = new[]
                {
                    "      # cmd history",
                    "      1 cat",
                    "      2 dog"
                };
                Assert.Equal(expected, _statusUtil.LastStatusLong);
            }

            /// <summary>
            /// When there are more than 1 entries the number of columns for the count shouldn't 
            /// expand.  Instead the number should start taking up columns to the left
            /// </summary>
            [Fact]
            public void PedanticMoreThan10()
            {
                Create("");
                const int count = 15;
                var expected = new List<string>();
                for (int i = 0; i < count; i++)
                {
                    _vimData.CommandHistory.Add("cat" + i);
                }

                ParseAndRun("history");
                var found = _statusUtil.LastStatusLong.ToList();
                Assert.Equal(count + 1, found.Count);
                for (int i = 1; i < found.Count; i++)
                {
                    var line = String.Format("{0,7} {1}", i, "cat" + (i - 1));
                    Assert.Equal(line, found[i]);
                }
            }

            /// <summary>
            /// Once the maximum number of items in the list is completed the list will begin to 
            /// truncate items.  The index though should still reflect the running count of items
            /// </summary>
            [Fact]
            public void TruncatedList()
            {
                Create();
                _vimData.CommandHistory.Limit = 2;
                _vimData.CommandHistory.AddRange("cat", "dog", "fish", "tree");
                ParseAndRun("history");
                var expected = new[]
                {
                    "      # cmd history",
                    "      3 fish",
                    "      4 tree"
                };
                Assert.Equal(expected, _statusUtil.LastStatusLong);
            }
        }

        public sealed class EchoTest : InterpreterTest
        {
            [Fact]
            public void WhenCalledWithNoArgsDoesNothing()
            {
                Create("");
                ParseAndRun(@"echo");
                Assert.Equal(null, _statusUtil.LastStatus);
            }

            [Fact]
            public void WhenPassedIntegerEchoesItOnStatusLine()
            {
                Create("");
                ParseAndRun(@"echo 2");
                Assert.Equal("2", _statusUtil.LastStatus);
            }

            [Fact]
            public void WhenPassedStringLiteralEchoesItOnStatusLine()
            {
                Create("");
                ParseAndRun(@"echo 'foo'");
                Assert.Equal("foo", _statusUtil.LastStatus);
            }

            [Fact]
            public void WhenPassedStringConstantEchoesItOnStatusLine()
            {
                Create("");
                ParseAndRun("echo \"foo\"");
                Assert.Equal("foo", _statusUtil.LastStatus);
            }

            [Fact]
            public void WhenPassedEmptyListEchoesItOnStatusLine()
            {
                Create("");
                ParseAndRun(@"echo []");
                Assert.Equal("[]", _statusUtil.LastStatus);
            }

            [Fact]
            public void WhenPassedNonEmptyListEchoesItOnStatusLine()
            {
                Create("");
                ParseAndRun("echo [1,\"two\",3]");
                Assert.Equal("[1, 'two', 3]", _statusUtil.LastStatus);
            }

            [Fact]
            public void WhenPassedEmptyDictionaryEchoesItOnStatusLine()
            {
                Create("");
                ParseAndRun(@"echo {}");
                Assert.Equal("{}", _statusUtil.LastStatus);
            }

            [Fact]
            public void WhenPassedBooleanSettingWhichIsOffEchoes0OnStatusLine()
            {
                Create("");
                _localSettings.ExpandTab = false;
                ParseAndRun(@"echo &expandtab");
                Assert.Equal("0", _statusUtil.LastStatus);
            }

            [Fact]
            public void WhenPassedBooleanSettingWhichIsOnEchoes1OnStatusLine()
            {
                Create("");
                _localSettings.ExpandTab = true;
                ParseAndRun(@"echo &expandtab");
                Assert.Equal("1", _statusUtil.LastStatus);
            }

            [Fact]
            public void WhenPassedIntegerSettingsEchoesIntegerOnStatusLine()
            {
                Create("");
                _localSettings.TabStop = 4;
                ParseAndRun(@"echo &tabstop");
                Assert.Equal("4", _statusUtil.LastStatus);
            }

            [Fact]
            public void WhenPassedStringSettingsEchoesStringOnStatusLine()
            {
                Create("");
                _globalSettings.Backspace = "eol";
                ParseAndRun(@"echo &backspace");
                Assert.Equal("eol", _statusUtil.LastStatus);
            }

            [Fact]
            public void WhenPassedWindowSettingsEchoesItOnStatusLine()
            {
                Create("");
                _windowSettings.Scroll = 12;
                ParseAndRun(@"echo &scroll");
                Assert.Equal("12", _statusUtil.LastStatus);
            }

            [Fact]
            public void WhenPassedVariableEchoesItOnStatusLine()
            {
                Create("");
                VariableMap["foo"] = VariableValue.NewString("bar");
                ParseAndRun(@"echo foo");
                Assert.Equal("bar", _statusUtil.LastStatus);
            }

            [Fact]
            public void WhenPassedRegisterEchoesItOnStatusLine()
            {
                Create("");
                UnnamedRegister.UpdateValue("Hello, world!");
                ParseAndRun("echo @\"");
                Assert.Equal("Hello, world!", _statusUtil.LastStatus);
            }
        }

        public sealed class ExecuteTest : InterpreterTest
        {
            [Fact]
            public void ExecuteWithNoArgumentsDoesNothing()
            {
                Create("");
                ParseAndRun("execute");
                Assert.Equal(null, _statusUtil.LastStatus);
            }
            [Fact]
            public void ExecuteString()
            {
                Create("");
                ParseAndRun("execute \"echo 'asdf'\"");
                Assert.Equal("asdf", _statusUtil.LastStatus);
            }
        }

        public sealed class LetTest : InterpreterTest
        {
            private Dictionary<string, VariableValue> _variableMap;

            public LetTest()
            {
                _variableMap = VariableMap;
            }

            private void AssertValue(string name, int value)
            {
                AssertValue(name, VariableValue.NewNumber(value));
            }

            private void AssertValue(string name, string value)
            {
                AssertValue(name, VariableValue.NewString(value));
            }

            private void AssertValue(string name, VariableValue value)
            {
                Assert.Equal(value, _variableMap[name]);
            }

            [Fact]
            public void Simple()
            {
                Create("");
                ParseAndRun(@"let x=42");
                AssertValue("x", 42);
            }

            [Fact]
            public void SpaceBeforeNumber()
            {
                Create("");
                ParseAndRun(@"let x= 42");
                AssertValue("x", 42);
            }

            [Fact]
            public void SpaceBeforeString()
            {
                Create("");
                ParseAndRun(@"let x= 'oo'");
                AssertValue("x", "oo");
            }

            [Fact]
            public void SpaceBeforeEquals()
            {
                Create("");
                ParseAndRun(@"let x =42");
                AssertValue("x", 42);
            }

            [Fact]
            public void SpaceInAllLocations()
            {
                Create("");
                ParseAndRun(@"let x = 42");
                AssertValue("x", 42);
            }

            [Fact]
            public void RHSCanBeArbitraryExpression()
            {
                Create("");
                UnnamedRegister.UpdateValue("Hello, world!");
                ParseAndRun("let x=@\"");
                AssertValue("x", "Hello, world!");
            }

            [Fact]
            public void RHSCanBeBinaryExpression()
            {
                Create("");
                ParseAndRun("let x=1+2");
                AssertValue("x", 3);
            }

            [Fact]
            public void LHSCanBeRegisterName()
            {
                Create("");
                ParseAndRun("let @a='copy by assign'");
                var register = RegisterMap.GetRegister(RegisterName.NewNamed(NamedRegister.NameA));
                Assert.Equal("copy by assign", register.StringValue);
            }

            [Fact]
            public void CannotSetVariableToErrorValue()
            {
                Create("");
                _variableMap["x"] = VariableValue.NewNumber(2);
                ParseAndRun("let x = &fakeoption");
                AssertValue("x", 2);
            }
        }

        public sealed class UnletTest : InterpreterTest
        {
            private void AssertGone(string name)
            {
                Assert.False(VariableMap.ContainsKey(name));
            }

            [Fact]
            public void Simple()
            {
                Create();
                ParseAndRun(@"let x=42");
                ParseAndRun(@"unlet x");
                AssertGone("x");
            }

            [Fact]
            public void NotPresent()
            {
                Create();
                ParseAndRun(@"unlet x");
                Assert.Equal(Resources.Interpreter_NoSuchVariable("x"), _statusUtil.LastError);
                Assert.Equal(0, VariableMap.Count);
            }

            /// <summary>
            /// The ! modifier should cause us to ignore the missing variable during an unlet
            /// </summary>
            [Fact]
            public void NotPresentAndIgnored()
            {
                Create();
                ParseAndRun(@"unlet! x");
                Assert.True(String.IsNullOrEmpty(_statusUtil.LastError));
            }

            [Fact]
            public void Multiple()
            {
                Create();
                ParseAndRun(@"let x=42");
                ParseAndRun(@"let y=42");
                ParseAndRun(@"let z=42");
                ParseAndRun(@"unlet x y");
                Assert.Equal(1, VariableMap.Count);
                Assert.True(VariableMap.ContainsKey("z"));
            }
        }

        public sealed class WriteTest : InterpreterTest
        {
            private string _text;
            private string _filePath;

            public WriteTest()
            {
                VimHost.RunSaveTextAs = (text, filePath) =>
                    {
                        _text = text;
                        _filePath = filePath;
                        return true;
                    };
            }

            /// <summary>
            /// Make sure the file path option is supported
            /// </summary>
            [Fact]
            public void FilePath()
            {
                Create("cat");
                ParseAndRun("w foo.txt");
                Assert.Equal("cat", _text);
                Assert.Equal("foo.txt", _filePath);
            }

            [Fact]
            public void NoPath()
            {
                Create("cat");
                ParseAndRun("w");
                Assert.Same(_textBuffer, VimHost.LastSaved);
                Assert.Null(_filePath);
            }

            [Fact]
            public void InvalidCharacters()
            {
                Create("cat");
                var filePath = "file<name>.txt";
                VimHost.RunSaveTextAs = delegate { return false; };
                ParseAndRun(string.Format("w {0}", filePath));
            }

            [Fact]
            public void Issue1699()
            {
                Create("cat");
                VimHost.RunSaveTextAs = delegate { return false; };
                ParseAndRun("w'");
            }
        }


        public sealed class YankTest : InterpreterTest
        {
            [Fact]
            public void Yank1()
            {
                Create("foo", "bar");
                ParseAndRun("y");
                Assert.Equal("foo" + Environment.NewLine, UnnamedRegister.StringValue);
                Assert.Equal(OperationKind.LineWise, UnnamedRegister.OperationKind);
            }

            [Fact]
            public void Yank2()
            {
                Create("foo", "bar", "baz");
                ParseAndRun("1,2y");
                var text = _textView.GetLineRange(0, 1).ExtentIncludingLineBreak.GetText();
                Assert.Equal(text, UnnamedRegister.StringValue);
            }

            [Fact]
            public void Yank3()
            {
                Create("foo", "bar");
                ParseAndRun("y c");
                Assert.Equal(_textView.GetLine(0).ExtentIncludingLineBreak.GetText(), RegisterMap.GetRegister('c').StringValue);
            }

            /// <summary>
            /// Ensure that an invalid line number still registers an error with commands line yank vs. chosing
            /// the last line in the ITextBuffer as it does for jump commands
            /// </summary>
            [Fact]
            public void Yank_InvalidLineNumber()
            {
                Create("hello", "world");
                ParseAndRun("300y");
                Assert.Equal(Resources.Range_Invalid, _statusUtil.LastError);
            }

            /// <summary>
            /// The count should be applied to the specified line number for yank
            /// </summary>
            [Fact]
            public void Yank_WithRangeAndCount()
            {
                Create("cat", "dog", "rabbit", "tree");
                ParseAndRun("2y 1");
                Assert.Equal("dog" + Environment.NewLine, UnnamedRegister.StringValue);
            }
        }

        public sealed class QuickFixTest : InterpreterTest
        {
            private void AssertQuickFix(string command, QuickFix quickFix, int count, bool hasBang)
            {
                var didRun = false;
                VimHost.RunQuickFixFunc =
                    (qf, c, h) =>
                    {
                        Assert.Equal(quickFix, qf);
                        Assert.Equal(count, c);
                        Assert.Equal(hasBang, h);
                        didRun = true;
                    };
                Create("");
                ParseAndRun(command);
                Assert.True(didRun);
            }

            [Fact]
            public void Next()
            {
                AssertQuickFix("cn", QuickFix.Next, 1, hasBang: false);
                AssertQuickFix("1cn", QuickFix.Next, 1, hasBang: false);
                AssertQuickFix("2cn", QuickFix.Next, 2, hasBang: false);
                AssertQuickFix("2cn!", QuickFix.Next, 2, hasBang: true);
            }

            [Fact]
            public void Previous()
            {
                AssertQuickFix("cp", QuickFix.Previous, 1, hasBang: false);
                AssertQuickFix("1cp", QuickFix.Previous, 1, hasBang: false);
                AssertQuickFix("2cp", QuickFix.Previous, 2, hasBang: false);
                AssertQuickFix("2cp!", QuickFix.Previous, 2, hasBang: true);
            }
        }

        public sealed class RegisterTest : InterpreterTest
        {
            private void AssertLineCore(string line, bool doFind)
            {
                var found = false;
                foreach (var status in _statusUtil.LastStatus.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (status == line)
                    {
                        found = true;
                        break;
                    }
                }

                Assert.Equal(doFind, found);
            }

            private void AssertLine(string line)
            {
                AssertLineCore(line, doFind: true);
            }

            private void AssertNotLine(string line)
            {
                AssertLineCore(line, doFind: false);
            }

            /// <summary>
            /// Ensure the unnamed register is displayed if the clipboard has a value 
            /// when the clipboard has text
            /// </summary>
            [Fact]
            public void Unnamed()
            {
                Create("");
                ClipboardDevice.Text = "clipboard";
                ParseAndRun("reg");
                AssertLine(@"""+   clipboard");
            }

            /// <summary>
            /// The clipboard register should not be displayed via the + register
            /// </summary>
            [Fact]
            public void Unnamed_ViaStar()
            {
                Create("");
                ClipboardDevice.Text = "clipboard";
                ParseAndRun("reg");
                AssertNotLine(@"""*   clipboard");
            }

            /// <summary>
            /// Deleting a line should cause register 1 to be filled with the contents
            /// </summary>
            [Fact]
            public void Register1_DeleteLine()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("dd");
                ParseAndRun("reg");
                AssertLine(@"""1   cat^J");
            }

            /// <summary>
            /// Yanking a line should cause register 0 to be filled with the contents
            /// </summary>
            [Fact]
            public void Register0_YankLine()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("yy");
                ParseAndRun("reg");
                AssertLine(@"""0   cat^J");
            }

            [Fact]
            public void LastSearch()
            {
                Create("");
                _vimBuffer.ProcessNotation("/test", enter: true);
                ParseAndRun("reg");
                AssertLine(@"""/   test");
            }

            /// <summary>
            /// The last search register should be binding to the LastSearchData member
            /// </summary>
            [Fact]
            public void LastSearch_ViaLastPattern()
            {
                Create("");
                _vimData.LastSearchData = new SearchData("test", SearchPath.Forward);
                ParseAndRun("reg");
                AssertLine(@"""/   test");
            }
        }

        public sealed class AutoCommandTest : InterpreterTest
        {
            [Fact]
            public void SimpleCommand()
            {
                Create();
                ParseAndRun("autocmd BufEnter *.html set ts=4");
                var autoCommand = _vimData.AutoCommands.Single();
                Assert.Equal("*.html", autoCommand.Pattern);
            }

            [Fact]
            public void MultipleCommands()
            {
                Create();
                ParseAndRun("autocmd BufEnter *.html set ts=4");
                ParseAndRun("autocmd BufEnter *.cs set ts=4");
                var all = _vimData.AutoCommands.ToList();
                Assert.Equal(all[0].Pattern, "*.html");
                Assert.Equal(all[1].Pattern, "*.cs");
            }

            [Fact]
            public void RemoveAll()
            {
                Create();
                ParseAndRun("autocmd BufEnter *.html set ts=4");
                ParseAndRun("autocmd!");
                Assert.Equal(0, _vimData.AutoCommands.Length);
            }

            [Fact]
            public void RemoveAllInEventKind()
            {
                Create();
                ParseAndRun("autocmd BufEnter,BufWinEnter *.html set ts=4");
                ParseAndRun("autocmd! BufEnter");
                Assert.Equal(1, _vimData.AutoCommands.Length);
                Assert.Equal(EventKind.BufWinEnter, _vimData.AutoCommands.Single().EventKind);
            }

            [Fact]
            public void RemoveAllWithPattern()
            {
                Create();
                ParseAndRun("autocmd BufEnter,BufWinEnter *.html set ts=4");
                ParseAndRun("autocmd! * *.html");
                Assert.Equal(0, _vimData.AutoCommands.Length);
            }

            [Fact]
            public void RemoveAllWithPattern2()
            {
                Create();
                ParseAndRun("autocmd BufEnter,BufWinEnter *.html set ts=4");
                ParseAndRun("autocmd BufEnter,BufWinEnter *.cs set ts=4");
                ParseAndRun("autocmd! * *.html");
                Assert.Equal(2, _vimData.AutoCommands.Length);
            }

            [Fact]
            public void RemoveAllWithEventAndPattern()
            {
                Create();
                ParseAndRun("autocmd BufEnter,BufWinEnter *.html set ts=4");
                ParseAndRun("autocmd BufEnter,BufWinEnter *.cs set ts=4");
                ParseAndRun("autocmd! BufEnter *.html");
                Assert.Equal(3, _vimData.AutoCommands.Length);
            }
        }

        public sealed class IfTest : InterpreterTest
        {
            private void ParseAndRun(params string[] lines)
            {
                var parser = VimUtil.CreateParser();
                parser.Reset(lines);
                var lineCommand = parser.ParseSingleCommand();
                _interpreter.RunLineCommand(lineCommand);
            }

            [Fact]
            public void If()
            {
                Create();
                ParseAndRun("if 1", "set ts=13", "endif");
                Assert.Equal(13, _localSettings.TabStop);
            }

            [Fact]
            public void IfElse()
            {
                Create();
                ParseAndRun("if 1", "set ts=13", "else", "set ts=12", "endif");
                Assert.Equal(13, _localSettings.TabStop);
            }

            [Fact]
            public void IfElse2()
            {
                Create();
                ParseAndRun("if 0", "set ts=13", "else", "set ts=12", "endif");
                Assert.Equal(12, _localSettings.TabStop);
            }

            [Fact]
            public void IfElseIf()
            {
                Create();
                ParseAndRun("if 1", "set ts=13", "elseif 1", "set ts=12", "endif");
                Assert.Equal(13, _localSettings.TabStop);
            }

            [Fact]
            public void IfElseIf2()
            {
                Create();
                ParseAndRun("if 0", "set ts=13", "elseif 1", "set ts=12", "endif");
                Assert.Equal(12, _localSettings.TabStop);
            }

            [Fact]
            public void IfElseIf3()
            {
                Create();
                _localSettings.TabStop = 42;
                ParseAndRun("if 0", "set ts=13", "elseif 0", "set ts=12", "endif");
                Assert.Equal(42, _localSettings.TabStop);
            }
        }

        public sealed class Misc : InterpreterTest
        {
            private LineRangeSpecifier ParseLineRange(string lineRangeText)
            {
                var parser = VimUtil.CreateParser();
                var result = parser.ParseRange(lineRangeText);
                Assert.True(!result.Item1.IsNone);
                Assert.Equal("", result.Item2);
                return result.Item1;
            }

            private SnapshotLineRange ParseAndGetLineRange(string lineRangeText)
            {
                var lineRange = ParseLineRange(lineRangeText);
                return _interpreter.GetLineRange(lineRange).Value;
            }

            [Fact]
            public void Behave_Mswin()
            {
                Create("");
                ParseAndRun("behave mswin");
                Assert.Equal(SelectModeOptions.Keyboard | SelectModeOptions.Mouse, _globalSettings.SelectModeOptions);
                Assert.Equal("popup", _globalSettings.MouseModel);
                Assert.Equal(KeyModelOptions.StartSelection | KeyModelOptions.StopSelection, _globalSettings.KeyModelOptions);
                Assert.Equal("exclusive", _globalSettings.Selection);
            }

            [Fact]
            public void Behave_Xterm()
            {
                Create("");
                ParseAndRun("behave xterm");
                Assert.Equal(SelectModeOptions.None, _globalSettings.SelectModeOptions);
                Assert.Equal("extend", _globalSettings.MouseModel);
                Assert.Equal(KeyModelOptions.None, _globalSettings.KeyModelOptions);
                Assert.Equal("inclusive", _globalSettings.Selection);
            }

            /// <summary>
            /// Don't execute a line that starts with a comment
            /// </summary>
            [Fact]
            public void CommentLine_Set()
            {
                Create("");
                _localSettings.AutoIndent = false;
                ParseAndRun(@"""set ai");
                Assert.False(_localSettings.AutoIndent);
            }

            /// <summary>
            /// Don't execute a line that starts with a comment
            /// </summary>
            [Fact]
            public void CommentLine_Delete()
            {
                Create("dog", "cat");
                ParseAndRun(@"""del");
                Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal("cat", _textBuffer.GetLine(1).GetText());
            }

            /// <summary>
            /// The delete of the last line in the ITextBuffer should reduce the line count
            /// </summary>
            [Fact]
            public void Delete_LastLine()
            {
                Create("cat", "dog");
                _textView.MoveCaretToLine(1);
                ParseAndRun("del");
                Assert.Equal(1, _textBuffer.CurrentSnapshot.LineCount);
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The delete of the first line in the ITextBuffer should reduce the line count
            /// </summary>
            [Fact]
            public void Delete_FirstLine()
            {
                Create("cat", "dog");
                ParseAndRun("del");
                Assert.Equal(1, _textBuffer.CurrentSnapshot.LineCount);
                Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Running :del on a single line should cause the line contents to be deleted
            /// but not crash
            /// </summary>
            [Fact]
            public void Delete_OneLine()
            {
                Create("cat");
                ParseAndRun("del");
                Assert.Equal(1, _textBuffer.CurrentSnapshot.LineCount);
                Assert.Equal("", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// The delete of the multiple lines including the last line should reduce by the 
            /// appropriate number of lines
            /// </summary>
            [Fact]
            public void Delete_MultipleLastLine()
            {
                Create("cat", "dog", "fish", "tree");
                _textView.MoveCaretToLine(1);
                ParseAndRun("2,$del");
                Assert.Equal(1, _textBuffer.CurrentSnapshot.LineCount);
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// No arguments means delete the current line
            /// </summary>
            [Fact]
            public void Delete_CurrentLine()
            {
                Create("foo", "bar");
                ParseAndRun("del");
                Assert.Equal("foo" + Environment.NewLine, UnnamedRegister.StringValue);
                Assert.Equal("bar", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// When count is in back it's a range of lines
            /// </summary>
            [Fact]
            public void Delete_SeveralLines()
            {
                Create("foo", "bar", "baz");
                ParseAndRun("dele 2");
                Assert.Equal("baz", _textView.GetLine(0).GetText());
                Assert.Equal("foo" + Environment.NewLine + "bar" + Environment.NewLine, UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Delete only the specified line when count is in front
            /// </summary>
            [Fact]
            public void Delete_SpecificLineNumber()
            {
                Create("foo", "bar", "baz");
                ParseAndRun("2del");
                Assert.Equal("foo", _textView.GetLine(0).GetText());
                Assert.Equal("baz", _textView.GetLine(1).GetText());
                Assert.Equal("bar" + Environment.NewLine, UnnamedRegister.StringValue);
            }

            /// <summary>
            /// Handle the case where the adjustment simply occurs on the current line 
            /// </summary>
            [Fact]
            public void GetLine_AdjustmentOnCurrent()
            {
                Create("cat", "dog", "bear");
                var range = _interpreter.GetLine(LineSpecifier.NewAdjustmentOnCurrent(1));
                Assert.Equal(_textBuffer.GetLine(1).LineNumber, range.Value.LineNumber);
            }

            [Fact]
            public void LineRange_FullFile()
            {
                Create("foo", "bar");
                var lineRange = ParseAndGetLineRange("%");
                var tss = _textBuffer.CurrentSnapshot;
                Assert.Equal(new SnapshotSpan(tss, 0, tss.Length), lineRange.ExtentIncludingLineBreak);
            }

            [Fact]
            public void LineRange_CurrentLine()
            {
                Create("foo", "bar");
                var lineRange = ParseAndGetLineRange(".");
                Assert.Equal(_textBuffer.GetLineRange(0), lineRange);
            }

            [Fact]
            public void LineRange_CurrentLineWithCurrentLine()
            {
                Create("foo", "bar");
                var lineRange = ParseAndGetLineRange(".,.");
                Assert.Equal(_textBuffer.GetLineRange(0), lineRange);
            }

            [Fact]
            public void LineRange_LineNumberRange()
            {
                Create("a", "b", "c");
                var lineRange = ParseAndGetLineRange("1,2");
                Assert.Equal(_textBuffer.GetLineRange(0, 1), lineRange);
            }

            [Fact]
            public void LineRange_SingleLine1()
            {
                Create("foo", "bar");
                var lineRange = ParseAndGetLineRange("1");
                Assert.Equal(0, lineRange.StartLineNumber);
                Assert.Equal(1, lineRange.Count);
            }

            [Fact]
            public void LineRange_MarkWithLineNumber()
            {
                Create("foo", "bar", "tree");
                _vimTextBuffer.SetLocalMark(LocalMark.OfChar('c').Value, 0, 1);
                var lineRange = ParseAndGetLineRange("'c,2");
                Assert.Equal(_textBuffer.GetLineRange(0, 1), lineRange);
            }

            [Fact]
            public void LineRange_MarkWithMark()
            {
                Create("foo", "bar");

                _vimTextBuffer.SetLocalMark(LocalMark.NewLetter(Letter.C), 0, 0);
                _vimTextBuffer.SetLocalMark(LocalMark.NewLetter(Letter.B), 1, 0);

                var lineRange = ParseAndGetLineRange("'c,'b");
                Assert.Equal(_textBuffer.GetLineRange(0, 1), lineRange);
            }

            /// <summary>
            /// Combine a global mark with a line number
            /// </summary>
            [Fact]
            public void LineRange_GlobalMarkAndLineNumber()
            {
                Create("foo bar", "bar", "baz");
                Vim.MarkMap.SetGlobalMark(Letter.A, _vimTextBuffer, 0, 2);
                var lineRange = ParseAndGetLineRange("'A,2");
                Assert.Equal(_textBuffer.GetLineRange(0, 1), lineRange);
            }

            /// <summary>
            /// Change the directory to a valid directory
            /// </summary>
            [Fact]
            public void ChangeDirectory_Valid()
            {
                Create("");
                ParseAndRun("cd " + ValidDirectoryPath);
                Assert.Equal(ValidDirectoryPath, _vimData.CurrentDirectory);
            }

            /// <summary>
            /// Change the global directory should invalidate the local directory
            /// </summary>
            [Fact]
            public void ChangeDirectory_InvalidateLocal()
            {
                Create("");
                _vimBuffer.CurrentDirectory = FSharpOption.Create(@"c:\");
                ParseAndRun("cd " + ValidDirectoryPath);
                Assert.Equal(ValidDirectoryPath, _vimData.CurrentDirectory);
                Assert.True(_vimBuffer.CurrentDirectory.IsNone());
            }

            /// <summary>
            /// Change the local directory to a valid directory
            /// </summary>
            [Fact]
            public void ChangeLocalDirectory_Valid()
            {
                Create("");
                _vimData.CurrentDirectory = @"c:\";
                ParseAndRun("lcd " + ValidDirectoryPath);
                Assert.Equal(@"c:\", _vimData.CurrentDirectory);
                Assert.Equal(ValidDirectoryPath, _vimBuffer.CurrentDirectory.Value);
            }

            /// <summary>
            /// Test the use of the "del" command with global
            /// </summary>
            [Fact]
            public void Global_Delete()
            {
                Create("cat", "dog", "fish");
                ParseAndRun("g/a/del");
                Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal("fish", _textBuffer.GetLine(1).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Test the use of the "del" command with global for a negative match
            /// </summary>
            [Fact]
            public void Global_Delete_NotMatch()
            {
                Create("cat", "dog", "fish");
                ParseAndRun("g!/a/del");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal(1, _textBuffer.CurrentSnapshot.LineCount);
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Test the use of the "del" command with global and alternate separators
            /// </summary>
            [Fact]
            public void Global_Delete_AlternateSeparators()
            {
                Create("cat", "dog", "fish");
                ParseAndRun("g,a,del");
                Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal("fish", _textBuffer.GetLine(1).GetText());
                Assert.Equal(0, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Test out the :global command with put
            /// </summary>
            [Fact]
            public void Global_Put()
            {
                Create("cat", "dog", "fash");
                UnnamedRegister.UpdateValue("bat");
                ParseAndRun("g,a,put");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("bat", _textBuffer.GetLine(1).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(2).GetText());
                Assert.Equal("fash", _textBuffer.GetLine(3).GetText());
                Assert.Equal("bat", _textBuffer.GetLine(4).GetText());
                Assert.Equal(_textView.GetPointInLine(4, 0), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Test out the :global command delete until double quote character
            /// </summary>
            [Fact]
            public void Global_Normal_DeleteUntilDoubleQuote()
            {
                Create("cat\"dog");
                ParseAndRun("norm df\"");
                Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Test out the :global command with normal command deleting after search pattern
            /// </summary>
            [Fact]
            public void Global_Normal_Search_Delete()
            {
                Create("cat,dog");
                ParseAndRun("g/,/norm nD");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Test out the :normal command with delete line (dd) and put (p) key strokes
            /// </summary>
            [Fact]
            public void Normal_DeletePut()
            {
                Create("cat", "dog", "fish");
                ParseAndRun("norm ddp");
                Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal("cat", _textBuffer.GetLine(1).GetText());
                Assert.Equal("fish", _textBuffer.GetLine(2).GetText());

            }

            /// <summary>
            /// Test out the :normal command with remove (x) with extra leading spaces
            /// </summary>
            [Fact]
            public void Normal_RemoveWithSpaces()
            {
                Create("ccat");
                ParseAndRun("norm  x");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Test out the :normal command insert text in the middle of a line
            /// </summary>
            [Fact]
            public void Normal_InsertInMiddleOfLine()
            {
                Create("cat dog");
                ParseAndRun("norm w");
                ParseAndRun("norm iand ");
                Assert.Equal("cat and dog", _textBuffer.GetLine(0).GetText());
            }
            /// <summary>
            /// Test out the :normal command insert text in the middle of a line
            /// </summary>
            [Fact]
            public void Normal_InsertWithLineRange()
            {
                Create("cat", "dog", "fish", "whale");
                ParseAndRun("2,3norm i.");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal(".dog", _textBuffer.GetLine(1).GetText());
                Assert.Equal(".fish", _textBuffer.GetLine(2).GetText());
                Assert.Equal("whale", _textBuffer.GetLine(3).GetText());
            }

            /// <summary>
            /// Can't get the range for a mark that doesn't exist
            /// </summary>
            [Fact]
            public void LineRange_BadMark()
            {
                Create("foo bar", "baz");
                var lineRange = _interpreter.GetLineRange(ParseLineRange("'c,2"));
                Assert.True(lineRange.IsNone());
            }

            [Fact]
            public void LineRange_LineNumberWithPlus()
            {
                Create("foo", "bar", "baz", "jaz");
                var lineRange = ParseAndGetLineRange("1+2");
                Assert.Equal(2, lineRange.StartLineNumber);
                Assert.Equal(1, lineRange.Count);
            }

            /// <summary>
            /// Make sure that we treat a plus with no trailing value as a + 1
            /// </summary>
            [Fact]
            public void LineRange_LineNumberWithPlusAndNoValue()
            {
                Create("foo", "bar", "baz");
                var lineRange = ParseAndGetLineRange("1+");
                Assert.Equal(1, lineRange.StartLineNumber);
                Assert.Equal(1, lineRange.Count);
            }

            /// <summary>
            /// Test the + with a range
            /// </summary>
            [Fact]
            public void LineRange_LineNumberWithPlusInRange()
            {
                Create("foo", "bar", "baz", "jaz", "aoeu", "za,.p");
                var lineRange = ParseAndGetLineRange("1+1,3");
                Assert.Equal(1, lineRange.StartLineNumber);
                Assert.Equal(2, lineRange.LastLineNumber);
            }

            [Fact]
            public void LineRange_LineNumberWithMinus()
            {
                Create("foo", "bar", "baz", "jaz", "aoeu", "za,.p");
                var lineRange = ParseAndGetLineRange("1-1");
                Assert.Equal(_textBuffer.GetLineRange(0), lineRange);
            }

            [Fact]
            public void LineRange_LineNumberWithMinusAndNoValue()
            {
                Create("foo", "bar", "baz", "jaz", "aoeu", "za,.p");
                var lineRange = ParseAndGetLineRange("2-");
                Assert.Equal(_textBuffer.GetLineRange(0), lineRange);
            }

            [Fact]
            public void LineRange_LineNumberWithMinus2()
            {
                Create("foo", "bar", "baz", "jaz", "aoeu", "za,.p");
                var lineRange = ParseAndGetLineRange("5-3");
                Assert.Equal(_textBuffer.GetLineRange(1), lineRange);
            }

            [Fact]
            public void LineRange_LineNumberWithMinusInRange()
            {
                Create("foo", "bar", "baz", "jaz", "aoeu", "za,.p");
                var lineRange = ParseAndGetLineRange("1,5-2");
                Assert.Equal(_textBuffer.GetLineRange(0, 2), lineRange);
            }

            [Fact]
            public void LineRange_LastLine()
            {
                Create("cat", "tree", "dog");
                var lineRange = ParseAndGetLineRange("$");
                Assert.Equal(_textBuffer.GetLineRange(2), lineRange);
            }

            [Fact]
            public void LineRange_LastLine_OneLineBuffer()
            {
                Create("cat");
                var lineRange = ParseAndGetLineRange("$");
                Assert.Equal(_textBuffer.GetLineRange(0), lineRange);
            }

            [Fact]
            public void LineRange_CurrentToEnd()
            {
                Create("cat", "tree", "dog");
                var lineRange = ParseAndGetLineRange(".,$");
                Assert.Equal(_textBuffer.GetLineRange(0, 2), lineRange);
            }

            [Fact]
            public void LineRange_RightSideIncrementsLeft()
            {
                Create("cat", "dog", "bear", "frog", "tree");
                var lineRange = ParseAndGetLineRange(".,+2");
                Assert.Equal(_textBuffer.GetLineRange(0, 2), lineRange);
            }

            [Fact]
            public void LineRange_LeftSideIncrementsCurrent()
            {
                Create("cat", "dog", "bear", "frog", "tree");
                var lineRange = ParseAndGetLineRange(".,+2");
                Assert.Equal(_textBuffer.GetLineRange(0, 2), lineRange);
            }

            /// <summary>
            /// Make sure we can clear out key mappings with the "mapc" command
            /// </summary>
            [Fact]
            public void MapKeys_Clear()
            {
                Create("");
                Action<string, KeyRemapMode[]> testMapClear =
                    (command, toClearModes) =>
                    {
                        foreach (var keyRemapMode in KeyRemapMode.All)
                        {
                            Assert.True(_keyMap.MapWithNoRemap("a", "b", keyRemapMode));
                        }
                    };

                _keyMap.ClearAll();

                testMapClear("mapc", new[] { KeyRemapMode.Normal, KeyRemapMode.Visual, KeyRemapMode.Command, KeyRemapMode.OperatorPending });
                testMapClear("nmapc", new[] { KeyRemapMode.Normal });
                testMapClear("vmapc", new[] { KeyRemapMode.Visual, KeyRemapMode.Select });
                testMapClear("xmapc", new[] { KeyRemapMode.Visual });
                testMapClear("smapc", new[] { KeyRemapMode.Select });
                testMapClear("omapc", new[] { KeyRemapMode.OperatorPending });
                testMapClear("mapc!", new[] { KeyRemapMode.Insert, KeyRemapMode.Command });
                testMapClear("imapc", new[] { KeyRemapMode.Insert });
                testMapClear("cmapc", new[] { KeyRemapMode.Command });
            }

            [Fact]
            public void Move_BackOneLine()
            {
                Create("fish", "cat", "dog", "tree");
                _textView.MoveCaretToLine(2);
                ParseAndRun("move -2");
                Assert.Equal("fish", _textView.GetLine(0).GetText());
                Assert.Equal("dog", _textView.GetLine(1).GetText());
                Assert.Equal("cat", _textView.GetLine(2).GetText());
                Assert.Equal("tree", _textView.GetLine(3).GetText());
            }

            [Fact]
            public void PrintCurrentDirectory_Global()
            {
                Create();
                ParseAndRun("pwd");
                Assert.Equal(_vimData.CurrentDirectory, _statusUtil.LastStatus);
            }

            /// <summary>
            /// The print current directory command should prefer the window directory
            /// over the global one
            /// </summary>
            [Fact]
            public void PrintCurrentDirectory_Local()
            {
                Create();
                _vimBuffer.CurrentDirectory = FSharpOption.Create(@"c:\");
                ParseAndRun("pwd");
                Assert.Equal(@"c:\", _statusUtil.LastStatus);
            }

            /// <summary>
            /// Ensure the Put command is linewise even if the register is marked as characterwise
            /// </summary>
            [Fact]
            public void Put_ShouldBeLinewise()
            {
                Create("foo", "bar");
                UnnamedRegister.UpdateValue("hey", OperationKind.CharacterWise);
                ParseAndRun("put");
                Assert.Equal("foo", _textBuffer.GetLine(0).GetText());
                Assert.Equal("hey", _textBuffer.GetLine(1).GetText());
            }

            /// <summary>
            /// Ensure that when the ! is present that the appropriate option is passed along
            /// </summary>
            [Fact]
            public void Put_BangShouldPutTextBefore()
            {
                Create("foo", "bar");
                UnnamedRegister.UpdateValue("hey", OperationKind.CharacterWise);
                ParseAndRun("put!");
                Assert.Equal("hey", _textBuffer.GetLine(0).GetText());
                Assert.Equal("foo", _textBuffer.GetLine(1).GetText());
            }

            /// <summary>
            /// By default the retab command should affect the entire ITextBuffer and not include
            /// space strings
            /// </summary>
            [Fact]
            public void Retab_Default()
            {
                Create("   cat", "\tdog");
                _localSettings.ExpandTab = true;
                _localSettings.TabStop = 2;
                ParseAndRun("retab");
                Assert.Equal("   cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("  dog", _textBuffer.GetLine(1).GetText());
            }

            /// <summary>
            /// The ! operator should force the command to include spaces
            /// </summary>
            [Fact]
            public void Retab_WithBang()
            {
                Create("  cat", "  dog");
                _localSettings.ExpandTab = false;
                _localSettings.TabStop = 2;
                ParseAndRun("retab!");
                Assert.Equal("\tcat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("\tdog", _textBuffer.GetLine(1).GetText());
            }

            /// <summary>
            /// Make sure the basic command is passed down to the func
            /// </summary>
            [Fact]
            public void ShellCommand_Simple()
            {
                Create("");
                var didRun = false;
                VimHost.RunCommandFunc =
                    (command, args, _) =>
                    {
                        Assert.Equal("/c git status", args);
                        didRun = true;
                        return "";
                    };
                ParseAndRun(@"!git status");
                Assert.True(didRun);
            }

            /// <summary>
            /// Do a simple replacement of a ! in the shell command
            /// </summary>
            [Fact]
            public void ShellCommand_BangReplacement()
            {
                Create("");
                Vim.VimData.LastShellCommand = FSharpOption.Create("cat");
                var didRun = false;
                VimHost.RunCommandFunc =
                    (command, args, _) =>
                    {
                        Assert.Equal("/c git status cat", args);
                        didRun = true;
                        return "";
                    };
                ParseAndRun(@"!git status !");
                Assert.True(didRun);
            }

            /// <summary>
            /// Don't replace a ! which occurs after a \
            /// </summary>
            [Fact]
            public void ShellCommand_BangNoReplace()
            {
                Create("");
                var didRun = false;
                VimHost.RunCommandFunc =
                    (command, args, _) =>
                    {
                        Assert.Equal("/c git status !", args);
                        didRun = true;
                        return "";
                    };
                ParseAndRun(@"!git status \!");
                Assert.True(didRun);
            }

            /// <summary>
            /// Raise an error message if there is no previous command and a bang relpacement
            /// isr requested.  Shouldn't run any command in this case
            /// </summary>
            [Fact]
            public void ShellCommand_BangReplacementFails()
            {
                Create("");
                var didRun = false;
                VimHost.RunCommandFunc = delegate { didRun = true; return ""; };
                ParseAndRun(@"!git status !");
                Assert.False(didRun);
                Assert.Equal(Resources.Common_NoPreviousShellCommand, _statusUtil.LastError);
            }

            /// <summary>
            /// Simple visual studio command
            /// </summary>
            [Fact]
            public void HostCommand_Simple()
            {
                Create("");
                var didRun = false;
                VimHost.RunHostCommandFunc =
                    (textView, command, argument) =>
                    {
                        Assert.Equal("Build.BuildSelection", command);
                        Assert.Equal("", argument);
                        didRun = true;
                    };
                ParseAndRun("vsc Build.BuildSelection");
                Assert.True(didRun);
            }

            /// <summary>
            /// Simple visual studio command with an argument
            /// </summary>
            [Fact]
            public void HostCommand_WithArgument()
            {
                Create("");
                var didRun = false;
                VimHost.RunHostCommandFunc =
                    (textView, command, argument) =>
                    {
                        Assert.Equal("Build.BuildSelection", command);
                        Assert.Equal("Arg", argument);
                        didRun = true;
                    };
                ParseAndRun("vsc Build.BuildSelection Arg");
                Assert.True(didRun);
            }

            /// <summary>
            /// This came up as a part of Issue 1038.  Numbers should be considered as part of the host 
            /// command name
            /// </summary>
            [Fact]
            public void HostCommand_WithNumber()
            {
                Create("");
                var didRun = false;
                VimHost.RunHostCommandFunc =
                    (textView, command, argument) =>
                    {
                        Assert.Equal("Command1", command);
                        Assert.Equal("Arg2", argument);
                        didRun = true;
                    };
                ParseAndRun("vsc Command1 Arg2");
                Assert.True(didRun);
            }

            public void Issue1328()
            {
                Create("");
                ParseAndRun(":set backspace=2");
                Assert.True(_globalSettings.IsBackspaceEol && _globalSettings.IsBackspaceStart && _globalSettings.IsBackspaceIndent);
            }
        }

        public sealed class RunTabOnlyTest : InterpreterTest
        {
            [Fact]
            public void TabonlyClosesOtherWindows()
            {
                Create();
                ParseAndRun("tabonly");
                Assert.True(VimHost.ClosedOtherTabs);
            }
        }

        public sealed class RunOnlyTest : InterpreterTest
        {
            [Fact]
            public void TabonlyClosesOtherWindows()
            {
                Create();
                ParseAndRun("only");
                Assert.True(VimHost.ClosedOtherWindows);
            }
        }

        public sealed class GoToTabTest : InterpreterTest
        {
            [Fact]
            public void TabFirstGoesToFirstTab()
            {
                Create();
                ParseAndRun("tabfirst");
                Assert.Equal(0, VimHost.GoToTabData);
            }

            [Fact]
            public void TabLastGoesToLastTab()
            {
                Create();
                VimHost.TabCount = 3;
                ParseAndRun("tablast");
                Assert.Equal(2, VimHost.GoToTabData);
            }
        }
    }
}
