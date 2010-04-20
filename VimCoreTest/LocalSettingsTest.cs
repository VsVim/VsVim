using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using Moq;
using Microsoft.VisualStudio.Text.Editor;

namespace VimCoreTest
{
    [TestFixture]
    public class LocalSettingsTest : SettingsCommonTest
    {
        protected override string ToggleSettingName { get { return LocalSettingNames.NumberName; } }
        protected override Vim.IVimSettings Create()
        {
            var global = new Vim.GlobalSettings();
            var view = Utils.EditorUtil.CreateView("foo");
            return new LocalSettings(global, view);
        }

        private Mock<ITextView> _textView;
        private Mock<IVimGlobalSettings> _global;
        private LocalSettings _localRaw;
        private IVimLocalSettings _local;

        [SetUp]
        public void SetUp()
        {
            _textView= new Mock<ITextView>(MockBehavior.Strict);
            _global = new Mock<IVimGlobalSettings>(MockBehavior.Strict);
            _localRaw = new LocalSettings(_global.Object, _textView.Object);
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
