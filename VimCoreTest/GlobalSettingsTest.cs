using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using GlobalSettings = Vim.GlobalSettings;
using VimCoreTest.Utils;

namespace VimCoreTest
{
    [TestFixture]
    public class GlobalSettingsTest
    {
        private Tuple<IVimGlobalSettings, GlobalSettings> Create()
        {
            var global = new GlobalSettings();
            IVimGlobalSettings interfaceVersion = global;
            return Tuple.Create(interfaceVersion, global);
        }

        [Test]
        public void AllSettings1()
        {
            var global = Create().Item1;
            var all = global.AllSettings;
            Assert.IsTrue(all.Any(x => x.Name == GlobalSettings.IgnoreCaseName));
            Assert.IsTrue(all.Any(x => x.Name == GlobalSettings.ShiftWidthName));
        }

        [Test, Description("Value returned from all should be immutable")]
        public void AllSettings2()
        {
            var global = Create().Item1;
            var all = global.AllSettings;
            var value = all.Single(x => x.Name == GlobalSettings.ShiftWidthName);
            var prev= value.Value.AsNumberValue().Item;
            Assert.AreNotEqual(42, prev);
            global.ShiftWidth = 42;
            value = all.Single(x => x.Name == GlobalSettings.ShiftWidthName);
            Assert.AreEqual(prev, value.Value.AsNumberValue().Item);
        }

        [Test]
        public void GetSetting1()
        {
            var global = Create().Item1;
            var opt = global.GetSetting("foo");
            Assert.IsTrue(opt.IsNone());
        }

        [Test]
        public void GetSetting2()
        {
            var global = Create().Item1;
            var opt = global.GetSetting(GlobalSettings.IgnoreCaseName);
            Assert.IsTrue(opt.IsSome());
        }

        [Test, Description("Should work by abbreviation")]
        public void GetSetting3()
        {
            var global = Create().Item1;
            var opt = global.GetSetting("ic");
            Assert.IsTrue(opt.IsSome());
        }

        [Test, Description("Make sure all values are gettable by abbrevation")]
        public void GetSettings4()
        {
            var global = Create().Item1;
            foreach (var setting in global.AllSettings)
            {
                var opt = global.GetSetting(setting.Abbreviation);
                Assert.IsTrue(opt.IsSome());
            }
        }

        [Test]
        public void TrySetValue1()
        {
            var global = Create().Item1;
            Assert.IsTrue(global.TrySetValue(GlobalSettings.IgnoreCaseName, SettingValue.NewBooleanValue(true)));
            Assert.AreEqual(true, global.IgnoreCase);
        }

        [Test]
        public void TrySetValue2()
        {
            var global = Create().Item1;
            Assert.IsTrue(global.TrySetValue(GlobalSettings.ShiftWidthName, SettingValue.NewNumberValue(42)));
            Assert.AreEqual(42, global.ShiftWidth);
        }

    }
}
