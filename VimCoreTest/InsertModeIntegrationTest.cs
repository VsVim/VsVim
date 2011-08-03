using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    /// <summary>
    /// Used to test the integrated behavior if Insert Mode 
    /// </summary>
    [TestFixture]
    public sealed class InsertModeIntegrationTest : VimTestBase
    {
        private IVimBuffer _buffer;
        private IWpfTextView _textView;
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            Create(ModeArgument.None, lines);
        }

        private void Create(ModeArgument argument, params string[] lines)
        {
            var tuple = EditorUtil.CreateTextViewAndEditorOperations(lines);
            _textView = tuple.Item1;
            _textBuffer = _textView.TextBuffer;
            var service = EditorUtil.FactoryService;
            _buffer = service.Vim.CreateBuffer(_textView);
            _buffer.SwitchMode(ModeKind.Insert, argument);
        }

        /// <summary>
        /// Clear out the key mappings on tear down so that they don't affect future suite
        /// runs
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            EditorUtil.FactoryService.Vim.KeyMap.ClearAll();
        }

        /// <summary>
        /// Make sure that the ITextView isn't accessed in insert mode if it's active and the 
        /// ITextView is closed
        /// </summary>
        [Test]
        [Description("Make sure we don't access the ITextView on the way down")]
        public void CloseInInsertMode()
        {
            Create("foo", "bar");
            _textView.Close();
        }

        /// <summary>
        /// Ensure that delete all indent both deletes the indent and preserves the caret position
        /// </summary>
        [Test]
        public void DeleteAllIndent()
        {
            Create("       hello");
            _textView.MoveCaretTo(8);
            _buffer.Process("0");
            _buffer.Process(KeyInputUtil.CharWithControlToKeyInput('d'));
            Assert.AreEqual("hello", _textView.GetLine(0).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure that in the case where there is buffered input and we fail at the mapping 
        /// that both values are inserted into the ITextBuffer
        /// </summary>
        [Test]
        public void KeyRemap_BufferedInputFailsMapping()
        {
            Create("");
            _buffer.Vim.KeyMap.MapWithNoRemap("jj", "<Esc>", KeyRemapMode.Insert);
            _buffer.Process("j");
            Assert.AreEqual("", _textBuffer.GetLine(0).GetText());
            _buffer.Process("a");
            Assert.AreEqual("ja", _textBuffer.GetLine(0).GetText());
        }

        /// <summary>
        /// Ensure we can use a double keystroke to escape
        /// </summary>
        [Test]
        public void KeyRemap_TwoKeysToEscape()
        {
            Create(ModeArgument.NewInsertWithCount(2), "hello");
            _buffer.Vim.KeyMap.MapWithNoRemap("jj", "<Esc>", KeyRemapMode.Insert);
            _buffer.Process("jj");
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
        }

        /// <summary>
        /// Ensure when the mode is entered with a count that the escape will cause the 
        /// text to be repeated
        /// </summary>
        [Test]
        public void Repeat_Insert()
        {
            Create(ModeArgument.NewInsertWithCount(2), "the cat");
            _buffer.Process("hi");
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual("hihithe cat", _textView.GetLine(0).GetText());
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Ensure when the mode is entered with a count that the escape will cause the
        /// deleted text to be repeated
        /// </summary>
        [Test]
        public void Repeat_Delete()
        {
            Create(ModeArgument.NewInsertWithCount(2), "doggie");
            _textView.MoveCaretTo(1);
            _buffer.Process(VimKey.Delete);
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual("dgie", _textView.GetLine(0).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Repeated white space change to tabs should only repeat the normalized change
        /// </summary>
        [Test]
        public void Repeat_WhiteSpaceChange()
        {
            Create(ModeArgument.NewInsertWithCount(2), "blue\t\t    dog");
            _buffer.LocalSettings.TabStop = 4;
            _buffer.LocalSettings.ExpandTab = false;
            _textView.MoveCaretTo(10);
            _textBuffer.Replace(new Span(6, 4), "\t\t");
            _textView.MoveCaretTo(8);
            Assert.AreEqual("blue\t\t\t\tdog", _textBuffer.GetLine(0).GetText());
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual("blue\t\t\t\t\tdog", _textBuffer.GetLine(0).GetText());
        }

        /// <summary>
        /// Ensure that multi-line changes are properly recorded and repeated in the ITextBuffer
        /// </summary>
        [Test]
        public void Repeat_MultilineChange()
        {
            Create("cat", "dog");
            _buffer.LocalSettings.TabStop = 4;
            _buffer.LocalSettings.ExpandTab = false;
            _buffer.Process("if (condition)", enter: true);
            _buffer.Process("\t");
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual("if (condition)", _textBuffer.GetLine(0).GetText());
            Assert.AreEqual("\tcat", _textBuffer.GetLine(1).GetText());
            _textView.MoveCaretToLine(2);
            _buffer.Process(".");
            Assert.AreEqual("if (condition)", _textBuffer.GetLine(2).GetText());
            Assert.AreEqual("\tdog", _textBuffer.GetLine(3).GetText());
        }

        /// <summary>
        /// Verify that we can repeat the DeleteAllIndent command.  Make sure that the command repeats
        /// and not the literal change of the text
        /// </summary>
        [Test]
        public void Repeat_DeleteAllIndent()
        {
            Create("     hello", "          world");
            _buffer.Process("0");
            _buffer.Process(KeyInputUtil.CharWithControlToKeyInput('d'));
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual("hello", _textView.GetLine(0).GetText());
            _textView.MoveCaretToLine(1);
            _buffer.Process(".");
            Assert.AreEqual("world", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Make sure that the tab operation can be properly repeated
        /// </summary>
        [Test]
        public void Repeat_InsertTab()
        {
            Create("cat", "dog");
            _buffer.LocalSettings.ExpandTab = false;
            _buffer.Process(VimKey.Tab);
            _buffer.Process(VimKey.Escape);
            _textView.MoveCaretToLine(1);
            _buffer.Process('.');
            Assert.AreEqual("\tdog", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Make sure that the insert tab repeats as the insert tab command and not as the 
        /// repeat of a text change.  This can be verified by altering the settings between the initial
        /// insert and the repeat
        /// </summary>
        [Test]
        public void Repeat_InsertTab_ChangedSettings()
        {
            Create("cat", "dog");
            _buffer.LocalSettings.ExpandTab = false;
            _buffer.Process(VimKey.Tab);
            _buffer.Process(VimKey.Escape);
            _textView.MoveCaretToLine(1);
            _buffer.LocalSettings.ExpandTab = true;
            _buffer.LocalSettings.TabStop = 2;
            _buffer.Process('.');
            Assert.AreEqual("  dog", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Make sure that the insert tab command when linked with before and after text changes is treated
        /// as a separate command and not straight text.  This can be verified by changing the tab insertion
        /// settings between the initial insert and the repeat
        /// </summary>
        [Test]
        public void Repeat_InsertTab_CombinedWithText()
        {
            Create("", "");
            _buffer.LocalSettings.ExpandTab = false;
            _buffer.Process("cat\tdog");
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual("cat\tdog", _textView.GetLine(0).GetText());
            _buffer.LocalSettings.ExpandTab = true;
            _buffer.LocalSettings.TabStop = 1;
            _textView.MoveCaretToLine(1);
            _buffer.Process('.');
            Assert.AreEqual("cat dog", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Test the special case of repeating an insert mode action which doesn't actually edit any
        /// items.  This may seem like a trivial action, and really it is, but the behavior being right
        /// is core to us being able to correctly repeat insert mode actions
        /// </summary>
        [Test]
        public void Repeat_NoChange()
        {
            Create("cat");
            _textView.MoveCaretTo(2);
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
            _buffer.Process('.');
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure we don't accidentally link the move caret left action with a command coming
        /// from normal mode
        /// </summary>
        [Test]
        public void Repeat_NoChange_DontLinkWithNormalCommand()
        {
            Create("cat dog");
            _buffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _textView.MoveCaretTo(0);
            _buffer.Process("dwi");
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
            _textView.MoveCaretTo(1);
            _buffer.Process('.');
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// This test is mainly a regression test against the selection change logic
        /// </summary>
        [Test]
        public void SelectionChange1()
        {
            Create("foo", "bar");
            _textView.SelectAndUpdateCaret(new SnapshotSpan(_textView.GetLine(0).Start, 0));
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
        }

        /// <summary>
        /// Make sure that shift left does a round up before it shifts to the left.
        /// </summary>
        [Test]
        public void ShiftLeft_RoundUp()
        {
            Create("     hello");
            _buffer.GlobalSettings.ShiftWidth = 4;
            _buffer.Process(KeyNotationUtil.StringToKeyInput("<C-D>"));
            Assert.AreEqual("    hello", _textBuffer.GetLine(0).GetText());
        }

        /// <summary>
        /// Make sure that when the text is properly rounded to a shift width that the 
        /// shift left just deletes a shift width
        /// </summary>
        [Test]
        public void ShiftLeft_Normal()
        {
            Create("        hello");
            _buffer.GlobalSettings.ShiftWidth = 4;
            _buffer.Process(KeyNotationUtil.StringToKeyInput("<C-D>"));
            Assert.AreEqual("    hello", _textBuffer.GetLine(0).GetText());
        }

        /// <summary>
        /// Simple word completion action which accepts the first match
        /// </summary>
        [Test]
        public void WordCompletion_Simple()
        {
            Create("c dog", "cat");
            _textView.MoveCaretTo(1);
            _buffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
            Assert.AreEqual("cat dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Simulate choosing the second possibility in the completion list
        /// </summary>
        [Test]
        public void WordCompletion_ChooseNext()
        {
            Create("c dog", "cat copter");
            _textView.MoveCaretTo(1);
            _buffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
            _buffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
            Assert.AreEqual("copter dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Typing a char while the completion list is up should cancel it out and 
        /// cause the char to be added to the IVimBuffer
        /// </summary>
        [Test]
        public void WordCompletion_TypeAfter()
        {
            Create("c dog", "cat");
            _textView.MoveCaretTo(1);
            _buffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
            _buffer.Process('s');
            Assert.AreEqual("cats dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Esacpe should cancel both word completion and insert mode.  It's just
        /// like normal intellisense in that respect
        /// </summary>
        [Test]
        public void WordCompletion_Escape()
        {
            Create("c dog", "cat");
            _textView.MoveCaretTo(1);
            _buffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
            _buffer.Process(KeyNotationUtil.StringToKeyInput("<Esc>"));
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// When there are no matches then no active IWordCompletion should be created and 
        /// it should continue in insert mode
        /// </summary>
        [Test]
        public void WordCompletion_NoMatches()
        {
            Create("c dog");
            _textView.MoveCaretTo(1);
            _buffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
            Assert.AreEqual("c dog", _textView.GetLine(0).GetText());
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
            Assert.IsTrue(_buffer.InsertMode.ActiveWordCompletionSession.IsNone());
        }
    }
}
