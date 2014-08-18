using System;
using EditorUtils;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Extensions;
using Xunit;
using Microsoft.VisualStudio.Text;

namespace Vim.UnitTest
{
    public abstract class MacroIntegrationTest : VimTestBase
    {
        protected IVimBuffer _vimBuffer;
        protected ITextBuffer _textBuffer;
        protected ITextView _textView;
        protected IVimGlobalSettings _globalSettings;

        internal char TestRegisterChar
        {
            get { return 'c'; }
        }

        internal Register TestRegister
        {
            get { return _vimBuffer.RegisterMap.GetRegister(TestRegisterChar); }
        }

        protected void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _globalSettings = _vimBuffer.LocalSettings.GlobalSettings;
            VimHost.FocusedTextView = _textView;
        }

        /// <summary>
        /// Make sure that on tear down we don't have a current transaction.  Having one indicates
        /// we didn't close it and hence are killing undo in the ITextBuffer
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();

            var history = TextBufferUndoManagerProvider.GetTextBufferUndoManager(_textView.TextBuffer).TextBufferUndoHistory;
            Assert.Null(history.CurrentTransaction);
        }

        public sealed class RunMacroTest : MacroIntegrationTest
        {
            /// <summary>
            /// RunMacro a text insert back from a particular register
            /// </summary>
            [Fact]
            public void InsertText()
            {
                Create("world");
                TestRegister.UpdateValue("ihello ");
                _vimBuffer.Process("@c");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("hello world", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Replay a text insert back from a particular register which also contains an Escape key
            /// </summary>
            [Fact]
            public void InsertTextWithEsacpe()
            {
                Create("world");
                TestRegister.UpdateValue("ihello ", VimKey.Escape);
                _vimBuffer.Process("@c");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal("hello world", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// When running a macro make sure that we properly repeat the last command
            /// </summary>
            [Fact]
            public void RepeatLastCommand_DeleteWord()
            {
                Create("hello world again");
                TestRegister.UpdateValue(".");
                _vimBuffer.Process("dw@c");
                Assert.Equal("again", _textView.GetLine(0).GetText());
                Assert.True(_vimBuffer.VimData.LastMacroRun.IsSome('c'));
            }

            /// <summary>
            /// When running the last macro with a count it should do the macro 'count' times
            /// </summary>
            [Fact]
            public void WithCount()
            {
                Create("cat", "dog", "bear");
                TestRegister.UpdateValue("~", VimKey.Left, VimKey.Down);
                _vimBuffer.Process("2@c");
                Assert.Equal("Cat", _textView.GetLine(0).GetText());
                Assert.Equal("Dog", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// This is actually a macro scenario called out in the Vim documentation.  Namely the ability
            /// to build a numbered list by using a macro
            /// </summary>
            [Fact]
            public void NumberedList()
            {
                Create("1. Heading");
                _vimBuffer.Process("qaYp");
                _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-a>"));
                _vimBuffer.Process("q3@a");
                for (var i = 0; i < 5; i++)
                {
                    var line = String.Format("{0}. Heading", i + 1);
                    Assert.Equal(line, _textView.GetLine(i).GetText());
                }
            }

            /// <summary>
            /// If there is no focussed IVimBuffer then the macro playback should use the original IVimBuffer
            /// </summary>
            [Fact]
            public void NoFocusedView()
            {
                Create("world");
                VimHost.FocusedTextView = null;
                TestRegister.UpdateValue("ihello ");
                _vimBuffer.Process("@c");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                Assert.Equal("hello world", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Record a a text insert sequence followed by escape and play it back
            /// </summary>
            [Fact]
            public void InsertTextAndEscape()
            {
                Create("");
                _vimBuffer.Process("qcidog");
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process("q");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                _textView.MoveCaretTo(0);
                _vimBuffer.Process("@c");
                Assert.Equal("dogdog", _textView.GetLine(0).GetText());
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// When using an upper case register notation make sure the information is appended to
            /// the existing value.  This can and will cause different behavior to occur
            /// </summary>
            [Fact]
            public void AppendValues()
            {
                Create("");
                TestRegister.UpdateValue("iw");
                _vimBuffer.Process("qCin");
                _vimBuffer.Process(VimKey.Escape);
                _vimBuffer.Process("q");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                _textView.SetText("");
                _textView.MoveCaretTo(0);
                _vimBuffer.Process("@c");
                Assert.Equal("win", _textView.GetLine(0).GetText());
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// The ^ motion shouldn't register as an error at the start of the line and hence shouldn't
            /// cancel macro playback
            /// </summary>
            [Fact]
            public void StartOfLineAndChange()
            {
                Create("  cat dog");
                _textView.MoveCaretTo(2);
                TestRegister.UpdateValue("^cwfish");
                _vimBuffer.Process("@c");
                Assert.Equal(0, VimHost.BeepCount);
                Assert.Equal("  fish dog", _textBuffer.GetLine(0).GetText());
                Assert.Equal(6, _textView.GetCaretPoint());
            }
        }

        public sealed class RunLastMacroTest : MacroIntegrationTest
        {
            /// <summary>
            /// When the word completion command is run and there are no completions this shouldn't
            /// register as an error and macro processing should continue
            /// </summary>
            [Fact]
            public void WordCompletionWithNoCompletion()
            {
                Create("z ");
                _textView.MoveCaretTo(1);
                TestRegister.UpdateValue(
                    KeyNotationUtil.StringToKeyInput("i"),
                    KeyNotationUtil.StringToKeyInput("<C-n>"),
                    KeyNotationUtil.StringToKeyInput("s"));
                _vimBuffer.Process("@c");
                Assert.Equal("zs ", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// The @@ command should just read the char on the LastMacroRun value and replay 
            /// that macro
            /// </summary>
            [Fact]
            public void ReadTheRegister()
            {
                Create("");
                TestRegister.UpdateValue("iwin");
                _vimBuffer.VimData.LastMacroRun = FSharpOption.Create('c');
                _vimBuffer.Process("@@");
                Assert.Equal("win", _textView.GetLine(0).GetText());
            }
        }

        public sealed class ErrorTest : MacroIntegrationTest
        {
            /// <summary>
            /// Any command which produces an error should cause the macro to stop playback.  One
            /// such command is trying to move right past the end of a line in insert mode
            /// </summary>
            [Fact]
            public void RightMove()
            {
                Create("cat", "cat");
                _globalSettings.VirtualEdit = string.Empty; // ensure not 've=onemore'
                TestRegister.UpdateValue("llidone", VimKey.Escape);

                // Works because 'll' can get to the end of the line
                _vimBuffer.Process("@c");
                Assert.Equal("cadonet", _textView.GetLine(0).GetText());

                // Fails since the second 'l' fails
                _textView.MoveCaretToLine(1, 2);
                _vimBuffer.Process("@c");
                Assert.Equal("cat", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// Recursive macros which move to the end of the line shouldn't recurse infinitely
            /// </summary>
            [Fact]
            public void RecursiveRightMove()
            {
                Create("cat", "dog");
                _globalSettings.VirtualEdit = string.Empty; // Ensure not 've=onemore'
                TestRegister.UpdateValue("l@c");
                _vimBuffer.Process("@c");
                Assert.Equal(2, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// An up move at the start of the ITextBuffer should be an error and hence break 
            /// a macro execution.  But the results of the macro before the error should be 
            /// still visible
            /// </summary>
            [Fact]
            public void UpMove()
            {
                Create("dog cat tree", "dog cat tree");
                TestRegister.UpdateValue("lkdw");
                _vimBuffer.Process("@c");
                Assert.Equal("dog cat tree", _textView.GetLine(0).GetText());
                Assert.Equal(1, _textView.GetCaretPoint().Position);
            }

            /// <summary>
            /// Attempting to move left before the beginining of the line should register as an error
            /// and hence kill macro playbakc
            /// </summary>
            [Fact]
            public void LeftMoveBeforeLine()
            {
                Create("dog cat tree");
                _textView.MoveCaretTo(1);
                TestRegister.UpdateValue("hhhhdw");
                _vimBuffer.Process("@c");
                Assert.Equal(1, VimHost.BeepCount);
                Assert.Equal("dog cat tree", _textBuffer.GetLine(0).GetText());
                Assert.Equal(0, _textView.GetCaretPoint());
            }

            /// <summary>
            /// Attempting to move right after the end of the line should register as an error and
            /// hence kill macro playback
            /// </summary>
            [Fact]
            public void RightMoveAfterLine()
            {
                Create("dog cat");
                _textView.MoveCaretTo(4);
                TestRegister.UpdateValue("lllllD");
                _vimBuffer.Process("@c");
                Assert.Equal(1, VimHost.BeepCount);
                Assert.Equal("dog cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal(6, _textView.GetCaretPoint());
            }
        }

        public sealed class KeyMappingTest : MacroIntegrationTest
        {
            /// <summary>
            /// During macro evaluation what is typed is what should be recorded, not what is actually
            /// processed by the buffer.  If the user types 'h' but it is mapped to 'u' then 'h' should
            /// be recorded
            /// </summary>
            [Fact]
            public void RecordTyped()
            {
                Create("cat dog");
                _textView.MoveCaretTo(4);
                _vimBuffer.Process(":noremap l h", enter: true);
                _vimBuffer.Process("qfllq");
                Assert.Equal(2, _textView.GetCaretPoint().Position);
                Assert.Equal("ll", _vimBuffer.GetRegister('f').StringValue);
            }

            /// <summary>
            /// The macro replay should consider mappings as they exist during the replay.  If the mappings
            /// change after a record occurs then the behavior of the replay should demonstrate that
            /// change 
            /// </summary>
            [Fact]
            public void ConsiderMappingDuringReplay()
            {
                Create("cat");
                _vimBuffer.GetRegister('c').UpdateValue("ibig ");
                _vimBuffer.Process("@c");
                Assert.Equal("big cat", _textBuffer.GetLine(0).GetText());
            }

            [Fact]
            public void Issue1117()
            {
                Create("cat", "dog", "fish", "hello", "world", "ok");
                _vimBuffer.Process(":noremap h k", enter: true);
                _vimBuffer.Process(":noremap k j", enter: true);
                _textView.MoveCaretToLine(5);
                _vimBuffer.Process("qfhhq@f");
                Assert.Equal(1, _textView.GetCaretPoint().GetContainingLine().LineNumber); 
            }
        }

        public sealed class MiscTest : MacroIntegrationTest
        {
            /// <summary>
            /// Running a macro which consists of several commands should cause only the last
            /// command to be the last command for the purpose of a 'repeat' operation
            /// </summary>
            [Fact]
            public void RepeatCommandAfterRunMacro()
            {
                Create("hello world", "kick tree");
                TestRegister.UpdateValue("dwra");
                _vimBuffer.Process("@c");
                Assert.Equal("aorld", _textView.GetLine(0).GetText());
                _textView.MoveCaretToLine(1);
                _vimBuffer.Process(".");
                Assert.Equal("aick tree", _textView.GetLine(1).GetText());
            }

            /// <summary>
            /// A macro run with a count should execute as a single action.  This includes undo behavior
            /// </summary>
            [Fact]
            public void UndoMacroWithCount()
            {
                Create("cat", "dog", "bear");
                TestRegister.UpdateValue("~", VimKey.Left, VimKey.Down);
                _vimBuffer.Process("2@c");
                _vimBuffer.Process("u");
                Assert.Equal(0, _textView.GetCaretPoint().Position);
                Assert.Equal("cat", _textView.GetLine(0).GetText());
                Assert.Equal("dog", _textView.GetLine(1).GetText());
            }

            [Fact]
            public void RepeatLinked()
            {
                Create("cat", "dog", "bear");
                _vimBuffer.ProcessNotation(@"qccwbat<Esc>q");
                _textView.MoveCaretToLine(1);
                _vimBuffer.ProcessNotation(@"@c");
                Assert.Equal("bat", _textBuffer.GetLine(1).GetText());
            }
        }
    }
}
