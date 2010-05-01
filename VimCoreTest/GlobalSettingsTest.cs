using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using GlobalSettings = Vim.GlobalSettings;
using VimCore.Test.Utils;

namespace VimCore.Test
{
    [TestFixture]
    public class GlobalSettingsTest : SettingsCommonTest
    {
        protected override string ToggleSettingName { get { return GlobalSettingNames.IgnoreCaseName; } }
        protected override IVimSettings Create()
        {
            return CreateGlobal();
        }

        private IVimGlobalSettings CreateGlobal()
        {
            return new GlobalSettings();
        }

        [Test]
        public void Sanity1()
        {
            var global = CreateGlobal();
            var all = global.AllSettings;
            Assert.IsTrue(all.Any(x => x.Name == GlobalSettingNames.IgnoreCaseName));
            Assert.IsTrue(all.Any(x => x.Name == GlobalSettingNames.ShiftWidthName));
        }

        [Test]
        public void SetByAbbreviation1()
        {
            var global = CreateGlobal();
            Assert.IsTrue(global.TrySetValueFromString("sw", "2"));
            Assert.AreEqual(2, global.ShiftWidth);
        }

        [Test]
        public void SetByAbbreviation2()
        {
            var global = CreateGlobal();
            Assert.IsFalse(global.IgnoreCase);
            Assert.IsTrue(global.TrySetValueFromString("ic", "true"));
            Assert.IsTrue(global.IgnoreCase);
        }

    }
}
