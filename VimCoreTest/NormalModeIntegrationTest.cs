using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using VimCore.Test.Utils;

namespace VimCore.Test
{
    [TestFixture]
    public class NormalModeIntegrationTest
    {
        private IVimBuffer _buffer;
        private IWpfTextView _textView;

        public void CreateBuffer(params string[] lines)
        {
            var tuple = Utils.EditorUtil.CreateViewAndOperations(lines);
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
            _buffer.Process(InputUtil.VimKeyToKeyInput(VimKey.EscapeKey));
            _textView.MoveCaretTo(4);
            _buffer.Process(InputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hey fox chased the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void dot_LinkedTextChange2()
        {
            CreateBuffer("the fox chased the bird");
            _buffer.ProcessInputAsString("cw");
            _buffer.TextBuffer.Insert(0, "hey");
            _buffer.Process(InputUtil.VimKeyToKeyInput(VimKey.EscapeKey));
            _textView.MoveCaretTo(4);
            _buffer.Process(InputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hey chased the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void dot_LinkedTextChange3()
        {
            CreateBuffer("the fox chased the bird");
            _buffer.ProcessInputAsString("cw");
            _buffer.TextBuffer.Insert(0, "hey");
            _buffer.Process(InputUtil.VimKeyToKeyInput(VimKey.EscapeKey));
            _textView.MoveCaretTo(4);
            _buffer.Process(InputUtil.CharToKeyInput('.'));
            _buffer.Process(InputUtil.CharToKeyInput('.'));
            Assert.AreEqual("hey hehey chased the bird", _textView.TextSnapshot.GetText());
        }
    }
}
