using Xunit;

namespace Vim.UnitTest
{
    public class LocalSettingsTest : SettingsCommonTest
    {
        protected override string ToggleSettingName { get { return LocalSettingNames.NumberName; } }
        protected override IVimSettings Create()
        {
            var global = new GlobalSettings();
            return new LocalSettings(global);
        }

        private IVimGlobalSettings _global;
        private LocalSettings _localRaw;
        private IVimLocalSettings _local;

        public LocalSettingsTest()
        {
            _global = new GlobalSettings();
            _localRaw = new LocalSettings(_global);
            _local = _localRaw;
        }

        [Fact]
        public void Sanity1()
        {
            Assert.Same(_global, _local.GlobalSettings);
        }
    }
}
