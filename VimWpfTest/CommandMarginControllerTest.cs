using System;
using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using Vim.UI.Wpf.Properties;
using Vim.UnitTest.Mock;

namespace Vim.UI.Wpf.Test
{
    [TestFixture]
    public class CommandMarginControllerTest
    {
        private MockVimBuffer _buffer;
        private CommandMarginControl _marginControl;
        private CommandMarginController _controller;

        [SetUp]
        public void Setup()
        {
            _buffer = new MockVimBuffer();
            _marginControl = new CommandMarginControl();
            _marginControl.StatusLine = String.Empty;
            _controller = new CommandMarginController(
                _buffer,
                _marginControl,
                new List<Lazy<IOptionsProviderFactory>>());
        }

        [Test]
        [Description("A switch mode with no messages should display the banner")]
        public void SwitchMode1()
        {
            var mode = new Mock<INormalMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
            _buffer.NormalModeImpl = mode.Object;
            _buffer.RaiseSwitchedMode(_buffer.NormalModeImpl);
            Assert.AreEqual(String.Empty, _marginControl.StatusLine);
        }

        [Test]
        [Description("A switch mode with no messages should display the banner")]
        public void SwitchMode2()
        {
            var mode = new Mock<IMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            _buffer.RaiseSwitchedMode(mode.Object);
            Assert.AreEqual(Resources.InsertBanner, _marginControl.StatusLine);
        }

        [Test]
        [Description("Status line shouldn't be updated until a KeyInput event completes")]
        public void SwitchMode3()
        {
            var mode = new Mock<IMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            _marginControl.StatusLine = String.Empty;
            _buffer.RaiseKeyInputReceived(InputUtil.CharToKeyInput('c'));
            _buffer.RaiseSwitchedMode(mode.Object);
            Assert.AreEqual(String.Empty, _marginControl.StatusLine);
        }

        [Test]
        [Description("Status line shouldn't be updated until a KeyInput event completes")]
        public void SwitchMode4()
        {
            var mode = new Mock<IMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            var ki = InputUtil.CharToKeyInput('c');
            _marginControl.StatusLine = String.Empty;
            _buffer.RaiseKeyInputReceived(ki);
            _buffer.RaiseSwitchedMode(mode.Object);
            Assert.AreEqual(String.Empty, _marginControl.StatusLine);
            _buffer.RaiseKeyInputProcessed(ki, ProcessResult.Processed);
            Assert.AreEqual(Resources.InsertBanner, _marginControl.StatusLine);
        }

        [Test]
        [Description("A switch to command mode should start the status bar with a :.")]
        public void SwitchMode5()
        {
            var mode = new Mock<ICommandMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Command);
            mode.SetupGet(x => x.Command).Returns("");
            _buffer.CommandModeImpl = mode.Object;
            _buffer.RaiseSwitchedMode(_buffer.CommandModeImpl);
            Assert.AreEqual(":", _marginControl.StatusLine);
        }

        [Test]
        [Description("A switch to command mode should start the status bar with a :. + the command")]
        public void SwitchMode6()
        {
            var mode = new Mock<ICommandMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Command);
            mode.SetupGet(x => x.Command).Returns("foo");
            _buffer.CommandModeImpl = mode.Object;
            _buffer.RaiseSwitchedMode(_buffer.CommandModeImpl);
            Assert.AreEqual(":foo", _marginControl.StatusLine);
        }

        [Test]
        public void SwitchMode7()
        {
            var mode = new Mock<IMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Replace);
            _buffer.RaiseSwitchedMode(mode.Object);
            Assert.AreEqual(Resources.ReplaceBanner, _marginControl.StatusLine);
        }

        [Test]
        public void StatusMessage1()
        {
            _buffer.RaiseStatusMessage("foo");
            Assert.AreEqual("foo", _marginControl.StatusLine);
        }

        [Test]
        [Description("Don't add it until the end of a KeyInput event")]
        public void StatusMessage2()
        {
            var ki = InputUtil.CharToKeyInput('c');
            _buffer.RaiseKeyInputReceived(ki);
            _buffer.RaiseStatusMessage("foo");
            Assert.AreEqual(String.Empty, _marginControl.StatusLine);
        }

        [Test]
        [Description("Don't add it until the end of a KeyInput event")]
        public void StatusMessage3()
        {
            var ki = InputUtil.CharToKeyInput('c');
            _buffer.RaiseKeyInputReceived(ki);
            _buffer.RaiseStatusMessage("foo");
            _buffer.RaiseKeyInputProcessed(ki, ProcessResult.Processed);
            Assert.AreEqual("foo", _marginControl.StatusLine);
        }

        [Test]
        [Description("Status message should win over mode switch.  Think :setting ignorecase.  Both status + switch mode")]
        public void StatusMessage4()
        {
            var mode = new Mock<IMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            var ki = InputUtil.CharToKeyInput('c');
            _buffer.RaiseKeyInputReceived(ki);
            _buffer.RaiseStatusMessage("foo");
            _buffer.RaiseSwitchedMode(mode.Object);
            _buffer.RaiseKeyInputProcessed(ki, ProcessResult.Processed);
            Assert.AreEqual("foo", _marginControl.StatusLine);
        }

        [Test]
        public void StatusMessageLong1()
        {
            _buffer.RaiseStatusMessageLong("foo", "bar");
            Assert.AreEqual("foo" + Environment.NewLine + "bar", _marginControl.StatusLine);
        }

        [Test]
        [Description("Don't update in the middle of an KeyInput event")]
        public void StatusMessageLong2()
        {
            var ki = InputUtil.CharToKeyInput('c');
            _buffer.RaiseKeyInputReceived(ki);
            _buffer.RaiseStatusMessageLong("foo", "bar");
            Assert.AreEqual(String.Empty, _marginControl.StatusLine);
        }

        [Test]
        [Description("Don't update in the middle of an KeyInput event")]
        public void StatusMessageLong3()
        {
            var ki = InputUtil.CharToKeyInput('c');
            _buffer.RaiseKeyInputReceived(ki);
            _buffer.RaiseStatusMessageLong("foo", "bar");
            _buffer.RaiseKeyInputProcessed(ki, ProcessResult.Processed);
            Assert.AreEqual("foo" + Environment.NewLine + "bar", _marginControl.StatusLine);
        }

        [Test]
        [Description("StatusMessageLong wins over SwitchMode")]
        public void StatusMessageLong4()
        {
            var mode = new Mock<IMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            var ki = InputUtil.CharToKeyInput('c');
            _buffer.RaiseKeyInputReceived(ki);
            _buffer.RaiseStatusMessageLong("foo", "bar");
            _buffer.RaiseSwitchedMode(mode.Object);
            _buffer.RaiseKeyInputProcessed(ki, ProcessResult.Processed);
            Assert.AreEqual("foo" + Environment.NewLine + "bar", _marginControl.StatusLine);
        }

        [Test]
        public void ErrorMessage1()
        {
            _buffer.RaiseErrorMessage("foo");
            Assert.AreEqual("foo", _marginControl.StatusLine);
        }

        [Test]
        [Description("Don't add it until the end of a KeyInput event")]
        public void ErrorMessage2()
        {
            var ki = InputUtil.CharToKeyInput('c');
            _buffer.RaiseKeyInputReceived(ki);
            _buffer.RaiseErrorMessage("foo");
            Assert.AreEqual(String.Empty, _marginControl.StatusLine);
        }

        [Test]
        [Description("Don't add it until the end of a KeyInput event")]
        public void ErrorMessage3()
        {
            var ki = InputUtil.CharToKeyInput('c');
            _buffer.RaiseKeyInputReceived(ki);
            _buffer.RaiseErrorMessage("foo");
            _buffer.RaiseKeyInputProcessed(ki, ProcessResult.Processed);
            Assert.AreEqual("foo", _marginControl.StatusLine);
        }

        [Test]
        [Description("Status message should win over mode switch.  Think :setting ignorecase.  Both status + switch mode")]
        public void ErrorMessage4()
        {
            var mode = new Mock<IMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            var ki = InputUtil.CharToKeyInput('c');
            _buffer.RaiseKeyInputReceived(ki);
            _buffer.RaiseErrorMessage("foo");
            _buffer.RaiseSwitchedMode(mode.Object);
            _buffer.RaiseKeyInputProcessed(ki, ProcessResult.Processed);
            Assert.AreEqual("foo", _marginControl.StatusLine);
        }

        [Test]
        public void NoEvents1()
        {
            var search = new Mock<IIncrementalSearch>();
            search.SetupGet(x => x.InSearch).Returns(false).Verifiable();
            var mode = new Mock<INormalMode>();
            mode.SetupGet(x => x.IncrementalSearch).Returns(search.Object).Verifiable();
            mode.SetupGet(x => x.Command).Returns("foo");
            _buffer.ModeKindImpl = ModeKind.Normal;
            _buffer.NormalModeImpl = mode.Object;

            var ki = InputUtil.CharToKeyInput('c');
            _buffer.RaiseKeyInputReceived(ki);
            _buffer.RaiseKeyInputProcessed(ki, ProcessResult.Processed);
            Assert.AreEqual("foo", _marginControl.StatusLine);
            mode.Verify();
            search.Verify();
        }

        [Test]
        public void NoEvents2()
        {
            var mode = new Mock<ICommandMode>();
            mode.SetupGet(x => x.Command).Returns("foo");
            _buffer.ModeKindImpl = ModeKind.Command;
            _buffer.CommandModeImpl = mode.Object;

            var ki = InputUtil.CharToKeyInput('c');
            _buffer.RaiseKeyInputReceived(ki);
            _buffer.RaiseKeyInputProcessed(ki, ProcessResult.Processed);
            Assert.AreEqual(":foo", _marginControl.StatusLine);
            mode.Verify();
        }

        [Test]
        public void NoEvents3()
        {
            var mode = new Mock<IDisabledMode>();
            mode.SetupGet(x => x.HelpMessage).Returns("foo").Verifiable();
            _buffer.ModeKindImpl = ModeKind.Disabled;
            _buffer.DisabledModeImpl = mode.Object;

            var ki = InputUtil.CharToKeyInput('c');
            _buffer.RaiseKeyInputReceived(ki);
            _buffer.RaiseKeyInputProcessed(ki, ProcessResult.Processed);
            Assert.AreEqual("foo", _marginControl.StatusLine);
            mode.Verify();
        }

    }
}
