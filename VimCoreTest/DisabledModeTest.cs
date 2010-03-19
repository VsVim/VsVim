using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using Moq;
using VimCoreTest.Utils;
using GlobalSettings = Vim.GlobalSettings;

namespace VimCoreTest
{
    [TestFixture]
    public class DisabledModeTest
    {
        private FakeVimHost _host;
        private Mock<IVimLocalSettings> _settings;
        private Mock<IVimBuffer> _bufferData;
        private DisabledMode _modeRaw;
        private IMode _mode;

        [SetUp]
        public void Init()
        {
            _host = new FakeVimHost();
            _settings = MockObjectFactory.CreateLocalSettings();
            _bufferData = new Mock<IVimBuffer>(MockBehavior.Strict);
            _bufferData.SetupGet(x => x.Settings).Returns(_settings.Object);
            _bufferData.SetupGet(x => x.VimHost).Returns(_host);
            _modeRaw = new DisabledMode(_bufferData.Object);
            _mode = _modeRaw;
        }

        [Test]
        public void CanProcess1()
        {
            Assert.IsTrue(_mode.CanProcess(GlobalSettings.DisableCommand));
        }

        [Test]
        public void Commands1()
        {
            Assert.IsTrue(_mode.Commands.First().Equals(GlobalSettings.DisableCommand));
        }

        [Test]
        public void Process1()
        {
            var res = _mode.Process(GlobalSettings.DisableCommand);
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Normal, res.AsSwitchMode().item);
        }
    }
}
