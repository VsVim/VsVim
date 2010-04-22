using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using GlobalSettings = Vim.GlobalSettings;
using Vim.Extensions;

namespace VimCoreTest
{
    [TestFixture]
    public abstract class SettingsCommonTest
    {
        protected abstract IVimSettings Create();

        protected abstract string ToggleSettingName { get; }

        [Test, Description("Value returned from all should be immutable")]
        public void AllSettings1()
        {
            var settings = Create();
            var all = settings.AllSettings;
            var value = all.Single(x => x.Name == GlobalSettingNames.ShiftWidthName);
            var prev= value.Value.AsNumberValue().Item;
            Assert.AreNotEqual(42, prev);
            Assert.IsTrue(settings.TrySetValue(GlobalSettingNames.ShiftWidthName, SettingValue.NewNumberValue(42)));
            value = all.Single(x => x.Name == GlobalSettingNames.ShiftWidthName);
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
            var opt = settings.GetSetting(GlobalSettingNames.IgnoreCaseName);
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
            Assert.IsTrue(settings.TrySetValue(GlobalSettingNames.IgnoreCaseName, SettingValue.NewToggleValue(true)));
            var value = settings.GetSetting(GlobalSettingNames.IgnoreCaseName);
            Assert.IsTrue(value.IsSome());
            Assert.AreEqual(true, value.Value.Value.AsBooleanValue().Item);
        }

        [Test]
        public void TrySetValue2()
        {
            var settings = Create();
            Assert.IsTrue(settings.TrySetValue(GlobalSettingNames.ShiftWidthName, SettingValue.NewNumberValue(42)));
            var value = settings.GetSetting(GlobalSettingNames.ShiftWidthName);
            Assert.IsTrue(value.IsSome());
            Assert.AreEqual(42, value.Value.Value.AsNumberValue().Item);
        }

        [Test, Description("Set by abbreviation")]
        public void TrySetValue3()
        {
            var settings = Create();
            foreach (var cur in settings.AllSettings)
            {
                SettingValue value = null;
                if ( cur.Kind.IsToggleKind )
                {
                    value = SettingValue.NewToggleValue(true);
                }
                else if (cur.Kind.IsStringKind)
                {
                    value = SettingValue.NewStringValue("foo");
                }
                else if (cur.Kind.IsNumberKind)
                {
                    value = SettingValue.NewNumberValue(42);
                }
                else
                {
                    Assert.Fail();
                }

                Assert.IsTrue(settings.TrySetValue(cur.Abbreviation, value));
            }
        }

        [Test]
        public void TrySetValueFromString1()
        {
            var settings = Create();
            foreach (var cur in settings.AllSettings)
            {
                string value = null;
                if ( cur.Kind.IsToggleKind )
                {
                    value = "true";
                }
                else if (cur.Kind.IsStringKind)
                {
                    value = "hello world";
                }
                else if (cur.Kind.IsNumberKind)
                {
                    value = "42";
                }
                else
                {
                    Assert.Fail();
                }

                Assert.IsTrue(settings.TrySetValueFromString(cur.Name, value));
            }
        }

        [Test, Description("Now by abbreviation")]
        public void TrySetValueFromString2()
        {
            var settings = Create();
            foreach (var cur in settings.AllSettings)
            {
                string value = null;
                if (cur.Kind.IsToggleKind)
                {
                    value = "true";
                }
                else if (cur.Kind.IsStringKind)
                {
                    value = "hello world";
                }
                else if (cur.Kind.IsNumberKind)
                {
                    value = "42";
                }
                else
                {
                    Assert.Fail();
                }

                Assert.IsTrue(settings.TrySetValueFromString(cur.Abbreviation, value));
            }
        }

        [Test]
        public void SettingChanged1()
        {
            var settings = Create();
            var didRun = false;
            settings.SettingChanged += (unused, setting) =>
                {
                    Assert.AreEqual(ToggleSettingName, setting.Name);
                    Assert.IsTrue(setting.AggregateValue.AsBooleanValue().Item);
                    didRun = true;
                };
            settings.TrySetValue(ToggleSettingName, SettingValue.NewToggleValue(true));
            Assert.IsTrue(didRun);
        }

        [Test]
        public void SettingChanged2()
        {
            var settings = Create();
            var didRun = false;
            settings.SettingChanged += (unused, setting) =>
                {
                    Assert.AreEqual(ToggleSettingName, setting.Name);
                    Assert.IsTrue(setting.AggregateValue.AsBooleanValue().Item);
                    didRun = true;
                };
            settings.TrySetValueFromString(ToggleSettingName, "true");
            Assert.IsTrue(didRun);
        }

    }
}
