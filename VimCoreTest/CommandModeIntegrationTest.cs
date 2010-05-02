using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;
using Moq;

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
        private FakeVimHost host;

        public void CreateBuffer(params string[] lines)
        {
            var tuple = Utils.EditorUtil.CreateViewAndOperations(lines);
            view = tuple.Item1;
            host = new FakeVimHost();
            var service = Utils.EditorUtil.FactoryService;
            buffer = service.vim.CreateBuffer(view);
            host = (FakeVimHost)service.vim.VimHost;
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
            buffer.Process(InputUtil.VimKeyToKeyInput(VimKey.EnterKey));
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
            buffer.Process(InputUtil.VimKeyToKeyInput(VimKey.EscapeKey));
            Assert.AreEqual(ModeKind.Normal, buffer.ModeKind);
        }

        [Test]
        public void JumpLine1()
        {
            buffer.ProcessAsString(":0");
            buffer.Process(InputUtil.VimKeyToKeyInput(VimKey.EnterKey));
            Assert.AreEqual(0, view.Caret.Position.BufferPosition.Position);
            buffer.ProcessAsString(":1");
            buffer.Process(InputUtil.VimKeyToKeyInput(VimKey.EnterKey));
            Assert.AreEqual(0, view.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// Non-first line
        /// </summary>
        [Test]
        public void JumpLine2()
        {
            buffer.ProcessAsString(":2");
            buffer.Process(InputUtil.VimKeyToKeyInput(VimKey.EnterKey));
            Assert.AreEqual(view.TextSnapshot.GetLineFromLineNumber(1).Start, view.Caret.Position.BufferPosition);
        }

        [Test]
        public void JumpLineLast()
        {
            buffer.ProcessAsString(":$");
            buffer.Process(InputUtil.VimKeyToKeyInput(VimKey.EnterKey));
            var tss = view.TextSnapshot;
            var last = tss.GetLineFromLineNumber(tss.LineCount - 1);
            Assert.AreEqual(last.EndIncludingLineBreak, view.Caret.Position.BufferPosition);
        }
    }
}
