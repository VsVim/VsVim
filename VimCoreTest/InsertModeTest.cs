using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Vim.Modes;
using Moq;

namespace VimCoreTest
{
    /// <summary>
    /// Summary description for InputMode
    /// </summary>
    [TestFixture]
    public class InsertModeTest
    {
        private Mock<IVimBuffer> _data;
        private Vim.Modes.Insert.InsertMode _modeRaw;
        private IMode _mode;
        private ITextBuffer _buffer;
        private IWpfTextView _view;
        private Mock<ICommonOperations> _operations;
        private Mock<IVimHost> _host;
        private Mock<ICompletionWindowBroker> _broker;

        public void CreateBuffer(params string[] lines)
        {
            _view = Utils.EditorUtil.CreateView(lines);
            _host = new Mock<IVimHost>(MockBehavior.Strict);
            _buffer = _view.TextBuffer;
            _data = Utils.MockObjectFactory.CreateVimBuffer(
                _view,
                vim: Utils.MockObjectFactory.CreateVim(host : _host.Object).Object);
            _operations = new Mock<ICommonOperations>(MockBehavior.Strict);
            _broker = new Mock<ICompletionWindowBroker>(MockBehavior.Strict);
            _modeRaw = new Vim.Modes.Insert.InsertMode(Tuple.Create<IVimBuffer,ICommonOperations,ICompletionWindowBroker>(_data.Object,_operations.Object,_broker.Object));
            _mode = _modeRaw;
        }

        [SetUp]
        public void Init()
        {
            CreateBuffer("foo bar baz", "boy kick ball");
        }

        [Test, Description("Must process escape")]
        public void CanProcess1()
        {
            Assert.IsTrue(_mode.CanProcess(WellKnownKey.EscapeKey));
        }

        [Test, Description("Do not processing anything other than Escape")]
        public void CanProcess2()
        {
            Assert.IsFalse(_mode.CanProcess(WellKnownKey.EnterKey));
            Assert.IsFalse(_mode.CanProcess(InputUtil.CharToKeyInput('c')));
        }

        [Test, Description("Escape should exit if we are not in the middle of a completion")]
        public void Escape1()
        {
            _broker
                .SetupGet(x => x.IsCompletionWindowActive)
                .Returns(false)
                .Verifiable();
            var res = _mode.Process(WellKnownKey.EscapeKey);
            Assert.IsTrue(res.IsSwitchMode);
            _broker.Verify();
        }

        [Test, Description("Escape should dismiss completion if it is active but not exit insert mode")]
        public void Escape2()
        {
            _broker
                .SetupGet(x => x.IsCompletionWindowActive)
                .Returns(true)
                .Verifiable();
            _broker
                .Setup(x => x.DismissCompletionWindow())
                .Verifiable();
            var res = _mode.Process(WellKnownKey.EscapeKey);
            Assert.IsTrue(res.IsProcessed);
        }

        [Test]
        public void ShiftLeft1()
        {
            CreateBuffer("    foo");
            _operations
                .Setup(x => x.ShiftLeft(_view.TextSnapshot.GetLineFromLineNumber(0).Extent, 4))
                .Returns<ITextSnapshot>(null)
                .Verifiable(); ;
            var res = _mode.Process(new KeyInput('d', KeyModifiers.Control));
            Assert.IsTrue(res.IsProcessed);
            _operations.Verify();
        }

        [Test]
        public void Cursor1()
        {
            CreateBuffer("faa bar");
            _view.MoveCaretTo(1);
            _mode.OnLeave();
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Don't crash at the start of the file")]
        public void Cursor2()
        {
            CreateBuffer("faa bar");
            _view.MoveCaretTo(0);
            _mode.OnLeave();
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Don't move past the start of the line")]
        public void Cursor3()
        {
            CreateBuffer("foo", "bar");
            var point = _view.GetLine(1).Start;
            _view.Caret.MoveTo(point);
            _mode.OnLeave();
            Assert.AreEqual(point, _view.Caret.Position.BufferPosition);
        }

    }
}
