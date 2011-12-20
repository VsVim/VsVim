using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Classification;
using Moq;
using NUnit.Framework;
using Vim.Extensions;
using Vim.UI.Wpf.Properties;
using Vim.UnitTest.Mock;

namespace Vim.UI.Wpf.UnitTest
{
    [TestFixture]
    public sealed class CommandMarginControllerTest
    {
        private MockVimBuffer _vimBuffer;
        private CommandMarginControl _marginControl;
        private CommandMarginController _controller;
        private MockRepository _factory;
        private Mock<IIncrementalSearch> _search;

        [SetUp]
        public void Setup()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _search = _factory.Create<IIncrementalSearch>();
            _vimBuffer = new MockVimBuffer();
            _vimBuffer.IncrementalSearchImpl = _search.Object;
            _vimBuffer.VimImpl = MockObjectFactory.CreateVim(factory: _factory).Object;
            _marginControl = new CommandMarginControl();
            _marginControl.StatusLine = String.Empty;

            var editorFormatMap = _factory.Create<IEditorFormatMap>(MockBehavior.Loose);
            editorFormatMap.Setup(x => x.GetProperties(It.IsAny<string>())).Returns(new ResourceDictionary());

            _controller = new CommandMarginController(
                _vimBuffer,
                _marginControl,
                editorFormatMap.Object,
                new List<Lazy<IOptionsProviderFactory>>());
        }

        private void SimulatKeystroke()
        {
            var ki = KeyInputUtil.CharToKeyInput('a');
            _vimBuffer.RaiseKeyInputStart(ki);
            _vimBuffer.RaiseKeyInputEnd(ki);
        }

        [Test]
        [Description("A switch mode with no messages should display the banner")]
        public void SwitchMode1()
        {
            var mode = new Mock<INormalMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
            _vimBuffer.NormalModeImpl = mode.Object;
            _vimBuffer.RaiseSwitchedMode(new SwitchModeEventArgs(FSharpOption<IMode>.None, _vimBuffer.NormalModeImpl));
            Assert.AreEqual(String.Empty, _marginControl.StatusLine);
        }

        [Test]
        [Description("A switch mode with no messages should display the banner")]
        public void SwitchMode2()
        {
            var mode = new Mock<IMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            _vimBuffer.RaiseSwitchedMode(new SwitchModeEventArgs(FSharpOption<IMode>.None, mode.Object));
            Assert.AreEqual(Resources.InsertBanner, _marginControl.StatusLine);
        }

        [Test]
        [Description("Status line shouldn't be updated until a KeyInput event completes")]
        public void SwitchMode3()
        {
            var mode = new Mock<IMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            _marginControl.StatusLine = String.Empty;
            _vimBuffer.RaiseKeyInputStart(KeyInputUtil.CharToKeyInput('c'));
            _vimBuffer.RaiseSwitchedMode(new SwitchModeEventArgs(FSharpOption<IMode>.None, mode.Object));
            Assert.AreEqual(String.Empty, _marginControl.StatusLine);
        }

        [Test]
        [Description("Status line shouldn't be updated until a KeyInput event completes")]
        public void SwitchMode4()
        {
            var mode = new Mock<IMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            var ki = KeyInputUtil.CharToKeyInput('c');
            _marginControl.StatusLine = String.Empty;
            _vimBuffer.RaiseKeyInputStart(ki);
            _vimBuffer.RaiseSwitchedMode(new SwitchModeEventArgs(FSharpOption<IMode>.None, mode.Object));
            Assert.AreEqual(String.Empty, _marginControl.StatusLine);
            _vimBuffer.RaiseKeyInputEnd(ki);
            Assert.AreEqual(Resources.InsertBanner, _marginControl.StatusLine);
        }

        [Test]
        [Description("A switch to command mode should start the status bar with a :.")]
        public void SwitchMode5()
        {
            var mode = new Mock<ICommandMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Command);
            mode.SetupGet(x => x.Command).Returns("");
            _vimBuffer.CommandModeImpl = mode.Object;
            _vimBuffer.RaiseSwitchedMode(_vimBuffer.CommandModeImpl);
            Assert.AreEqual(":", _marginControl.StatusLine);
        }

        [Test]
        [Description("A switch to command mode should start the status bar with a :. + the command")]
        public void SwitchMode6()
        {
            var mode = new Mock<ICommandMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Command);
            mode.SetupGet(x => x.Command).Returns("foo");
            _vimBuffer.CommandModeImpl = mode.Object;
            _vimBuffer.RaiseSwitchedMode(_vimBuffer.CommandModeImpl);
            Assert.AreEqual(":foo", _marginControl.StatusLine);
        }

        [Test]
        public void SwitchMode7()
        {
            var mode = new Mock<IMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Replace);
            _vimBuffer.RaiseSwitchedMode(mode.Object);
            Assert.AreEqual(Resources.ReplaceBanner, _marginControl.StatusLine);
        }

        [Test]
        public void SwitchMode_OneTimeCommand_Insert()
        {
            var mode = new Mock<INormalMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
            _vimBuffer.InOneTimeCommandImpl = FSharpOption.Create(ModeKind.Insert);
            _vimBuffer.NormalModeImpl = mode.Object;
            _vimBuffer.RaiseSwitchedMode(mode.Object);
            Assert.AreEqual(String.Format(Resources.NormalOneTimeCommandBanner, "insert"), _marginControl.StatusLine);
        }

        [Test]
        public void SwitchMode_SubstituteConfirm1()
        {
            var mode = _factory.Create<ISubstituteConfirmMode>();
            mode.SetupGet(x => x.CurrentSubstitute).Returns(FSharpOption.Create("here")).Verifiable();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.SubstituteConfirm).Verifiable();
            _vimBuffer.SubstituteConfirmModeImpl = mode.Object;
            _vimBuffer.RaiseSwitchedMode(mode.Object);

            Assert.AreEqual(string.Format(Resources.SubstituteConfirmBannerFormat, "here"), _marginControl.StatusLine);
            _factory.Verify();
        }

        [Test]
        public void StatusMessage1()
        {
            _vimBuffer.RaiseStatusMessage("foo");
            Assert.AreEqual("foo", _marginControl.StatusLine);
        }

        [Test]
        [Description("Don't add it until the end of a KeyInput event")]
        public void StatusMessage2()
        {
            var ki = KeyInputUtil.CharToKeyInput('c');
            _vimBuffer.RaiseKeyInputStart(ki);
            _vimBuffer.RaiseStatusMessage("foo");
            Assert.AreEqual(String.Empty, _marginControl.StatusLine);
        }

        [Test]
        [Description("Don't add it until the end of a KeyInput event")]
        public void StatusMessage3()
        {
            var ki = KeyInputUtil.CharToKeyInput('c');
            _vimBuffer.RaiseKeyInputStart(ki);
            _vimBuffer.RaiseStatusMessage("foo");
            _vimBuffer.RaiseKeyInputEnd(ki);
            Assert.AreEqual("foo", _marginControl.StatusLine);
        }

        [Test]
        [Description("Status message should win over mode switch.  Think :setting ignorecase.  Both status + switch mode")]
        public void StatusMessage4()
        {
            var mode = new Mock<IMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            var ki = KeyInputUtil.CharToKeyInput('c');
            _vimBuffer.RaiseKeyInputStart(ki);
            _vimBuffer.RaiseStatusMessage("foo");
            _vimBuffer.RaiseSwitchedMode(mode.Object);
            _vimBuffer.RaiseKeyInputEnd(ki);
            Assert.AreEqual("foo", _marginControl.StatusLine);
        }

        [Test]
        public void StatusMessage5()
        {
            _vimBuffer.RaiseStatusMessage("foo" + Environment.NewLine + "bar");
            Assert.AreEqual("foo" + Environment.NewLine + "bar", _marginControl.StatusLine);
        }

        [Test]
        [Description("Don't update in the middle of an KeyInput event")]
        public void StatusMessage6()
        {
            var ki = KeyInputUtil.CharToKeyInput('c');
            _vimBuffer.RaiseKeyInputStart(ki);
            _vimBuffer.RaiseStatusMessage("foo" + Environment.NewLine + "bar");
            Assert.AreEqual(String.Empty, _marginControl.StatusLine);
        }

        [Test]
        [Description("Don't update in the middle of an KeyInput event")]
        public void StatusMessage7()
        {
            var ki = KeyInputUtil.CharToKeyInput('c');
            _vimBuffer.RaiseKeyInputStart(ki);
            _vimBuffer.RaiseStatusMessage("foo" + Environment.NewLine + "bar");
            _vimBuffer.RaiseKeyInputEnd(ki);
            Assert.AreEqual("foo" + Environment.NewLine + "bar", _marginControl.StatusLine);
        }

        [Test]
        [Description("StatusMessageLong wins over SwitchMode")]
        public void StatusMessage8()
        {
            var mode = new Mock<IMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            var ki = KeyInputUtil.CharToKeyInput('c');
            _vimBuffer.RaiseKeyInputStart(ki);
            _vimBuffer.RaiseStatusMessage("foo" + Environment.NewLine + "bar");
            _vimBuffer.RaiseSwitchedMode(mode.Object);
            _vimBuffer.RaiseKeyInputEnd(ki);
            Assert.AreEqual("foo" + Environment.NewLine + "bar", _marginControl.StatusLine);
        }

        [Test]
        public void ErrorMessage1()
        {
            _vimBuffer.RaiseErrorMessage("foo");
            Assert.AreEqual("foo", _marginControl.StatusLine);
        }

        [Test]
        [Description("Don't add it until the end of a KeyInput event")]
        public void ErrorMessage2()
        {
            var ki = KeyInputUtil.CharToKeyInput('c');
            _vimBuffer.RaiseKeyInputStart(ki);
            _vimBuffer.RaiseErrorMessage("foo");
            Assert.AreEqual(String.Empty, _marginControl.StatusLine);
        }

        [Test]
        [Description("Don't add it until the end of a KeyInput event")]
        public void ErrorMessage3()
        {
            var ki = KeyInputUtil.CharToKeyInput('c');
            _vimBuffer.RaiseKeyInputStart(ki);
            _vimBuffer.RaiseErrorMessage("foo");
            _vimBuffer.RaiseKeyInputEnd(ki);
            Assert.AreEqual("foo", _marginControl.StatusLine);
        }

        [Test]
        [Description("Status message should win over mode switch.  Think :setting ignorecase.  Both status + switch mode")]
        public void ErrorMessage4()
        {
            var mode = new Mock<IMode>();
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            var ki = KeyInputUtil.CharToKeyInput('c');
            _vimBuffer.RaiseKeyInputStart(ki);
            _vimBuffer.RaiseErrorMessage("foo");
            _vimBuffer.RaiseSwitchedMode(mode.Object);
            _vimBuffer.RaiseKeyInputEnd(ki);
            Assert.AreEqual("foo", _marginControl.StatusLine);
        }

        [Test]
        public void NoEvents1()
        {
            var mode = new Mock<INormalMode>();
            _search.SetupGet(x => x.InSearch).Returns(false).Verifiable();
            mode.SetupGet(x => x.Command).Returns("foo");
            _vimBuffer.ModeKindImpl = ModeKind.Normal;
            _vimBuffer.NormalModeImpl = mode.Object;

            SimulatKeystroke();
            Assert.AreEqual("foo", _marginControl.StatusLine);
            mode.Verify();
            _factory.Verify();
        }

        [Test]
        public void NoEvents2()
        {
            var mode = new Mock<ICommandMode>();
            mode.SetupGet(x => x.Command).Returns("foo");
            _vimBuffer.ModeKindImpl = ModeKind.Command;
            _vimBuffer.CommandModeImpl = mode.Object;

            SimulatKeystroke();
            Assert.AreEqual(":foo", _marginControl.StatusLine);
            mode.Verify();
        }

        [Test]
        public void NoEvents3()
        {
            var mode = new Mock<IDisabledMode>();
            mode.SetupGet(x => x.HelpMessage).Returns("foo").Verifiable();
            _vimBuffer.ModeKindImpl = ModeKind.Disabled;
            _vimBuffer.DisabledModeImpl = mode.Object;

            SimulatKeystroke();
            Assert.AreEqual("foo", _marginControl.StatusLine);
            mode.Verify();
        }

        [Test]
        public void Search1()
        {
            var mode = _factory.Create<INormalMode>();
            _vimBuffer.NormalModeImpl = mode.Object;
            _vimBuffer.ModeKindImpl = ModeKind.Normal;
            var data = new SearchData("cat", SearchKind.Forward, SearchOptions.None);
            _search.SetupGet(x => x.InSearch).Returns(true).Verifiable();
            _search.SetupGet(x => x.CurrentSearchData).Returns(FSharpOption.Create(data)).Verifiable();
            SimulatKeystroke();
            Assert.AreEqual("/cat", _marginControl.StatusLine);
            _factory.Verify();
        }

        [Test]
        public void Search2()
        {
            var mode = _factory.Create<INormalMode>();
            _vimBuffer.NormalModeImpl = mode.Object;
            _vimBuffer.ModeKindImpl = ModeKind.Normal;
            var data = new SearchData("cat", SearchKind.Backward, SearchOptions.None);
            _search.SetupGet(x => x.InSearch).Returns(true).Verifiable();
            _search.SetupGet(x => x.CurrentSearchData).Returns(FSharpOption.Create(data)).Verifiable();
            SimulatKeystroke();
            Assert.AreEqual("?cat", _marginControl.StatusLine);
            _factory.Verify();
        }
    }
}
