using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using Moq;
using VimCoreTest.Utils;

namespace VimCoreTest
{
    [TestFixture]
    public class DisabledModeTest
    {
        private FakeVimHost _host;
        private VimSettings _settings;
        private Mock<IVimBuffer> _bufferData;
        private DisabledMode _modeRaw;
        private IMode _mode;

        [SetUp]
        public void Init()
        {
            _host = new FakeVimHost();
            _settings = VimSettingsUtil.CreateDefault;
            _bufferData = new Mock<IVimBuffer>(MockBehavior.Strict);
            _bufferData.SetupGet(x => x.Settings).Returns(_settings);
            _bufferData.SetupGet(x => x.VimHost).Returns(_host);
            _modeRaw = new DisabledMode(_bufferData.Object);
            _mode = _modeRaw;
        }

        [Test]
        public void CanProcess1()
        {
            Assert.IsTrue(_mode.CanProcess(_settings.DisableCommand));
        }

        [Test]
        public void CanProcess2()
        {
            _host.Status = String.Empty;
            Assert.IsFalse(_mode.CanProcess(InputUtil.CharToKeyInput('a')));
            Assert.IsFalse(String.IsNullOrEmpty(_host.Status));
        }

        [Test]
        public void OnEnter1()
        {
            _host.Status = String.Empty;
            _mode.OnEnter();
            Assert.IsFalse(string.IsNullOrEmpty(_host.Status));
        }

        [Test, Description("Leaving should clear the help message")]
        public void OnLeave1()
        {
            _host.Status = "aoeu";
            _mode.OnLeave();
            Assert.IsTrue(string.IsNullOrEmpty(_host.Status));
        }

        [Test]
        public void Commands1()
        {
            Assert.IsTrue(_mode.Commands.First().Equals(_settings.DisableCommand));
        }

        [Test]
        public void Process1()
        {
            var res = _mode.Process(_settings.DisableCommand);
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Normal, res.AsSwitchMode().item);
        }
    }
}
