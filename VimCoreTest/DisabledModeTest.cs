using System.Linq;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class DisabledModeTest : VimTestBase
    {
        private readonly DisabledMode _modeRaw;
        private readonly IDisabledMode _mode;

        public DisabledModeTest()
        {
            var vimBufferData = CreateVimBufferData(CreateTextView(""));
            _modeRaw = new DisabledMode(vimBufferData);
            _mode = _modeRaw;
        }

        [Fact]
        public void CanProcess1()
        {
            Assert.True(_mode.CanProcess(GlobalSettings.DisableAllCommand));
        }

        [Fact]
        public void Commands1()
        {
            Assert.True(_mode.CommandNames.First().KeyInputs.First().Equals(GlobalSettings.DisableAllCommand));
        }

        [Fact]
        public void Process1()
        {
            var res = _mode.Process(GlobalSettings.DisableAllCommand);
            Assert.True(res.IsSwitchMode(ModeKind.Normal));
        }
    }
}
