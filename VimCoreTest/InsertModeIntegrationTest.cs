using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    /// <summary>
    /// Used to test the integrated behavior if Insert Mode 
    /// </summary>
    [TestFixture]
    public sealed class InsertModeIntegrationTest
    {
        private IVimBuffer _buffer;
        private IWpfTextView _textView;
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            var tuple = EditorUtil.CreateTextViewAndEditorOperations(lines);
            _textView = tuple.Item1;
            _textBuffer = _textView.TextBuffer;
            var service = EditorUtil.FactoryService;
            _buffer = service.Vim.CreateBuffer(_textView);
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

        [Test]
        [Description("Make sure we don't access the ITextView on the way down")]
        public void CloseInInsertMode()
        {
            Create("foo", "bar");
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _textView.Close();
        }

        /// <summary>
        /// This test is mainly a regression test against the selection change logic
        /// </summary>
        [Test]
        [Description("Make sure a minor selection change doesn't move us into Normal mode")]
        public void SelectionChange1()
        {
            Create("foo", "bar");
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _textView.SelectAndUpdateCaret(new SnapshotSpan(_textView.GetLine(0).Start, 0));
            Assert.AreEqual(ModeKind.Insert, _buffer.ModeKind);
        }

        /// <summary>
        /// Ensure when the mode is entered with a count that the escape will cause the 
        /// text to be repeated
        /// </summary>
        [Test]
        public void Repeat_Insert()
        {
            Create("the cat");
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.NewInsertWithCount(2));
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
            Create("doggie");
            _textView.MoveCaretTo(1);
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.NewInsertWithCount(2));
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
            Create("blue\t\t    dog");
            _buffer.LocalSettings.TabStop = 4;
            _buffer.LocalSettings.ExpandTab = false;
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.NewInsertWithCount(2));
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
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
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
        /// Ensure we can use a double keystroke to escape
        /// </summary>
        [Test]
        public void KeyRemap_TwoKeysToEscape()
        {
            Create("hello");
            _buffer.Vim.KeyMap.MapWithNoRemap("jj", "<Esc>", KeyRemapMode.Insert);
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.NewInsertWithCount(2));
            _buffer.Process("jj");
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
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
            _buffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _buffer.Process("j");
            Assert.AreEqual("", _textBuffer.GetLine(0).GetText());
            _buffer.Process("a");
            Assert.AreEqual("ja", _textBuffer.GetLine(0).GetText());
        }
    }
}
