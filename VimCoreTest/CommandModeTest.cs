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
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using Microsoft.FSharp.Collections;

namespace VimCoreTest
{
    [TestFixture, RequiresSTA]
    public class CommandModeTest
    {
        private Mock<ITextCaret> _caret;
        private Mock<IWpfTextView> _view;
        private Mock<IVimBuffer> _bufferData;
        private Mock<ICommandProcessor> _processor;
        private CommandMode _modeRaw;
        private ICommandMode _mode;
        private FakeVimHost _host;

        [SetUp]
        public void SetUp()
        {
            _caret = MockObjectFactory.CreateCaret();
            _caret.SetupProperty(x => x.IsHidden);
            _view = new Mock<IWpfTextView>(MockBehavior.Strict);
            _view.SetupGet(x => x.Caret).Returns(_caret.Object);
            
            _host = new FakeVimHost();
            _bufferData = MockObjectFactory.CreateVimBuffer(view:_view.Object);
            _bufferData.SetupGet(x => x.VimHost).Returns(_host);
            _processor = new Mock<ICommandProcessor>(MockBehavior.Strict);
            _modeRaw = new CommandMode(Tuple.Create(_bufferData.Object, _processor.Object));
            _mode = _modeRaw;
        }

        private void ProcessWithEnter(string input)
        {
            _mode.Process(input);
            _mode.Process(InputUtil.WellKnownKeyToKeyInput(WellKnownKey.ReturnKey));
        }

        [Test, Description("Entering command mode should update the status")]
        public void StatusOnColon1()
        {
            _mode.OnEnter();
            Assert.AreEqual(":", _host.Status);
        }

        [Test, Description("When leaving command mode we should not clear the status because it will remove error messages")]
        public void StatusOnLeave()
        {
            _host.Status = "foo";
            _mode.OnLeave();
            Assert.AreEqual("foo", _host.Status);
        }

        [Test]
        public void StatusOnProcess()
        {
            _host.Status = "foo";
            _processor.Setup(x => x.RunCommand(MatchUtil.CreateForCharList("1"))).Verifiable();
            _mode.Process("1");
            _mode.Process(InputUtil.WellKnownKeyToKeyInput(WellKnownKey.EnterKey));
            _processor.Verify();
            Assert.AreEqual(String.Empty, _host.Status);
        }

        [Test, Description("Ensure multiple commands can be processed")]
        public void DoubleCommand1()
        {
            _processor.Setup(x => x.RunCommand(MatchUtil.CreateForCharList("2"))).Verifiable();
            ProcessWithEnter("2");
            _processor.Verify();
            _processor.Setup(x => x.RunCommand(MatchUtil.CreateForCharList("3"))).Verifiable();
            ProcessWithEnter("3");
            _processor.Verify();
        }

        [Test]
        public void Input1()
        {
            _mode.Process("fo");
            Assert.AreEqual("fo", _modeRaw.Command);
        }

        [Test]
        public void Input2()
        {
            _processor.Setup(x => x.RunCommand(MatchUtil.CreateForCharList("foo"))).Verifiable();
            ProcessWithEnter("foo");
            _processor.Verify();
            Assert.AreEqual(String.Empty, _modeRaw.Command);
        }

        [Test]
        public void Input3()
        {
            _mode.Process("foo");
            _mode.Process(InputUtil.WellKnownKeyToKeyInput(WellKnownKey.BackKey));
            Assert.AreEqual("fo", _modeRaw.Command);
        }

        [Test]
        public void Input4()
        {
            _mode.Process("foo");
            _mode.Process(InputUtil.WellKnownKeyToKeyInput(WellKnownKey.EscapeKey));
            Assert.AreEqual(string.Empty, _modeRaw.Command);
        }

        [Test, Description("Delete past the start of the command string")]
        public void Input5()
        {
            _mode.Process('c');
            _mode.Process(InputUtil.WellKnownKeyToKeyInput(WellKnownKey.BackKey));
            _mode.Process(InputUtil.WellKnownKeyToKeyInput(WellKnownKey.BackKey));
            Assert.AreEqual(String.Empty, _modeRaw.Command);
        }

        [Test, Description("Upper case letter")]
        public void Input6()
        {
            _mode.Process("BACK");
            Assert.AreEqual("BACK", _modeRaw.Command);
        }

        [Test]
        public void Input7()
        {
            _mode.Process("_bar");
            Assert.AreEqual("_bar", _modeRaw.Command);
        }

        [Test]
        public void Cursor1()
        {
            _mode.OnEnter();
            Assert.IsTrue(_view.Object.Caret.IsHidden);
        }

        [Test]
        public void Cursor2()
        {
            _mode.OnLeave();
            Assert.IsFalse(_view.Object.Caret.IsHidden);
        }
    }
}
