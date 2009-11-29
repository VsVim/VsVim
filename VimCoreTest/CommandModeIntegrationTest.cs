using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VimCore;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;

namespace VimCoreTest
{
    /// <summary>
    /// Summary description for CommandModeTest
    /// </summary>
    [TestClass]
    public class CommandModeIntegrationTest
    {
        private IVimBuffer buffer;
        private IWpfTextView view;
        private FakeVimHost host;

        public void CreateBuffer(params string[] lines)
        {
            view = Utils.EditorUtil.CreateView(lines);
            host = new FakeVimHost();
            buffer = Factory.CreateVimBuffer(host, view, "test");
        }

        [TestInitialize]
        public void Init()
        {
            CreateBuffer(
                "foo bar baz", 
                "boy kick ball",
                "again",
                "here we are"
                );
        }

        [TestMethod]
        public void OpenFile1()
        {
            buffer.ProcessInputAsString(":e foo.cpp");
            buffer.ProcessKey(Key.Enter);
            Assert.AreEqual("foo.cpp", host.LastFileOpen);
        }

        [TestMethod]
        public void SwitchTo()
        {
            buffer.ProcessInputAsString(":");
            Assert.AreEqual(ModeKind.Command, buffer.ModeKind);
        }

        [TestMethod]
        public void SwitchOut()
        {
            buffer.ProcessInputAsString(":e foo");
            buffer.ProcessKey(Key.Escape);
            Assert.AreEqual(ModeKind.Normal, buffer.ModeKind);
        }

        [TestMethod]
        public void JumpLine1()
        {
            buffer.ProcessInputAsString(":0");
            buffer.ProcessKey(Key.Enter);
            Assert.AreEqual(0, view.Caret.Position.BufferPosition.Position);
            buffer.ProcessInputAsString(":1");
            buffer.ProcessKey(Key.Enter);
            Assert.AreEqual(0, view.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// Non-first line
        /// </summary>
        [TestMethod]
        public void JumpLine2()
        {
            buffer.ProcessInputAsString(":2");
            buffer.ProcessKey(Key.Enter);
            Assert.AreEqual(view.TextSnapshot.GetLineFromLineNumber(1).Start, view.Caret.Position.BufferPosition);
        }

        /// <summary>
        /// Invalid line position
        /// </summary>
        [TestMethod]
        public void JumpLine3()
        {
            buffer.ProcessInputAsString(":200");
            buffer.ProcessKey(Key.Enter);
            Assert.AreEqual("Invalid line number", host.Status);
        }

        [TestMethod]
        public void JumpLineLast()
        {
            buffer.ProcessInputAsString(":$");
            buffer.ProcessKey(Key.Enter);
            var tss = view.TextSnapshot;
            var last = tss.GetLineFromLineNumber(tss.LineCount - 1);
            Assert.AreEqual(last.Start, view.Caret.Position.BufferPosition);
        }
    }
}
