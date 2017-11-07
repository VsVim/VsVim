﻿using System;
using System.Linq;
using Vim.EditorHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Extensions;
using Vim.UnitTest.Mock;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class CommandModeIntegrationTest : VimTestBase
    {
        private IVimBuffer _vimBuffer;
        private ITextView _textView;
        private ITextBuffer _textBuffer;
        private ICommandMode _commandMode;
        private MockVimHost _vimHost;
        private string _lastStatus;

        public virtual void Create(params string[] lines)
        {
            _vimBuffer = CreateVimBuffer(lines);
            _vimBuffer.StatusMessage += (sender, args) => { _lastStatus = args.Message; };
            _textView = _vimBuffer.TextView;
            _textBuffer = _textView.TextBuffer;
            _vimHost = VimHost;
            _commandMode = _vimBuffer.CommandMode;
        }

        /// <summary>
        /// Run the command.  A colon will be inserted before the command
        /// </summary>
        protected void RunCommand(string command)
        {
            _vimBuffer.Process(':');
            _vimBuffer.Process(command, enter: true);
        }

        /// <summary>
        /// Run a command without prefixing it with a colon (:)
        /// </summary>
        protected void RunCommandRaw(string command)
        {
            _vimBuffer.Process(command, enter: true);
        }

        public sealed class CommandChangedEventTest : CommandModeIntegrationTest
        {
            private int _commandChangedCount;

            public CommandChangedEventTest()
            {
                Create();
                _vimBuffer.SwitchMode(ModeKind.Command, ModeArgument.None);
                _vimBuffer.CommandMode.CommandChanged += (sender, e) => { _commandChangedCount++; };
                _vimBuffer.LocalSettings.AutoIndent = false;
            }

            [WpfFact]
            public void SimpleSet()
            {
                _commandMode.Command = "set ai";
                Assert.Equal(1, _commandChangedCount);
            }

            [WpfFact]
            public void EnterShouldChange()
            {
                _vimBuffer.Process("set ai");
                _commandChangedCount = 0;
                _vimBuffer.Process(VimKey.Enter);
                Assert.Equal(1, _commandChangedCount);
            }

            [WpfFact]
            public void EscapeShouldChange()
            {
                _vimBuffer.Process("set ai");
                _commandChangedCount = 0;
                _vimBuffer.Process(VimKey.Escape);
                Assert.Equal(1, _commandChangedCount);
            }
        }

        public sealed class CommandPropertyTest : CommandModeIntegrationTest
        {
            public CommandPropertyTest()
            {
                Create();
                _vimBuffer.SwitchMode(ModeKind.Command, ModeArgument.None);
                _vimBuffer.LocalSettings.AutoIndent = false;
            }

            [WpfFact]
            public void TypeAndCheck()
            {
                _vimBuffer.Process("set");
                Assert.Equal("set", _commandMode.Command);
            }

            [WpfFact]
            public void SimpleSet()
            {
                _commandMode.Command = "set ai";
                Assert.Equal("set ai", _commandMode.Command);
                _vimBuffer.Process(VimKey.Enter);
                Assert.True(_vimBuffer.LocalSettings.AutoIndent);
            }

            [WpfFact]
            public void SetAndUse()
            {
                _commandMode.Command = "set ai";
                _vimBuffer.Process(VimKey.Enter);
                Assert.True(_vimBuffer.LocalSettings.AutoIndent);
            }
        }

        public sealed class CopyToTest : CommandModeIntegrationTest
        {
            /// <summary>
            /// Copying a line to a given line should put it at that given line
            /// </summary>
            [WpfFact]
            public void ItDisplacesToTheLineBelowWhenTargetedAtCurrentLine()
            {
                Create("cat", "dog", "bear");
                RunCommand("co 1");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("cat", _textBuffer.GetLine(1).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(2).GetText());
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            [WpfFact]
            public void ItCanJumpLongRanges()
            {
                Create("cat", "dog", "bear");
                RunCommand("co 2");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
                Assert.Equal("cat", _textBuffer.GetLine(2).GetText());
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Check the copy command via the 't' synonym
            /// </summary>
            [WpfFact]
            public void The_t_SynonymWorksAlso()
            {
                Create("cat", "dog", "bear");
                RunCommand("t 2");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
                Assert.Equal("cat", _textBuffer.GetLine(2).GetText());
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Copying a line to a range should cause it to copy to the first line 
            /// in the range
            /// </summary>
            [WpfFact]
            public void CopyingASingleLineToARangeDuplicatesTheLine()
            {
                Create("cat", "dog", "bear");
                RunCommand("co 1,2");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("cat", _textBuffer.GetLine(1).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(2).GetText());
            }

            [WpfFact]
            public void PositiveRelativeReferencesUsingDotWork()
            {
                Create("cat", "dog", "bear");
                _textView.MoveCaretToLine(1);
                RunCommand("co .");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(2).GetText());
                Assert.Equal("bear", _textBuffer.GetLine(3).GetText());
            }

            [WpfFact]
            public void PositiveRelativeReferencesWork()
            {
                Create("cat", "dog", "bear");
                RunCommand("co +1");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
                Assert.Equal("cat", _textBuffer.GetLine(2).GetText());
                Assert.Equal("bear", _textBuffer.GetLine(3).GetText());
            }

            [WpfFact]
            public void NegativeRelativeReferencesWork()
            {
                // Added goose to simplify this test case. Look further for an issue with last line endlines 
                Create("cat", "dog", "bear", "goose");
                _textView.MoveCaretToLine(2);
                RunCommand("co -2");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("bear", _textBuffer.GetLine(1).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(2).GetText());
                Assert.Equal("bear", _textBuffer.GetLine(3).GetText());
            }

            [WpfFact]
            public void CopyingPastLastLineInsertsAnImplicitNewline()
            {
                Create("cat", "dog", "bear");
                RunCommand("co 3");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
                Assert.Equal("bear", _textBuffer.GetLine(2).GetText());
                Assert.Equal("cat", _textBuffer.GetLine(3).GetText());
            }
        }

        public sealed class DeleteMarksTest : CommandModeIntegrationTest
        {
            private bool HasGlobalMark(Letter letter)
            {
                return Vim.MarkMap.GetGlobalMark(letter).IsSome();
            }

            private bool HasLocalMark(LocalMark localMark)
            {
                return _vimBuffer.VimTextBuffer.GetLocalMark(localMark).IsSome();
            }

            private bool HasLocalMark(Letter letter)
            {
                return HasLocalMark(LocalMark.NewLetter(letter));
            }

            [WpfFact]
            public void DeleteGlobal()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("mA");
                Assert.True(HasGlobalMark(Letter.A));
                _vimBuffer.ProcessNotation(":delmarks A", enter: true);
                Assert.False(HasGlobalMark(Letter.A));
            }

            [WpfFact]
            public void DeleteGlobalMany()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("mA");
                _vimBuffer.ProcessNotation("mB");
                Assert.True(HasGlobalMark(Letter.A));
                _vimBuffer.ProcessNotation(":delmarks A B", enter: true);
                Assert.False(HasGlobalMark(Letter.A));
                Assert.False(HasGlobalMark(Letter.B));
            }

            [WpfFact]
            public void DeleteGlobalRange()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("mA");
                _vimBuffer.ProcessNotation("mB");
                Assert.True(HasGlobalMark(Letter.A));
                _vimBuffer.ProcessNotation(":delmarks A-B", enter: true);
                Assert.False(HasGlobalMark(Letter.A));
                Assert.False(HasGlobalMark(Letter.B));
            }

            /// <summary>
            /// Normal delete range operation but include some invalid marks here.  No errors
            /// should be issued
            /// </summary>
            [WpfFact]
            public void DeleteGlobalRangeWithInvalid()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("mA");
                _vimBuffer.ProcessNotation("mB");
                Assert.True(HasGlobalMark(Letter.A));
                _vimBuffer.ProcessNotation(":delmarks A-C", enter: true);
                Assert.False(HasGlobalMark(Letter.A));
                Assert.False(HasGlobalMark(Letter.B));
                Assert.False(HasGlobalMark(Letter.C));
            }

            [WpfFact]
            public void DeleteLocalMark()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("ma");
                _vimBuffer.ProcessNotation("mb");
                Assert.True(HasLocalMark(Letter.A));
                Assert.True(HasLocalMark(Letter.B));
                _vimBuffer.ProcessNotation(":delmarks a", enter: true);
                Assert.False(HasLocalMark(Letter.A));
                Assert.True(HasLocalMark(Letter.B));
            }

            [WpfFact]
            public void DeleteLocalMarkMany()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("ma");
                _vimBuffer.ProcessNotation("mb");
                Assert.True(HasLocalMark(Letter.A));
                Assert.True(HasLocalMark(Letter.B));
                _vimBuffer.ProcessNotation(":delmarks a b", enter: true);
                Assert.False(HasLocalMark(Letter.A));
                Assert.False(HasLocalMark(Letter.B));
            }

            [WpfFact]
            public void DeleteLocalMarkRange()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("ma");
                _vimBuffer.ProcessNotation("mb");
                Assert.True(HasLocalMark(Letter.A));
                Assert.True(HasLocalMark(Letter.B));
                _vimBuffer.ProcessNotation(":delmarks a-b", enter: true);
                Assert.False(HasLocalMark(Letter.A));
                Assert.False(HasLocalMark(Letter.B));
            }

            [WpfFact]
            public void DeleteLocalMarkNumber()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("ma");
                _vimBuffer.ProcessNotation("mb");
                Assert.True(HasLocalMark(Letter.A));
                Assert.True(HasLocalMark(Letter.B));
                _vimBuffer.ProcessNotation(":delmarks a-b", enter: true);
                Assert.False(HasLocalMark(Letter.A));
                Assert.False(HasLocalMark(Letter.B));
            }

            [WpfFact]
            public void DeleteAllMarks()
            {
                Create("cat", "dog");
                _vimBuffer.ProcessNotation("ma");
                _vimBuffer.ProcessNotation("mA");
                Assert.True(HasLocalMark(Letter.A));
                Assert.True(HasGlobalMark(Letter.A));
                _vimBuffer.ProcessNotation(":delmarks!", enter: true);
                Assert.False(HasLocalMark(Letter.A));
                Assert.True(HasGlobalMark(Letter.A));
            }
        }

        public sealed class GlobalTest : CommandModeIntegrationTest
        {
            [WpfFact]
            public void DeleteSelected()
            {
                Create("cat", "dog", "cattle");
                _vimBuffer.ProcessNotation(":g/cat/d", enter: true);
                Assert.Equal(new[] { "dog" }, _vimBuffer.TextBuffer.GetLines());
            }

            [WpfFact]
            public void UpdateLastSearch()
            {
                Create("cat", "dog", "cattle");
                _vimBuffer.VimData.LastSearchData = new SearchData("cat", SearchPath.Forward);
                _vimBuffer.ProcessNotation(":g/cat/echo", enter: true);
                Assert.Equal("cat", _vimBuffer.VimData.LastSearchData.Pattern);
            }

            [WpfFact]
            public void SpaceDoesntUseLastSearch()
            {
                Create("cat", "dog", "cattle", "big dog");
                _vimBuffer.VimData.LastSearchData = new SearchData("cat", SearchPath.Forward);
                _vimBuffer.ProcessNotation(":g/ /d", enter: true);
                Assert.Equal(new[] { "cat", "dog", "cattle" }, _vimBuffer.TextBuffer.GetLines());
            }

            /// <summary>
            /// By default the global command should use the last search pattern
            /// </summary>
            [WpfFact]
            public void Issue1626()
            {
                Create("cat", "dog", "cattle");
                _vimBuffer.VimData.LastSearchData = new SearchData("cat", SearchPath.Forward);
                _vimBuffer.ProcessNotation(":g//d", enter: true);
                Assert.Equal(new[] { "dog" }, _vimBuffer.TextBuffer.GetLines());
            }
        }

        public sealed class IncrementalSearchTest : CommandModeIntegrationTest
        {
            /// <summary>
            /// Make sure that we can handle the incremental search command from the command line 
            /// Issue 1034
            /// </summary>
            [WpfFact]
            public void ForwardSimple()
            {
                Create("cat", "dog", "fish");
                RunCommandRaw(":/dog");
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure that we can handle the incremental search command from the command line 
            /// </summary>
            [WpfFact]
            public void BackwardSimple()
            {
                Create("cat", "dog", "fish");
                _textView.MoveCaretToLine(2);
                RunCommandRaw(":?dog");
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure the match goes to the first non-whitespace character on the line 
            /// </summary>
            [WpfFact]
            public void MatchNotOnColumnZero()
            {
                Create("cat", " dog", "fish");
                RunCommandRaw(":/dog");
                Assert.Equal(_textBuffer.GetPointInLine(1, 1), _textView.GetCaretPoint());
            }

            /// <summary>
            /// The caret should not move to the word but instead to the first non-blank character
            /// of the line
            /// </summary>
            [WpfFact]
            public void MatchNotStartOfLine()
            {
                Create("cat", " big dog", "fish");
                RunCommandRaw(":/dog");
                Assert.Equal(_textBuffer.GetPointInLine(1, 1), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Executing an incremental search from the command line needs to update the last searched
            /// for term
            /// </summary>
            [WpfFact]
            public void Issue1146()
            {
                Create("cat", " dog", "dog");
                RunCommandRaw(":/dog");
                _vimBuffer.Process("n");
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
            }
        }

        public sealed class LastCommandLineTest : CommandModeIntegrationTest
        {
            [WpfFact]
            public void Simple()
            {
                Create();
                RunCommandRaw(":/dog");
                Assert.Equal("/dog", VimData.LastCommandLine);
            }

            [WpfFact]
            public void Error()
            {
                Create();
                VimData.LastCommandLine = "test";
                RunCommandRaw(":not_a_vim_command");
                Assert.Equal("not_a_vim_command", VimData.LastCommandLine);
            }
        }

        public sealed class LineEdittingTest : CommandModeIntegrationTest
        {
            private readonly HistoryList _commandHistoryList;

            public LineEdittingTest()
            {
                _commandHistoryList = VimData.CommandHistory;
            }

            /// <summary>
            /// An empty command shouldn't be store in the command history 
            /// </summary>
            [WpfFact]
            public void EmptyCommandsNotStored()
            {
                Create("");
                RunCommand("");
                Assert.Equal(0, VimData.CommandHistory.Count());
            }

            [WpfFact]
            public void PreviousCommand()
            {
                Create("");
                _commandHistoryList.Add("dog");
                _vimBuffer.ProcessNotation(":<Up>");
                Assert.Equal("dog", _commandMode.Command);
            }

            [WpfFact]
            public void PreviousCommandAlternateKeystroke()
            {
                Create("");
                _commandHistoryList.Add("dog");
                _vimBuffer.ProcessNotation(":<C-p>");
                Assert.Equal("dog", _commandMode.Command);
            }

            [WpfFact]
            public void NextCommand()
            {
                Create("");
                _commandHistoryList.AddRange("dog", "cat");
                _vimBuffer.ProcessNotation(":<Up><Up><Down>");
                Assert.Equal("cat", _commandMode.Command);
            }

            [WpfFact]
            public void NextCommandAlternateKeystroke()
            {
                Create("");
                _commandHistoryList.AddRange("dog", "cat");
                _vimBuffer.ProcessNotation(":<C-p><C-p><C-n>");
                Assert.Equal("cat", _commandMode.Command);
            }

            [WpfFact]
            public void Backspace()
            {
                Create("");
                _vimBuffer.ProcessNotation(":dogd<BS>");
                Assert.Equal("dog", _commandMode.Command);
            }

            [WpfFact]
            public void BackspaceWithShift()
            {
                Create("");
                _vimBuffer.ProcessNotation(":dogd<S-BS>");
                Assert.Equal("dog", _commandMode.Command);
            }
        }

        public sealed class MoveToTests : CommandModeIntegrationTest
        {
            [WpfFact]
            public void SimpleCaseOfMovingLineOneBelow()
            {
                Create("cat", "dog", "bear");

                RunCommand("m 2");
                Assert.Equal(_textBuffer.GetLine(0).GetText(), "dog");
                Assert.Equal(_textBuffer.GetLine(1).GetText(), "cat");
                Assert.Equal(_textBuffer.GetLine(2).GetText(), "bear");
            }

            /// <summary>
            /// The last line in the file seems to be an exception because it doesn't have a 
            /// newline at the end
            /// </summary>
            [WpfFact]
            public void MoveToLastLineInFile()
            {
                Create("cat", "dog", "bear");

                RunCommand("m 3");
                Assert.Equal(_textBuffer.GetLine(0).GetText(), "dog");
                Assert.Equal(_textBuffer.GetLine(1).GetText(), "bear");
                Assert.Equal(_textBuffer.GetLine(2).GetText(), "cat");
            }


            /// <summary>
            /// Specifying "line 0" should move to before the first line.
            /// </summary>
            [WpfFact]
            public void MoveToBeforeFirstLineInFile() {
                Create("cat", "dog", "bear");

                _textView.MoveCaretToLine(2);
                RunCommand("m0");

                Assert.Equal(_textBuffer.GetLine(0).GetText(), "bear");
                Assert.Equal(_textBuffer.GetLine(1).GetText(), "cat");
                Assert.Equal(_textBuffer.GetLine(2).GetText(), "dog");
            }
        }

        public sealed class PasteTest : CommandModeIntegrationTest
        {
            [WpfFact]
            public void Simple()
            {
                Create("");
                Vim.RegisterMap.GetRegister('c').UpdateValue("test");
                _vimBuffer.ProcessNotation(":<C-r>c");
                Assert.Equal("test", _commandMode.Command);
            }

            [WpfFact]
            public void InPasteWait()
            {
                Create("");
                Vim.RegisterMap.GetRegister('c').UpdateValue("test");
                _vimBuffer.ProcessNotation(":h<C-r>");
                Assert.True(_commandMode.InPasteWait);
                _vimBuffer.ProcessNotation("c");
                Assert.Equal("htest", _commandMode.Command);
                Assert.False(_commandMode.InPasteWait);
            }

            [WpfFact]
            public void InsertWordUnderCursor()
            {
                // :help c_CTRL-R_CTRL-W
                Create("dog-bark", "cat-meow", "bear-growl");
                _textView.MoveCaretToLine(1);
                var initialCaret = _textView.Caret;
                var initialSelection = _textView.Selection;
                _vimBuffer.ProcessNotation(":<C-r><C-w>");
                Assert.Equal("cat", _commandMode.Command);
                Assert.False(_commandMode.InPasteWait);
                Assert.Equal(initialCaret, _textView.Caret);
                Assert.Equal(initialSelection, _textView.Selection);
            }

            [WpfFact]
            public void InsertAllWordUnderCursor()
            {
                // :help c_CTRL-R_CTRL-A
                Create("dog-bark", "cat-meow", "bear-growl");
                _textView.MoveCaretToLine(1);
                var initialCaret = _textView.Caret;
                var initialSelection = _textView.Selection;
                _vimBuffer.ProcessNotation(":<C-r><C-a>");
                Assert.Equal("cat-meow", _commandMode.Command);
                Assert.False(_commandMode.InPasteWait);
                Assert.Equal(initialCaret, _textView.Caret);
                Assert.Equal(initialSelection, _textView.Selection);
            }
        }

        public abstract class SubstituteTest : CommandModeIntegrationTest
        {
            public sealed class GlobalDefaultTest : SubstituteTest
            {
                public override void Create(params string[] lines)
                {
                    base.Create(lines);
                    _vimBuffer.Vim.GlobalSettings.GlobalDefault = true;
                }

                [WpfFact]
                public void Simple()
                {
                    Create("cat bat");
                    RunCommand("s/a/o");
                    Assert.Equal("cot bot", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void Invert()
                {
                    Create("cat bat");
                    RunCommand("s/a/o/g");
                    Assert.Equal("cot bat", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void Repeat()
                {
                    Create("cat bat", "cat bat");
                    RunCommand("s/a/o");
                    _textView.MoveCaretToLine(1);
                    RunCommand("s");
                    Assert.Equal(new[] { "cot bot", "cot bot" }, _textBuffer.GetLines());
                }
            }

            public sealed class SubstituteMiscTest : SubstituteTest
            {
                /// <summary>
                /// Suppress errors shouldn't print anything
                /// </summary>
                [WpfFact]
                public void Substitute1()
                {
                    Create("cat", "dog");
                    var sawError = false;
                    _vimBuffer.ErrorMessage += delegate { sawError = true; };
                    RunCommand("s/z/o/e");
                    Assert.False(sawError);
                }

                /// <summary>
                /// Simple search and replace
                /// </summary>
                [WpfFact]
                public void Substitute2()
                {
                    Create("cat bat", "dag");
                    RunCommand("s/a/o/g 2");
                    Assert.Equal("cot bot", _textBuffer.GetLine(0).GetText());
                    Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
                }

                /// <summary>
                /// Repeat of the last search with a new flag
                /// </summary>
                [WpfFact]
                public void Substitute3()
                {
                    Create("cat bat", "dag");
                    _vimBuffer.VimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "o", SubstituteFlags.None));
                    RunCommand("s g 2");
                    Assert.Equal("cot bot", _textBuffer.GetLine(0).GetText());
                    Assert.Equal("dog", _textBuffer.GetLine(1).GetText());
                }

                /// <summary>
                /// Testing the print option
                /// </summary>
                [WpfFact]
                public void Substitute4()
                {
                    Create("cat bat", "dag");
                    var message = String.Empty;
                    _vimBuffer.StatusMessage += (_, e) => { message = e.Message; };
                    RunCommand("s/a/b/p");
                    Assert.Equal("cbt bat", message);
                }

                /// <summary>
                /// Testing the print number option
                /// </summary>
                [WpfFact]
                public void Substitute6()
                {
                    Create("cat bat", "dag");
                    var message = String.Empty;
                    _vimBuffer.StatusMessage += (_, e) => { message = e.Message; };
                    RunCommand("s/a/b/#");
                    Assert.Equal("  1 cbt bat", message);
                }

                /// <summary>
                /// Testing the print list option
                /// </summary>
                [WpfFact]
                public void Substitute7()
                {
                    Create("cat bat", "dag");
                    var message = String.Empty;
                    _vimBuffer.StatusMessage += (_, e) => { message = e.Message; };
                    RunCommand("s/a/b/l");
                    Assert.Equal("cbt bat$", message);
                }

                /// <summary>
                /// Verify we handle escaped back slashes correctly
                /// </summary>
                [WpfFact]
                public void WithBackslashes()
                {
                    Create(@"\\\\abc\\\\def");
                    RunCommand(@"s/\\\{4\}/\\\\/g");
                    Assert.Equal(@"\\abc\\def", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Convert a set of spaces into tabs with the '\t' replacement
                /// </summary>
                [WpfFact]
                public void TabsForSpaces()
                {
                    Create("    ");
                    RunCommand(@"s/  /\t");
                    Assert.Equal("\t  ", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Convert spaces into new lines with the '\r' replacement
                /// </summary>
                [WpfFact]
                public void SpacesToNewLine()
                {
                    Create("dog chases cat");
                    RunCommand(@"s/ /\r/g");
                    Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
                    Assert.Equal("chases", _textBuffer.GetLine(1).GetText());
                    Assert.Equal("cat", _textBuffer.GetLine(2).GetText());
                }

                [WpfFact]
                public void DefaultsToMagicMode()
                {
                    Create("a.c", "abc");
                    RunCommand(@"%s/a\.c/replaced/g");
                    Assert.Equal(_textBuffer.GetLine(0).GetText(), "replaced");
                    Assert.Equal(_textBuffer.GetLine(1).GetText(), "abc");
                }

                /// <summary>
                /// Make sure the "\1" does a group substitution instead of pushing in the literal 1
                /// </summary>
                [WpfFact]
                public void ReplaceWithGroup()
                {
                    Create(@"cat (dog)");
                    RunCommand(@"s/(\(\w\+\))/\1/");
                    Assert.Equal(@"cat dog", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void NewlinesCanBeReplaced()
                {
                    Create("foo", "bar");
                    RunCommand(@"%s/\n/ /");
                    Assert.Equal(_textBuffer.GetLine(0).GetText(), "foo bar");
                }

                /// <summary>
                /// Covers #763 where the default search for substitute uses the last substitute
                /// instead of the last search
                /// </summary>
                [WpfFact]
                public void SubstituteThenSearchThenUsesPatternFromLastSearch()
                {
                    Create("foo", "bar");

                    RunCommandRaw(":%s/foo/foos");
                    RunCommandRaw("/bar");
                    RunCommandRaw(":%s//baz");

                    Assert.Equal(_textBuffer.GetLine(1).Extent.GetText(), "baz");
                }

                [WpfFact]
                public void SubstituteThenSearchThenUsesPatternFromLastSubstitute()
                {
                    Create("foo foo foo");

                    RunCommandRaw(":%s/foo/bar");
                    RunCommandRaw("/bar");
                    // Do same substitute as the last substitute, but global this time
                    RunCommandRaw(":%&g");

                    Assert.Equal(_textBuffer.GetLine(0).Extent.GetText(), "bar bar bar");
                }

                /// <summary>
                /// Baseline to make sure I don't break anything while fixing #763
                /// </summary>
                [WpfFact]
                public void SubstituteThenUsesPatternFromLastSubstitute()
                {
                    Create("foo", "bar");

                    RunCommandRaw(":%s/foo/foos");
                    RunCommandRaw(":%s//baz");

                    Assert.Equal(_textBuffer.GetLine(0).Extent.GetText(), "bazs");
                }

                /// <summary>
                /// Make sure that we can handle a space between the :substitute command name 
                /// and the pattern
                /// </summary>
                [WpfFact]
                public void SpaceAfterCommandName()
                {
                    Create("ca t");
                    RunCommandRaw(":s / /");
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void AsteriskReplace()
                {
                    Create("dog");
                    RunCommandRaw(":s/o/*");
                    Assert.Equal("d*g", _textBuffer.GetLine(0).GetText());
                }

                [WpfFact]
                public void AsteriskAndExtraReplace()
                {
                    Create("dog");
                    RunCommandRaw(":s/o/*8");
                    Assert.Equal("d*8g", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Integration test for issue #973.  The key problem here is that the regex built
                /// from the substitute was ignoring the ignorecase and smartcase options.  It was 
                /// instead creating literally from the substitute flags
                /// </summary>
                [WpfFact]
                public void Issue973()
                {
                    Create("vols.First()");
                    _vimBuffer.GlobalSettings.IgnoreCase = true;
                    RunCommandRaw(":%s/vols.first()/target/g");
                    Assert.Equal("target", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure that we can handle a space before the substitute command 
                /// </summary>
                [WpfFact]
                public void Issue1057()
                {
                    Create("ca t");
                    RunCommandRaw(": s / /");
                    Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                }

                /// <summary>
                /// Even a failed substitution should update the last pattern
                /// </summary>
                [WpfFact]
                public void Issue1244()
                {
                    Create("cat", "dog", "fish");
                    RunCommandRaw(":s/dog/food");
                    Assert.Equal(_textView.GetPointInLine(0, 0), _textView.GetCaretPoint());
                    _vimBuffer.Process("n");
                    Assert.Equal(_textView.GetPointInLine(1, 0), _textView.GetCaretPoint());
                }
            }
        }

        public sealed class RunHostCommandTest : CommandModeIntegrationTest
        {
            [WpfFact]
            public void SimpleCommand()
            {
                Create("");
                var didRun = false;
                _vimHost.RunHostCommandFunc = (textView, commandName, argument) =>
                    {
                        didRun = true;
                        Assert.Equal("Edit.Comment", commandName);
                        Assert.Equal("", argument);
                    };
                RunCommandRaw(":vsc Edit.Comment");
                Assert.True(didRun);
            }

            /// <summary>
            /// It is legal for visual studio commands to have underscores in the name
            /// </summary>
            [WpfFact]
            public void NameWithUnderscore()
            {
                Create("");
                var didRun = false;
                _vimHost.RunHostCommandFunc = (textView, commandName, argument) =>
                    {
                        didRun = true;
                        Assert.Equal("Edit_Comment", commandName);
                        Assert.Equal("", argument);
                    };
                RunCommandRaw(":vsc Edit_Comment");
                Assert.True(didRun);
            }

            /// <summary>
            /// While we don't actually pass the range down to the Visual Studio Command function, the 
            /// underlying commands themselves can act on the active selection (think comment).  Given that
            /// : will always prefix a range if there is a selection we should support the range to make
            /// key mappings easier to use
            /// </summary>
            [WpfFact]
            public void Range()
            {
                Create("cat", "dog");
                _vimBuffer.VimTextBuffer.SetLocalMark(LocalMark.NewLetter(Letter.A), 0, 1);
                _vimBuffer.VimTextBuffer.SetLocalMark(LocalMark.NewLetter(Letter.B), 0, 1);
                var didRun = false;
                _vimHost.RunHostCommandFunc = (textView, commandName, argument) =>
                    {
                        didRun = true;
                        Assert.Equal("Edit.Comment", commandName);
                        Assert.Equal("", argument);
                    };
                RunCommandRaw(":'a,'bvsc Edit.Comment");
                Assert.True(didRun);
            }
        }

        public sealed class YankTest : CommandModeIntegrationTest
        {
            private void AssertLines(params string[] lines)
            {
                var text = lines.Select(x => x + Environment.NewLine).Aggregate((x, y) => x + y);
                Assert.Equal(text, UnnamedRegister.StringValue);
            }

            [WpfFact]
            public void Multiple()
            {
                Create("cat", "dog", "tree");
                RunCommand("y2");
                AssertLines("cat", "dog");
            }

            [WpfFact]
            public void MultipleThree()
            {
                Create("cat", "dog", "tree", "fish");
                RunCommand("y3");
                AssertLines("cat", "dog", "tree");
            }

            [WpfFact]
            public void Single()
            {
                Create("cat", "dog", "tree");
                RunCommand("y");
                AssertLines("cat");
            }

            /// <summary>
            /// The first count is the range 
            /// </summary>
            [WpfFact]
            public void RangeSingleLine()
            {
                Create("cat", "dog", "tree");
                RunCommand("2y");
                AssertLines("dog");
            }

            [WpfFact]
            public void RangeMultiLine()
            {
                Create("cat", "dog", "tree", "fish");
                RunCommand("2,3y");
                AssertLines("dog", "tree");
            }

            /// <summary>
            /// Yank a count from the specified line range
            /// </summary>
            [WpfFact]
            public void RangeSingleLineWithCount()
            {
                Create("cat", "dog", "tree", "fish");
                RunCommand("2y2");
                AssertLines("dog", "tree");
            }

            /// <summary>
            /// Yank a count from the end of the specified line range
            /// </summary>
            [WpfFact]
            public void RangeMultiLineWithCount()
            {
                Create("cat", "dog", "tree", "fish", "rock");
                RunCommand("2,3y2");
                AssertLines("tree", "fish");
            }

            /// <summary>
            /// A line wise value in a register should always be normalized to end with a new 
            /// line.  In this particular case the last line doesn't have a new line so the code
            /// must add it in advance
            ///
            /// Issue 1526
            /// </summary>
            [WpfFact]
            public void YankIncludesLastLine()
            {
                Create("foo", "bar", "baz");
                _textView.MoveCaretToLine(1);
                RunCommand("y2");
                Assert.True(UnnamedRegister.StringValue.EndsWith(Environment.NewLine));
            }
        }

        public sealed class RangeTest : CommandModeIntegrationTest
        {
            [WpfFact]
            public void CurrentLineWithEndCount()
            {
                Create("dog", "cat");
                _vimBuffer.LocalSettings.ShiftWidth = 2;
                RunCommand(".1>");
                Assert.Equal(new[] { "dog", "  cat" }, _textBuffer.GetLines());
            }

            [WpfFact]
            public void CurrentLineWithEndCountRange()
            {
                Create("dog", "cat", "tree");
                _vimBuffer.LocalSettings.ShiftWidth = 2;
                RunCommand(".,.1>");
                Assert.Equal(new[] { "  dog", "  cat", "tree" }, _textBuffer.GetLines());
            }
        }

        public sealed class MiscTest : CommandModeIntegrationTest
        {
            [WpfFact]
            public void JumpLine1()
            {
                Create("a", "b", "c", "d");
                RunCommand("0");
                Assert.Equal(0, _textView.Caret.Position.BufferPosition.Position);
                RunCommand("1");
                Assert.Equal(0, _textView.Caret.Position.BufferPosition.Position);
            }

            /// <summary>
            /// Non-first line
            /// </summary>
            [WpfFact]
            public void JumpLine2()
            {
                Create("a", "b", "c", "d");
                RunCommand("2");
                Assert.Equal(_textView.TextSnapshot.GetLineFromLineNumber(1).Start, _textView.Caret.Position.BufferPosition);
            }

            [WpfFact]
            public void JumpLineLastWithNoWhiteSpace()
            {
                Create("dog", "cat", "tree");
                RunCommand("$");
                var tss = _textView.TextSnapshot;
                var last = tss.GetLineFromLineNumber(tss.LineCount - 1);
                Assert.Equal(last.Start, _textView.GetCaretPoint());
            }

            [WpfFact]
            public void JumpLineLastWithWhiteSpace()
            {
                Create("dog", "cat", "  tree");
                RunCommand("$");
                var tss = _textView.TextSnapshot;
                var last = tss.GetLineFromLineNumber(tss.LineCount - 1);
                Assert.Equal(last.Start.Add(2), _textView.GetCaretPoint());
            }

            /// <summary>
            /// Make sure that we don't crash or print anything when :map is run with no mappings
            /// </summary>
            [WpfFact]
            public void KeyMap_NoMappings()
            {
                Create("");
                RunCommand("map");
                Assert.Equal("", _lastStatus);
            }

            /// <summary>
            /// In Vim it's legal to unmap a key command with the expansion
            /// </summary>
            [WpfFact]
            public void KeyMap_UnmapByExpansion()
            {
                Create("");
                RunCommand("imap cat dog");
                Assert.Equal(1, KeyMap.GetKeyMappingsForMode(KeyRemapMode.Insert).Length);
                RunCommand("iunmap dog");
                Assert.Equal(0, KeyMap.GetKeyMappingsForMode(KeyRemapMode.Insert).Length);
            }

            /// <summary>
            /// The ! in unmap should cause it to umap command and insert commands.  Make sure it
            /// works for unmap by expansion as well
            /// </summary>
            [WpfFact]
            public void KeyMap_UnmapByExpansionUsingBang()
            {
                Create("");
                RunCommand("imap cat dog");
                Assert.Equal(1, KeyMap.GetKeyMappingsForMode(KeyRemapMode.Insert).Length);
                RunCommand("unmap! dog");
                Assert.Equal(0, KeyMap.GetKeyMappingsForMode(KeyRemapMode.Insert).Length);
            }

            /// <summary>
            /// Using the search forward feature which hits a match.  Search should start after the range
            /// so the first match will be after it 
            /// </summary>
            [WpfFact]
            public void Search_ForwardWithMatch()
            {
                Create("cat", "dog", "cat", "fish");
                RunCommand("1,2/cat");
                Assert.Equal(_textBuffer.GetLine(2).Start, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Using the search forward feature which doesn't hit a match in the specified path.  Should 
            /// raise a warning
            /// </summary>
            [WpfFact]
            public void Search_ForwardWithNoMatchInPath()
            {
                Create("cat", "dog", "cat", "fish");
                var didHit = false;
                _vimBuffer.LocalSettings.GlobalSettings.WrapScan = false;
                _vimBuffer.ErrorMessage +=
                    (sender, args) =>
                    {
                        Assert.Equal(Resources.Common_SearchHitBottomWithout("cat"), args.Message);
                        didHit = true;
                    };
                RunCommand("1,3/cat");
                Assert.True(didHit);
            }

            /// <summary>
            /// No match in the buffer should raise a different message
            /// </summary>
            [WpfFact]
            public void Search_ForwardWithNoMatchInBuffer()
            {
                Create("cat", "dog", "cat", "fish");
                var didHit = false;
                _vimBuffer.ErrorMessage +=
                    (sender, args) =>
                    {
                        Assert.Equal(Resources.Common_PatternNotFound("pig"), args.Message);
                        didHit = true;
                    };
                RunCommand("1,2/pig");
                Assert.True(didHit);
            }

            [WpfFact]
            public void SwitchTo()
            {
                Create("");
                _vimBuffer.Process(':');
                Assert.Equal(ModeKind.Command, _vimBuffer.ModeKind);
            }

            [WpfFact]
            public void SwitchOut()
            {
                Create("");
                RunCommand("e foo");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            [WpfFact]
            public void SwitchOutFromBackspace()
            {
                Create("");
                _vimBuffer.Process(':');
                _vimBuffer.Process(VimKey.Back);
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            [WpfFact]
            public void Yank_WithRange()
            {
                Create("cat", "dog", "fish");
                _vimBuffer.VimTextBuffer.SetLocalMark(LocalMark.NewLetter(Letter.A), 0, 0);
                _vimBuffer.VimTextBuffer.SetLocalMark(LocalMark.NewLetter(Letter.B), 1, 0);
                RunCommand("'a,'by");
                Assert.Equal("cat" + Environment.NewLine + "dog" + Environment.NewLine, Vim.RegisterMap.GetRegister(RegisterName.Unnamed).StringValue);
            }

            /// <summary>
            /// The `. mark should go to the last edit position on the last edit line
            /// </summary>
            [WpfFact]
            public void Replace_GoToLastEditPosition()
            {
                Create("cat", "dog");
                _textView.MoveCaretToLine(1, 1);
                _vimBuffer.ProcessNotation("ru");
                _textView.MoveCaretToLine(0, 0);

                Assert.Equal("dug", _textView.GetLine(1).GetText());

                Assert.Equal(0, _textView.Caret.Position.BufferPosition.GetColumn().Column);
                Assert.Equal(0, _textView.GetCaretLine().LineNumber);

                _vimBuffer.ProcessNotation("`.");

                Assert.Equal(1, _textView.Caret.Position.BufferPosition.GetColumn().Column);
                Assert.Equal(1, _textView.GetCaretLine().LineNumber);
            }

            [WpfFact]
            public void Issue1327()
            {
                Create("cat", "dog");
                _textView.MoveCaretTo(3);
                RunCommand("wq");
            }

            [WpfFact]
            public void Issue1794()
            {
                Create("cat", "dog", "tree");
                _vimBuffer.LocalSettings.ShiftWidth = 2;
                _vimBuffer.MarkMap.SetLocalMark('a', _vimBuffer.VimBufferData, line: 0, column: 0);
                _vimBuffer.MarkMap.SetLocalMark('b', _vimBuffer.VimBufferData, line: 1, column: 0);
                RunCommandRaw(":'a,'b >");
                Assert.Equal(new[] { "  cat", "  dog", "tree" }, _textBuffer.GetLines());
            }
        }
    }
}
