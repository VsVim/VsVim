using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.Test
{
    /// <summary>
    /// Summary description for CommandModeTest
    /// </summary>
    [TestFixture]
    public class CommandModeIntegrationTest
    {
        private IVimBuffer _buffer;
        private IWpfTextView _textView;
        private MockVimHost _host;

        public void Create(params string[] lines)
        {
            var tuple = EditorUtil.CreateViewAndOperations(lines);
            _textView = tuple.Item1;
            _host = new MockVimHost();

            var service = EditorUtil.FactoryService;
            _buffer = service.vim.CreateBuffer(_textView);
            _host = (MockVimHost)service.vim.VimHost;
        }

        [Test]
        public void OpenFile1()
        {
            Create("");
            _buffer.Process(":e foo.cpp");
            _buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Enter));
            Assert.AreEqual("foo.cpp", _host.LastFileOpen);
        }

        [Test]
        public void SwitchTo()
        {
            Create("");
            _buffer.Process(":");
            Assert.AreEqual(ModeKind.Command, _buffer.ModeKind);
        }

        [Test]
        public void SwitchOut()
        {
            Create("");
            _buffer.Process(":e foo");
            _buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Escape));
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
        }

        [Test]
        public void SwitchOutFromBackspace()
        {
            Create("");
            _buffer.Process(":");
            _buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Back));
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
        }

        [Test]
        public void JumpLine1()
        {
            Create("a", "b", "c", "d");
            _buffer.Process(":0");
            _buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Enter));
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.Position);
            _buffer.Process(":1");
            _buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Enter));
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// Non-first line
        /// </summary>
        [Test]
        public void JumpLine2()
        {
            Create("a", "b", "c", "d");
            _buffer.Process(":2");
            _buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Enter));
            Assert.AreEqual(_textView.TextSnapshot.GetLineFromLineNumber(1).Start, _textView.Caret.Position.BufferPosition);
        }

        [Test]
        public void JumpLineLast()
        {
            _buffer.Process(":$");
            _buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Enter));
            var tss = _textView.TextSnapshot;
            var last = tss.GetLineFromLineNumber(tss.LineCount - 1);
            Assert.AreEqual(last.EndIncludingLineBreak, _textView.Caret.Position.BufferPosition);
        }

    }
}
