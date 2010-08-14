using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.UnitTest.Mock;
using Vim.UnitTest;

namespace VimCore.Test
{
    /// <summary>
    /// Summary description for CommandModeTest
    /// </summary>
    [TestFixture]
    public class CommandModeIntegrationTest
    {
        private IVimBuffer buffer;
        private IWpfTextView view;
        private MockVimHost host;

        public void CreateBuffer(params string[] lines)
        {
            var tuple = EditorUtil.CreateViewAndOperations(lines);
            view = tuple.Item1;
            host = new MockVimHost();
            var service = EditorUtil.FactoryService;
            buffer = service.vim.CreateBuffer(view);
            host = (MockVimHost)service.vim.VimHost;
        }

        [SetUp]
        public void Init()
        {
            CreateBuffer(
                "foo bar baz", 
                "boy kick ball",
                "again",
                "here we are"
                );
        }

        [Test]
        public void OpenFile1()
        {
            buffer.ProcessAsString(":e foo.cpp");
            buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Enter));
            Assert.AreEqual("foo.cpp", host.LastFileOpen);
        }

        [Test]
        public void SwitchTo()
        {
            buffer.ProcessAsString(":");
            Assert.AreEqual(ModeKind.Command, buffer.ModeKind);
        }

        [Test]
        public void SwitchOut()
        {
            buffer.ProcessAsString(":e foo");
            buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Escape));
            Assert.AreEqual(ModeKind.Normal, buffer.ModeKind);
        }

        [Test]
        public void SwitchOutFromBackspace()
        {
            buffer.ProcessAsString(":");
            buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Back));
            Assert.AreEqual(ModeKind.Normal, buffer.ModeKind);
        }

        [Test]
        public void JumpLine1()
        {
            buffer.ProcessAsString(":0");
            buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Enter));
            Assert.AreEqual(0, view.Caret.Position.BufferPosition.Position);
            buffer.ProcessAsString(":1");
            buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Enter));
            Assert.AreEqual(0, view.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// Non-first line
        /// </summary>
        [Test]
        public void JumpLine2()
        {
            buffer.ProcessAsString(":2");
            buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Enter));
            Assert.AreEqual(view.TextSnapshot.GetLineFromLineNumber(1).Start, view.Caret.Position.BufferPosition);
        }

        [Test]
        public void JumpLineLast()
        {
            buffer.ProcessAsString(":$");
            buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Enter));
            var tss = view.TextSnapshot;
            var last = tss.GetLineFromLineNumber(tss.LineCount - 1);
            Assert.AreEqual(last.EndIncludingLineBreak, view.Caret.Position.BufferPosition);
        }
    }
}
