using System;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Modes.Command;
using Vim.UnitTest.Mock;
using Vim.UnitTest;

namespace VimCore.Test
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

        [SetUp]
        public void SetUp()
        {
            _caret = MockObjectFactory.CreateCaret();
            _caret.SetupProperty(x => x.IsHidden);
            _view = new Mock<IWpfTextView>(MockBehavior.Strict);
            _view.SetupGet(x => x.Caret).Returns(_caret.Object);
            
            _bufferData = MockObjectFactory.CreateVimBuffer(view:_view.Object);
            _processor = new Mock<ICommandProcessor>(MockBehavior.Strict);
            _modeRaw = new CommandMode(_bufferData.Object, _processor.Object);
            _mode = _modeRaw;
        }

        private void ProcessWithEnter(string input)
        {
            _mode.Process(input);
            _mode.Process(InputUtil.VimKeyToKeyInput(VimKey.Enter));
        }

        [Test, Description("Entering command mode should update the status")]
        public void StatusOnColon1()
        {
            _mode.OnEnter(ModeArgument.None);
            Assert.AreEqual("", _mode.Command);
        }

        [Test, Description("When leaving command mode we should not clear the status because it will remove error messages")]
        public void StatusOnLeave()
        {
            _mode.OnLeave();
            Assert.AreEqual("", _mode.Command);
        }

        [Test]
        public void StatusOnProcess()
        {
            _processor.Setup(x => x.RunCommand(MatchUtil.CreateForCharList("1"))).Verifiable();
            _mode.Process("1");
            _mode.Process(InputUtil.VimKeyToKeyInput(VimKey.Enter));
            _processor.Verify();
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
            _mode.Process(InputUtil.VimKeyToKeyInput(VimKey.Back));
            Assert.AreEqual("fo", _modeRaw.Command);
        }

        [Test]
        public void Input4()
        {
            _mode.Process("foo");
            _mode.Process(InputUtil.VimKeyToKeyInput(VimKey.Escape));
            Assert.AreEqual(string.Empty, _modeRaw.Command);
        }

        [Test, Description("Delete past the start of the command string")]
        public void Input5()
        {
            _mode.Process('c');
            _mode.Process(InputUtil.VimKeyToKeyInput(VimKey.Back));
            _mode.Process(InputUtil.VimKeyToKeyInput(VimKey.Back));
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
            _mode.OnEnter(ModeArgument.None);
            Assert.IsTrue(_view.Object.Caret.IsHidden);
        }

        [Test]
        public void Cursor2()
        {
            _mode.OnLeave();
            Assert.IsFalse(_view.Object.Caret.IsHidden);
        }

        [Test]
        public void OnEnter1()
        {
            _mode.OnEnter(ModeArgument.None);
            Assert.AreEqual(String.Empty, _modeRaw.Command);
        }

        [Test]
        public void OnEnter2()
        {
            _mode.OnEnter(ModeArgument.FromVisual);
            Assert.AreEqual("'<,'>", _modeRaw.Command);
        }
    }
}
