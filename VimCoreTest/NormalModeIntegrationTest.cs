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
            _buffer.Process("dd");
            Assert.AreEqual("foo", _textView.TextSnapshot.GetText());
            Assert.AreEqual(1, _textView.TextSnapshot.LineCount);
        }

        [Test]
        public void dot_Repeated1()
        {
            CreateBuffer("the fox chased the bird");
            _buffer.Process("dw");
            Assert.AreEqual("fox chased the bird", _textView.TextSnapshot.GetText());
            _buffer.Process(".");
            Assert.AreEqual("chased the bird", _textView.TextSnapshot.GetText());
            _buffer.Process(".");
            Assert.AreEqual("the bird", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void dot_LinkedTextChange1()
        {
            CreateBuffer("the fox chased the bird");
            _buffer.Process("cw");
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
            _buffer.Process("cw");
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
            _buffer.Process("cw");
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
            _buffer.Process("dj");
            Assert.AreEqual("ghi", _textView.GetLine(0).GetText());
            Assert.AreEqual("jkl", _textView.GetLine(1).GetText());
        }

        [Test]
        [Description("A d with Enter should delete the line break")]
        public void Issue317_1()
        {
            CreateBuffer("dog", "cat", "jazz", "band");
            _buffer.Process("2d");
            _buffer.Process(VimKey.Enter);
            Assert.AreEqual("band", _textView.GetLine(0).GetText());
        }

        [Test]
        [Description("Verify the contents after with a paste")]
        public void Issue317_2()
        {
            CreateBuffer("dog", "cat", "jazz", "band");
            _buffer.Process("2d");
            _buffer.Process(VimKey.Enter);
            _buffer.Process("p");
            Assert.AreEqual("band", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
            Assert.AreEqual("cat", _textView.GetLine(2).GetText());
            Assert.AreEqual("jazz", _textView.GetLine(3).GetText());
        }

        [Test]
        [Description("Plain old Enter should just move the cursor one line")]
        public void Issue317_3()
        {
            CreateBuffer("dog", "cat", "jazz", "band");
            _buffer.Process(VimKey.Enter);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }


        [Test]
        [Description("[[ motion should put the caret on the target character")]
        public void SectionMotion1()
        {
            CreateBuffer("hello", "{world");
            _buffer.Process("]]");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        [Description("[[ motion should put the caret on the target character")]
        public void SectionMotion2()
        {
            CreateBuffer("hello", "\fworld");
            _buffer.Process("]]");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void SectionMotion3()
        {
            CreateBuffer("foo", "{", "bar");
            _textView.MoveCaretTo(_textView.GetLine(2).End);
            _buffer.Process("[[");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void SectionMotion4()
        {
            CreateBuffer("foo", "{", "bar", "baz");
            _textView.MoveCaretTo(_textView.GetLine(3).End);
            _buffer.Process("[[");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void SectionMotion5()
        {
            CreateBuffer("foo", "{", "bar", "baz", "jazz");
            _textView.MoveCaretTo(_textView.GetLine(4).Start);
            _buffer.Process("[[");
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void ParagraphMotion1()
        {
            CreateBuffer("foo", "{", "bar", "baz", "jazz");
            _textView.MoveCaretTo(_textView.TextSnapshot.GetEndPoint());
            _buffer.Process("{{");
        }

        [Test]
        public void RepeatLastSearch1()
        {
            CreateBuffer("random text", "pig dog cat", "pig dog cat", "pig dog cat");
            _buffer.Process("/pig");
            _buffer.Process(VimKey.Enter);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _textView.MoveCaretTo(0);
            _buffer.Process('n');
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void RepeatLastSearch2()
        {
            CreateBuffer("random text", "pig dog cat", "pig dog cat", "pig dog cat");
            _buffer.Process("/pig");
            _buffer.Process(VimKey.Enter);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _buffer.Process('n');
            Assert.AreEqual(_textView.GetLine(2).Start, _textView.GetCaretPoint());
        }

        [Test]
        public void RepeatLastSearch3()
        {
            CreateBuffer("random text", "pig dog cat", "random text", "pig dog cat", "pig dog cat");
            _buffer.Process("/pig");
            _buffer.Process(VimKey.Enter);
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            _textView.MoveCaretTo(_textView.GetLine(2).Start);
            _buffer.Process('N');
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        [Test]
        [Description("With no virtual edit the cursor should move backwards after x")]
        public void CursorPositionWith_x_1()
        {
            CreateBuffer("test");
            _buffer.Settings.GlobalSettings.VirtualEdit = string.Empty;
            _textView.MoveCaretTo(3);
            _buffer.Process('x');
            Assert.AreEqual("tes", _textView.GetLineSpan(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        [Test]
        [Description("With virtual edit the cursor should not move and stay at the end of the line")]
        public void CursorPositionWith_x_2()
        {
            CreateBuffer("test", "bar");
            _buffer.Settings.GlobalSettings.VirtualEdit = "onemore";
            _textView.MoveCaretTo(3);
            _buffer.Process('x');
            Assert.AreEqual("tes", _textView.GetLineSpan(0).GetText());
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        [Test]
        [Description("Caret position should remain the same in the middle of a word")]
        public void CursorPositionWith_x_3()
        {
            CreateBuffer("test", "bar");
            _buffer.Settings.GlobalSettings.VirtualEdit = string.Empty;
            _textView.MoveCaretTo(1);
            _buffer.Process('x');
            Assert.AreEqual("tst", _textView.GetLineSpan(0).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }
    }
}
