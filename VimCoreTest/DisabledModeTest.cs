using System.Linq;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
using GlobalSettings = Vim.GlobalSettings;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class DisabledModeTest
    {
        private Mock<IVimLocalSettings> _settings;
        private Mock<IVimBuffer> _buffer;
        private DisabledMode _modeRaw;
        private IDisabledMode _mode;

        [SetUp]
        public void Init()
        {
            _settings = MockObjectFactory.CreateLocalSettings();
            _buffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            _buffer.SetupGet(x => x.LocalSettings).Returns(_settings.Object);
            _modeRaw = new DisabledMode(_buffer.Object);
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
            Assert.IsTrue(_mode.CommandNames.First().KeyInputs.First().Equals(GlobalSettings.DisableCommand));
        }

        [Test]
        public void Process1()
        {
            var res = _mode.Process(GlobalSettings.DisableCommand);
            Assert.IsTrue(res.IsSwitchMode(ModeKind.Normal));
        }
    }
}
