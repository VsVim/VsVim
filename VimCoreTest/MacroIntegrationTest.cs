using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class MacroIntegrationTest
    {
        private IVimBuffer _buffer;
        private IWpfTextView _textView;
        private IVimGlobalSettings _globalSettings;

        internal char TestRegisterChar
        {
            get { return 'c'; }
        }

        internal Register TestRegister
        {
            get { return _buffer.RegisterMap.GetRegister(TestRegisterChar); }
        }

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateTextView(lines);
            _buffer = EditorUtil.FactoryService.Vim.CreateBuffer(_textView);
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _globalSettings = _buffer.LocalSettings.GlobalSettings;
            ((MockVimHost)_buffer.Vim.VimHost).FocusedTextView = _textView;
        }

        /// <summary>
        /// Make sure that on tear down we don't have a current transaction.  Having one indicates
        /// we didn't close it and hence are killing undo in the ITextBuffer
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            var history = EditorUtil.GetUndoHistory(_textView.TextBuffer);
            Assert.IsNull(history.CurrentTransaction);
        }

        /// <summary>
        /// RunMacro a text insert back from a particular register
        /// </summary>
        [Test]
        public void RunMacro_InsertText()
        {
            Create("world");
            TestRegister.UpdateValue("ihello ");
            _buffer.Process("@c");
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.AreEqual("hello world", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Replay a text insert back from a particular register which also contains an Escape key
        /// </summary>
        [Test]
        public void RunMacro_InsertTextWithEsacpe()
        {
            Create("world");
            TestRegister.UpdateValue("ihello ", VimKey.Escape);
            _buffer.Process("@c");
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
            Assert.AreEqual("hello world", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// When running a macro make sure that we properly repeat the last command
        /// </summary>
        [Test]
        public void RunMacro_RepeatLastCommand_DeleteWord()
        {
            Create("hello world again");
            TestRegister.UpdateValue(".");
            _buffer.Process("dw@c");
            Assert.AreEqual("again", _textView.GetLine(0).GetText());
            Assert.IsTrue(_buffer.VimData.LastMacroRun.IsSome('c'));
        }

        /// <summary>
        /// When running the last macro with a count it should do the macro 'count' times
        /// </summary>
        [Test]
        public void RunMacro_WithCount()
        {
            Create("cat", "dog", "bear");
            TestRegister.UpdateValue("~", VimKey.Left, VimKey.Down);
            _buffer.Process("2@c");
            Assert.AreEqual("Cat", _textView.GetLine(0).GetText());
            Assert.AreEqual("Dog", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Any command which produces an error should cause the macro to stop playback.  One
        /// such command is trying to move right past the end of a line in insert mode
        /// </summary>
        [Test]
        public void Error_RightMove()
        {
            Create("cat", "cat");
            _globalSettings.VirtualEdit = string.Empty; // ensure not 've=onemore'
            TestRegister.UpdateValue("llidone", VimKey.Escape);

            // Works because 'll' can get to the end of the line
            _buffer.Process("@c");
            Assert.AreEqual("cadonet", _textView.GetLine(0).GetText());

            // Fails since the second 'l' fails
            _textView.MoveCaretToLine(1, 2);
            _buffer.Process("@c");
            Assert.AreEqual("cat", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Recursive macros which move to the end of the line shouldn't recurse infinitely
        /// </summary>
        [Test]
        public void Error_RecursiveRightMove()
        {
            Create("cat", "dog");
            _globalSettings.VirtualEdit = string.Empty; // Ensure not 've=onemore'
            TestRegister.UpdateValue("l@c");
            _buffer.Process("@c");
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// An up move at the start of the ITextBuffer should be an error and hence break 
        /// a macro execution.  But the results of the macro before the error should be 
        /// still visible
        /// </summary>
        [Test]
        public void Error_UpMove()
        {
            Create("dog cat tree", "dog cat tree");
            TestRegister.UpdateValue("lkdw");
            _buffer.Process("@c");
            Assert.AreEqual("dog cat tree", _textView.GetLine(0).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Record a a text insert sequence followed by escape and play it back
        /// </summary>
        [Test]
        public void Record_InsertTextAndEscape()
        {
            Create("");
            _buffer.Process("qcidog");
            _buffer.Process(VimKey.Escape);
            _buffer.Process("q");
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
            _textView.MoveCaretTo(0);
            _buffer.Process("@c");
            Assert.AreEqual("dogdog", _textView.GetLine(0).GetText());
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// When using an upper case register notation make sure the information is appended to
        /// the existing value.  This can and will cause different behavior to occur
        /// </summary>
        [Test]
        public void Record_AppendValues()
        {
            Create("");
            TestRegister.UpdateValue("iw");
            _buffer.Process("qCin");
            _buffer.Process(VimKey.Escape);
            _buffer.Process("q");
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
            _textView.SetText("");
            _textView.MoveCaretTo(0);
            _buffer.Process("@c");
            Assert.AreEqual("win", _textView.GetLine(0).GetText());
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Running a macro which consists of several commands should cause only the last
        /// command to be the last command for the purpose of a 'repeat' operation
        /// </summary>
        [Test]
        public void RepeatCommandAfterRunMacro()
        {
            Create("hello world", "kick tree");
            TestRegister.UpdateValue("dwra");
            _buffer.Process("@c");
            Assert.AreEqual("aorld", _textView.GetLine(0).GetText());
            _textView.MoveCaretToLine(1);
            _buffer.Process(".");
            Assert.AreEqual("aick tree", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// The @@ command should just read the char on the LastMacroRun value and replay 
        /// that macro
        /// </summary>
        [Test]
        public void RunLastMacro_ReadTheRegister()
        {
            Create("");
            TestRegister.UpdateValue("iwin");
            _buffer.VimData.LastMacroRun = FSharpOption.Create('c');
            _buffer.Process("@@");
            Assert.AreEqual("win", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// A macro run with a count should execute as a single action.  This includes undo behavior
        /// </summary>
        [Test]
        public void Undo_MacroWithCount()
        {
            Create("cat", "dog", "bear");
            TestRegister.UpdateValue("~", VimKey.Left, VimKey.Down);
            _buffer.Process("2@c");
            _buffer.Process("u");
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.AreEqual("cat", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
        }
    }
}
