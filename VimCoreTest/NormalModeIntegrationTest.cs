using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.Test
{
    [TestFixture]
    public class NormalModeIntegrationTest
    {
        private IVimBuffer _buffer;
        private IWpfTextView _textView;

        public void CreateBuffer(params string[] lines)
        {
            var tuple = EditorUtil.CreateViewAndOperations(lines);
            _textView = tuple.Item1;
            var service = EditorUtil.FactoryService;
            _buffer = service.vim.CreateBuffer(_textView);
        }

        [Test]
        public void dd_OnLastLine()
        {
            CreateBuffer("foo", "bar");
            _textView.MoveCaretTo(_textView.GetLine(1).Start);
            _buffer.ProcessAsString("dd");
            Assert.AreEqual("foo", _textView.TextSnapshot.GetText());
            Assert.AreEqual(1, _textView.TextSnapshot.LineCount);
        }

        [Test]
        public void dot_Repeated1()
        {
            CreateBuffer("the fox chased the bird");
            _buffer.ProcessInputAsString("dw");
            Assert.AreEqual("fox chased the bird", _textView.TextSnapshot.GetText());
            _buffer.ProcessInputAsString(".");
            Assert.AreEqual("chased the bird", _textView.TextSnapshot.GetText());
            _buffer.ProcessInputAsString(".");
            Assert.AreEqual("the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void dot_LinkedTextChange1()
        {
            CreateBuffer("the fox chased the bird");
            _buffer.ProcessInputAsString("cw");
            _buffer.TextBuffer.Insert(0, "hey ");
            _buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Escape));
            _textView.MoveCaretTo(4);
            _buffer.Process(KeyInputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hey fox chased the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void dot_LinkedTextChange2()
        {
            CreateBuffer("the fox chased the bird");
            _buffer.ProcessInputAsString("cw");
            _buffer.TextBuffer.Insert(0, "hey");
            _buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Escape));
            _textView.MoveCaretTo(4);
            _buffer.Process(KeyInputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hey chased the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void dot_LinkedTextChange3()
        {
            CreateBuffer("the fox chased the bird");
            _buffer.ProcessInputAsString("cw");
            _buffer.TextBuffer.Insert(0, "hey");
            _buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Escape));
            _textView.MoveCaretTo(4);
            _buffer.Process(KeyInputUtil.CharToKeyInput('.'));
            _buffer.Process(KeyInputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hehey chased the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        [Description("See issue 288")]
        public void dj_1()
        {
            CreateBuffer("abc", "def", "ghi", "jkl");
            _buffer.ProcessInputAsString("dj");
            Assert.AreEqual("ghi", _textView.GetLine(0).GetText());
            Assert.AreEqual("jkl", _textView.GetLine(1).GetText());
        }

        [Test]
        [Description("[[ motion should put the caret on the target character")]
        public void SectionMotion1()
        {
            CreateBuffer("hello", "{world");
            _buffer.ProcessInputAsString("]]");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        [Description("[[ motion should put the caret on the target character")]
        public void SectionMotion2()
        {
            CreateBuffer("hello", "\fworld");
            _buffer.ProcessInputAsString("]]");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void SectionMotion3()
        {
            CreateBuffer("foo", "{", "bar");
            _textView.MoveCaretTo(_textView.GetLine(2).End);
            _buffer.ProcessInputAsString("[[");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void SectionMotion4()
        {
            CreateBuffer("foo", "{", "bar", "baz");
            _textView.MoveCaretTo(_textView.GetLine(3).End);
            _buffer.ProcessInputAsString("[[");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void SectionMotion5()
        {
            CreateBuffer("foo", "{", "bar", "baz", "jazz");
            _textView.MoveCaretTo(_textView.GetLine(4).Start);
            _buffer.ProcessInputAsString("[[");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }
    }
}
