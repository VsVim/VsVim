using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
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

        private void RunCommand(string command)
        {
            _buffer.Process(':');
            _buffer.Process(command);
            _buffer.Process(VimKey.Enter);
        }

        [Test]
        public void OpenFile1()
        {
            Create("");
            RunCommand("e foo.cpp");
            Assert.AreEqual("foo.cpp", _host.LastFileOpen);
        }

        [Test]
        public void SwitchTo()
        {
            Create("");
            _buffer.Process(':');
            Assert.AreEqual(ModeKind.Command, _buffer.ModeKind);
        }

        [Test]
        public void SwitchOut()
        {
            Create("");
            RunCommand("e foo");
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
        }

        [Test]
        public void SwitchOutFromBackspace()
        {
            Create("");
            _buffer.Process(':');
            _buffer.Process(VimKey.Back);
            Assert.AreEqual(ModeKind.Normal, _buffer.ModeKind);
        }

        [Test]
        public void JumpLine1()
        {
            Create("a", "b", "c", "d");
            RunCommand("0");
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.Position);
            RunCommand("1");
            Assert.AreEqual(0, _textView.Caret.Position.BufferPosition.Position);
        }

        /// <summary>
        /// Non-first line
        /// </summary>
        [Test]
        public void JumpLine2()
        {
            Create("a", "b", "c", "d");
            RunCommand("2");
            Assert.AreEqual(_textView.TextSnapshot.GetLineFromLineNumber(1).Start, _textView.Caret.Position.BufferPosition);
        }

        [Test]
        public void JumpLineLast()
        {
            RunCommand("$");
            var tss = _textView.TextSnapshot;
            var last = tss.GetLineFromLineNumber(tss.LineCount - 1);
            Assert.AreEqual(last.EndIncludingLineBreak, _textView.Caret.Position.BufferPosition);
        }

        [Test]
        [Description("Suppress errors shouldn't print anything")]
        public void Substitute1()
        {
            Create("cat", "dog");
            var sawError = false;
            _buffer.ErrorMessage += delegate { sawError = true; };
            RunCommand("s/z/o/e");
            Assert.IsFalse(sawError);
        }

        [Test]
        [Description("Simple search and replace")]
        public void Substitute2()
        {
            Create("cat bat", "dag");
            RunCommand("s/a/o/g 2");
            Assert.AreEqual("cot bot", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
        }

        [Test]
        [Description("Repeat of the last search with a new flag")]
        public void Substitute3()
        {
            Create("cat bat", "dag");
            _buffer.VimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "o", SubstituteFlags.None));
            RunCommand("s g 2");
            Assert.AreEqual("cot bot", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
        }

        [Test]
        [Description("Testing the print option")]
        public void Substitute4()
        {
            Create("cat bat", "dag");
            var message = String.Empty;
            _buffer.StatusMessage += (_, e) => { message = e; };
            RunCommand("s/a/b/p");
            Assert.AreEqual("cbt bat", message);
        }

        [Test]
        [Description("Testing the print option")]
        public void Substitute5()
        {
            Create("cat bat", "dag");
            List<string> list = null;
            _buffer.StatusMessageLong += (_, e) => { list = e.ToList(); };
            RunCommand("s/a/b/pg");
            Assert.AreEqual(Resources.Common_SubstituteComplete(2, 1), list[0]);
            Assert.AreEqual("cbt bbt", list[1]);
        }

        [Test]
        [Description("Testing the print number option")]
        public void Substitute6()
        {
            Create("cat bat", "dag");
            var message = String.Empty;
            _buffer.StatusMessage += (_, e) => { message = e; };
            RunCommand("s/a/b/#");
            Assert.AreEqual("  1 cbt bat", message);
        }

        [Test]
        [Description("Testing the print list option")]
        public void Substitute7()
        {
            Create("cat bat", "dag");
            var message = String.Empty;
            _buffer.StatusMessage += (_, e) => { message = e; };
            RunCommand("s/a/b/l");
            Assert.AreEqual("cbt bat$", message);
        }
    }
}
