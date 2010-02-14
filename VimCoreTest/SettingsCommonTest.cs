using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using GlobalSettings = Vim.GlobalSettings;

namespace VimCoreTest
{
    [TestFixture]
    public abstract class SettingsCommonTest
    {
        protected abstract IVimSettings Create();

        [Test, Description("Value returned from all should be immutable")]
        public void AllSettings1()
        {
            var settings = Create();
            var all = settings.AllSettings;
            var value = all.Single(x => x.Name == GlobalSettings.ShiftWidthName);
            var prev= value.Value.AsNumberValue().Item;
            Assert.AreNotEqual(42, prev);
            Assert.IsTrue(settings.TrySetValue(GlobalSettings.ShiftWidthName, SettingValue.NewNumberValue(42)));
            value = all.Single(x => x.Name == GlobalSettings.ShiftWidthName);
            Assert.AreEqual(prev, value.Value.AsNumberValue().Item);
        }

        [Test]
        public void GetSetting1()
        {
            var settings = Create();
            var opt = settings.GetSetting("foo");
            Assert.IsTrue(opt.IsNone());
        }

        [Test]
        public void GetSetting2()
        {
            var settings = Create();
            var opt = settings.GetSetting(GlobalSettings.IgnoreCaseName);
            Assert.IsTrue(opt.IsSome());
        }

        [Test, Description("Should work by abbreviation")]
        public void GetSetting3()
        {
            var settings = Create();
            var opt = settings.GetSetting("ic");
            Assert.IsTrue(opt.IsSome());
        }

        [Test, Description("Make sure all values are gettable by abbrevation")]
        public void GetSettings4()
        {
            var settings = Create();
            foreach (var setting in settings.AllSettings)
            {
                var opt = settings.GetSetting(setting.Abbreviation);
                Assert.IsTrue(opt.IsSome());
            }
        }

        [Test]
        public void TrySetValue1()
        {
            var settings = Create();
            Assert.IsTrue(settings.TrySetValue(GlobalSettings.IgnoreCaseName, SettingValue.NewBooleanValue(true)));
            var value = settings.GetSetting(GlobalSettings.IgnoreCaseName);
            Assert.IsTrue(value.IsSome());
            Assert.AreEqual(true, value.Value.Value.AsBooleanValue().Item);
        }

        [Test]
        public void TrySetValue2()
        {
            var settings = Create();
            Assert.IsTrue(settings.TrySetValue(GlobalSettings.ShiftWidthName, SettingValue.NewNumberValue(42)));
            var value = settings.GetSetting(GlobalSettings.ShiftWidthName);
            Assert.IsTrue(value.IsSome());
            Assert.AreEqual(42, value.Value.Value.AsNumberValue().Item);
        }
    }
}
