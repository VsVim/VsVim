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

namespace VimCore.Test
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
        private Mock<ITextView> _textView;
        private Mock<ICommonOperations> _operations;
        private Mock<IDisplayWindowBroker> _broker;
        private Mock<IVimGlobalSettings> _globalSettings;
        private Mock<IVimLocalSettings> _localSettings;
        private Mock<IVim> _vim;

        [SetUp]
        public void SetUp()
        {
            _textView = new Mock<ITextView>(MockBehavior.Strict);
            _vim = new Mock<IVim>();
            _globalSettings = new Mock<IVimGlobalSettings>(MockBehavior.Strict);
            _localSettings = new Mock<IVimLocalSettings>(MockBehavior.Strict);
            _localSettings.SetupGet(x => x.GlobalSettings).Returns(_globalSettings.Object);
            _data = Mock.MockObjectFactory.CreateVimBuffer(
                _textView.Object,
                settings:_localSettings.Object,
                vim:_vim.Object);
            _operations = new Mock<ICommonOperations>(MockBehavior.Strict);
            _broker = new Mock<IDisplayWindowBroker>(MockBehavior.Strict);
            _modeRaw = new Vim.Modes.Insert.InsertMode(_data.Object,_operations.Object,_broker.Object);
            _mode = _modeRaw;
        }

        [Test, Description("Must process escape")]
        public void CanProcess1()
        {
            Assert.IsTrue(_mode.CanProcess(VimKey.EscapeKey));
        }

        [Test, Description("Do not processing anything other than Escape")]
        public void CanProcess2()
        {
            Assert.IsFalse(_mode.CanProcess(VimKey.EnterKey));
            Assert.IsFalse(_mode.CanProcess(InputUtil.CharToKeyInput('c')));
        }

        [Test]
        public void Escape1()
        {
            _broker
                .SetupGet(x => x.IsCompletionWindowActive)
                .Returns(false)
                .Verifiable();
            var res = _mode.Process(VimKey.EscapeKey);
            Assert.IsTrue(res.IsSwitchMode);
            _broker.Verify();
        }

        [Test]
        public void Escape2()
        {
            _globalSettings.SetupGet(x => x.DoubleEscape).Returns(false);
            _broker
                .SetupGet(x => x.IsCompletionWindowActive)
                .Returns(true)
                .Verifiable();
            _broker
                .Setup(x => x.DismissCompletionWindow())
                .Verifiable();
            var res = _mode.Process(VimKey.EscapeKey);
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Normal, res.AsSwitchMode().Item);
        }

        [Test, Description("Double escape will only dismiss intellisense")]
        public void Escape3()
        {
            _globalSettings.SetupGet(x => x.DoubleEscape).Returns(true);
            _broker
                .SetupGet(x => x.IsCompletionWindowActive)
                .Returns(true)
                .Verifiable();
            _broker
                .Setup(x => x.DismissCompletionWindow())
                .Verifiable();
            var res = _mode.Process(VimKey.EscapeKey);
            Assert.IsTrue(res.IsProcessed);
        }

        [Test]
        public void Control_OpenBracket1()
        {
            var ki = InputUtil.CharAndModifiersToKeyInput('[', KeyModifiers.Control);
            var name = CommandName.NewOneKeyInput(ki);
            Assert.IsTrue(_mode.CommandNames.Contains(name));
        }

        [Test]
        public void Control_OpenBraket2()
        {
            _globalSettings.SetupGet(x => x.DoubleEscape).Returns(false);
            _broker
                .SetupGet(x => x.IsCompletionWindowActive)
                .Returns(true)
                .Verifiable();
            _broker
                .Setup(x => x.DismissCompletionWindow())
                .Verifiable();
            var ki = InputUtil.CharAndModifiersToKeyInput('[', KeyModifiers.Control);
            var res = _mode.Process(ki);
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Normal, res.AsSwitchMode().Item);
        }

        [Test]
        public void ShiftLeft1()
        {
            _operations
                .Setup(x => x.ShiftLinesLeft(1))
                .Verifiable(); ;
            var res = _mode.Process(new KeyInput('d', KeyModifiers.Control));
            Assert.IsTrue(res.IsProcessed);
            _operations.Verify();
            _globalSettings.Verify();
        }

        [Test]
        public void OnLeave1()
        {
            _textView.SetupGet(x => x.IsClosed).Returns(false).Verifiable();
            _operations.Setup(x => x.MoveCaretLeft(1)).Verifiable();
            _mode.OnLeave();
            _textView.Verify();
            _operations.Verify();
        }

        [Test]
        [Description("If the view is closed moving the caret will throw")]
        public void OnLeave2()
        {
            _textView.SetupGet(x => x.IsClosed).Returns(true).Verifiable();
            _mode.OnLeave();
            _textView.Verify();
        }


    }
}
