using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class LocalSettingsTest : SettingsCommonTest
    {
        protected override string ToggleSettingName { get { return LocalSettingNames.NumberName; } }
        protected override IVimSettings Create()
        {
            var global = new Vim.GlobalSettings();
            var view = EditorUtil.CreateView("foo");
            return new LocalSettings(global, FSharpOption<ITextView>.Some(view));
        }

        private Mock<ITextView> _textView;
        private Mock<IVimGlobalSettings> _global;
        private LocalSettings _localRaw;
        private IVimLocalSettings _local;

        [SetUp]
        public void SetUp()
        {
            _textView = new Mock<ITextView>(MockBehavior.Strict);
            _global = new Mock<IVimGlobalSettings>(MockBehavior.Strict);
            _localRaw = new LocalSettings(_global.Object, FSharpOption<ITextView>.Some(_textView.Object));
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
            Assert.AreSame(_global.Object, _local.GlobalSettings);
        }

        [Test, Description("Go to global if it's not a local settings")]
        public void TrySetValueUp1()
        {
            _global.Setup(x => x.TrySetValue("notasetting", It.IsAny<SettingValue>())).Returns(false).Verifiable();
            Assert.IsFalse(_local.TrySetValue("notasetting", SettingValue.NewToggleValue(true)));
            _global.Verify();
        }

        [Test, Description("Don't go up for our own settings")]
        public void TrySetValueUp2()
        {
            Assert.IsTrue(_local.TrySetValue(LocalSettingNames.ScrollName, SettingValue.NewNumberValue(42)));
        }

        [Test]
        public void AllSettingsUp1()
        {
            _global.SetupGet(x => x.AllSettings).Returns(Enumerable.Empty<Setting>()).Verifiable();
            var res = _local.AllSettings;
            _global.Verify();
        }

    }
}
