using System.Linq;
using NUnit.Framework;

namespace Vim.UnitTest
{
    [TestFixture]
    public sealed class DisabledModeTest : VimTestBase
    {
        private DisabledMode _modeRaw;
        private IDisabledMode _mode;

        [SetUp]
        public void Init()
        {
            var vimBufferData = CreateVimBufferData(CreateTextView(""));
            _modeRaw = new DisabledMode(vimBufferData);
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
