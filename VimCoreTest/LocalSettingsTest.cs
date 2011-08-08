using NUnit.Framework;
using Vim;
using GlobalSettings = Vim.GlobalSettings;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class LocalSettingsTest : SettingsCommonTest
    {
        protected override string ToggleSettingName { get { return LocalSettingNames.NumberName; } }
        protected override IVimSettings Create()
        {
            var global = new Vim.GlobalSettings();
            return new LocalSettings(global);
        }

        private IVimGlobalSettings _global;
        private LocalSettings _localRaw;
        private IVimLocalSettings _local;

        [SetUp]
        public void SetUp()
        {
            _global = new GlobalSettings();
            _localRaw = new LocalSettings(_global);
            _local = _localRaw;
        }

        [TearDown]
        public void TearDown()
        {
            _global = null;
            _localRaw = null;
            _local = null;
        }

        [Test]
        public void Sanity1()
        {
            Assert.AreSame(_global, _local.GlobalSettings);
        }
    }
}
