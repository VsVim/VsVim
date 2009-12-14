using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using Vim.Modes.Command;
using Microsoft.VisualStudio.Text.Editor;
using VimCoreTest.Utils;
using Microsoft.VisualStudio.Text;
using System.Windows.Input;

namespace VimCoreTest
{
    [TestFixture,RequiresSTA]
    public class CommandModeTest
    {
        private IWpfTextView _view;
        private IVimBufferData _bufferData;
        private CommandMode _modeRaw;
        private IMode _mode;
        private FakeVimHost _host;
        private IRegisterMap _map;

        public void Create(params string[] lines)
        {
            _view = Utils.EditorUtil.CreateView(lines);
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _map = new RegisterMap();
            _host = new FakeVimHost();
            _bufferData = MockObjectFactory.CreateVimBufferData(
                _view,
                "test",
                _host,
                MockObjectFactory.CreateVimData(_map).Object);
            _modeRaw = new Vim.Modes.Command.CommandMode(_bufferData);
            _mode = _modeRaw;
            _mode.OnEnter();
        }

        private void ProcessWithEnter(string input)
        {
            _mode.Process(input);
            _mode.Process(InputUtil.KeyToKeyInput(Key.Enter));
        }

        [Test, Description("Entering command mode should update the status")]
        public void StatusOnColon1()
        {
            Create(String.Empty);
            _mode.OnEnter();
            Assert.AreEqual(":", _host.Status);
        }

        [Test, Description("When leaving command mode we should not clear the status because it will remove error messages")]
        public void StatusOnLeave()
        {
            Create(String.Empty);
            _host.Status = "foo";
            _mode.OnLeave();
            Assert.AreEqual("foo", _host.Status);
        }

        [Test]
        public void StatusOnProcess()
        {
            Create("foo", "bar");
            _host.Status = "foo";
            _mode.Process("1");
            _mode.Process(InputUtil.KeyToKeyInput(Key.Enter));
            Assert.AreEqual(String.Empty, _host.Status);
        }

        [Test]
        public void Jump1()
        {
            Create("foo", "bar", "baz");
            ProcessWithEnter("$");
            var caretPoint = _view.Caret.Position.BufferPosition;
            var tss = caretPoint.Snapshot;
            var last = tss.GetLineFromLineNumber(tss.LineCount - 1);
            Assert.AreEqual(last.Start, caretPoint);
        }

        [Test]
        public void Jump2()
        {
            Create("foo", "bar");
            ProcessWithEnter("2");
            var caret = _view.Caret.Position.BufferPosition;
            Assert.AreEqual(1, caret.GetContainingLine().LineNumber);
            Assert.AreEqual(caret, caret.GetContainingLine().Start);
        }

        [Test]
        public void Jump3()
        {
            Create("foo");
            ProcessWithEnter("400");
            Assert.IsTrue(!String.IsNullOrEmpty(_host.Status));
        }

        [Test]
        public void Yank1()
        {
            Create("foo", "bar");

            IRegisterMap map = new RegisterMap();
            ProcessWithEnter("y");
            Assert.AreEqual("foo" + Environment.NewLine, map.DefaultRegister.Value.Value);
        }

        [Test]
        public void Yank2()
        {
            Create("foo", "bar", "baz");
            ProcessWithEnter("1,2y");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            Assert.AreEqual(span.GetText(), _map.DefaultRegister.Value.Value);
        }

        [Test]
        public void Yank3()
        {
            Create("foo", "bar");
            ProcessWithEnter("y c");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(line.ExtentIncludingLineBreak.GetText(), _map.GetRegister('c').Value.Value);
        }

        [Test]
        public void Yank4()
        {
            Create("foo", "bar");
            ProcessWithEnter("y 2");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            Assert.AreEqual(span.GetText(), _map.DefaultRegister.Value.Value);
        }

        [Test]
        public void Put1()
        {
            Create("foo", "bar");
            _map.DefaultRegister.UpdateValue("hey");
            ProcessWithEnter("put");
            Assert.AreEqual("hey", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void Put2()
        {
            Create("foo", "bar");
            _map.DefaultRegister.UpdateValue("hey");
            ProcessWithEnter("2put!");
            Assert.AreEqual("hey", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
        }
    }
}
