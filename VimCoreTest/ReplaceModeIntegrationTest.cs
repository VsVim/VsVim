using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class ReplaceModeIntegrationTest
    {
        private IVimBuffer _buffer;
        private IWpfTextView _textView;

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateTextView(lines);
            _buffer = EditorUtil.FactoryService.Vim.CreateBuffer(_textView);
        }

        /// <summary>
        /// Typing forward in replace mode should overwrite 
        /// </summary>
        [Test]
        public void TypeForwardShouldReplace()
        {
            Create("hello world");
            _buffer.SwitchMode(ModeKind.Replace, ModeArgument.None);
            _buffer.Process("again");
            Assert.AreEqual("again world", _textView.GetLine(0).GetText());
            Assert.AreEqual(5, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Typing past the end of the line should extend it
        /// </summary>
        [Test]
        public void TypePastEndOfLine()
        {
            Create("cat", "dog");
            _buffer.SwitchMode(ModeKind.Replace, ModeArgument.None);
            _buffer.Process("big tree");
            Assert.AreEqual("big tree", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// Typing a backspace character should undo the previous edit instead of 
        /// actually doing a backspace
        /// </summary>
        [Test]
        public void BackspaceShouldUndo()
        {
            Create("cat");
            _buffer.SwitchMode(ModeKind.Replace, ModeArgument.None);
            _buffer.Process("dog");
            _buffer.Process(VimKey.Back);
            _buffer.Process(VimKey.Back);
            _buffer.Process(VimKey.Back);
            Assert.AreEqual("cat", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// The enter key cannot be backspaced over.  It's a block in the edit list
        /// </summary>
        [Test]
        public void EnterCannotBeUndone()
        {
            Create("a");
            _buffer.SwitchMode(ModeKind.Replace, ModeArgument.None);
            _buffer.Process("b");
            _buffer.Process(VimKey.Enter);
            _buffer.Process(VimKey.Back);
            Assert.AreEqual("b", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.TextSnapshot.LineCount);
            Assert.AreEqual("", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// A one time normal mode command cannot be undone with the back key
        /// </summary>
        [Test]
        public void NormalCommandCannotBeUndone()
        {
            Create("cat");
            _buffer.GetRegister(RegisterName.Unnamed).UpdateValue("s");
            _buffer.SwitchMode(ModeKind.Replace, ModeArgument.None);
            _buffer.Process("co");
            _buffer.Process(KeyInputUtil.VimKeyAndModifiersToKeyInput(VimKey.LowerO, KeyModifiers.Control));
            _buffer.Process("P");
            Assert.AreEqual("cost", _textView.GetLine(0).GetText());
            _buffer.Process(VimKey.Back);
            Assert.AreEqual("cost", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Replace mode should respect the insert count 
        /// </summary>
        [Test]
        public void Repeat_InsertText()
        {
            Create("dog");
            _buffer.SwitchMode(ModeKind.Replace, ModeArgument.NewInsertWithCount(2));
            _buffer.Process("cat");
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual("catcat", _textView.GetLine(0).GetText());
            Assert.AreEqual(5, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// When repeating the insert it should write over the remaining text vs. inserting
        /// </summary>
        [Test]
        public void Repeat_InsertOver()
        {
            Create("fish tree");
            _buffer.SwitchMode(ModeKind.Replace, ModeArgument.NewInsertWithCount(2));
            _buffer.Process("cat");
            _buffer.Process(VimKey.Escape);
            Assert.AreEqual("catcatree", _textView.GetLine(0).GetText());
            Assert.AreEqual(5, _textView.GetCaretPoint().Position);
        }
    }
}
