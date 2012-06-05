using System.Linq;
using Vim.Extensions;
using Xunit;
using System;

namespace Vim.UnitTest
{
    public abstract class SettingsCommonTest
    {
        protected abstract IVimSettings Create();

        protected abstract string ToggleSettingName { get; }

        /// <summary>
        /// Value returned from all should be immutable
        /// </summary>
        [Fact]
        public void AllSettings1()
        {
            var settings = Create();
            var all = settings.AllSettings;
            var value = all.Single(x => x.Name == GlobalSettingNames.ShiftWidthName);
            var prev= value.Value.AsNumberValue().Item;
            Assert.NotEqual(42, prev);
            Assert.True(settings.TrySetValue(GlobalSettingNames.ShiftWidthName, SettingValue.NewNumberValue(42)));
            value = all.Single(x => x.Name == GlobalSettingNames.ShiftWidthName);
            Assert.Equal(prev, value.Value.AsNumberValue().Item);
        }

        [Fact]
        public void GetSetting1()
        {
            var settings = Create();
            var opt = settings.GetSetting("foo");
            Assert.True(opt.IsNone());
        }

        [Fact]
        public void GetSetting2()
        {
            var settings = Create();
            var opt = settings.GetSetting(GlobalSettingNames.IgnoreCaseName);
            Assert.True(opt.IsSome());
        }

        /// <summary>
        /// Should work by abbreviation
        /// </summary>
        [Fact]
        public void GetSetting3()
        {
            var settings = Create();
            var opt = settings.GetSetting("ic");
            Assert.True(opt.IsSome());
        }

        /// <summary>
        /// Make sure all values are gettable by abbrevation
        /// </summary>
        [Fact]
        public void GetSettings4()
        {
            var settings = Create();
            foreach (var setting in settings.AllSettings)
            {
                var opt = settings.GetSetting(setting.Abbreviation);
                Assert.True(opt.IsSome());
            }
        }

        [Fact]
        public void TrySetValue1()
        {
            var settings = Create();
            Assert.True(settings.TrySetValue(GlobalSettingNames.IgnoreCaseName, SettingValue.NewToggleValue(true)));
            var value = settings.GetSetting(GlobalSettingNames.IgnoreCaseName);
            Assert.True(value.IsSome());
            Assert.Equal(true, value.Value.Value.AsToggleValue().Item);
        }

        [Fact]
        public void TrySetValue2()
        {
            var settings = Create();
            Assert.True(settings.TrySetValue(GlobalSettingNames.ShiftWidthName, SettingValue.NewNumberValue(42)));
            var value = settings.GetSetting(GlobalSettingNames.ShiftWidthName);
            Assert.True(value.IsSome());
            Assert.Equal(42, value.Value.Value.AsNumberValue().Item);
        }

        /// <summary>
        /// Set by abbreviation
        /// </summary>
        [Fact]
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
                    throw new Exception("failed");
                }

                Assert.True(settings.TrySetValue(cur.Abbreviation, value));
            }
        }

        [Fact]
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
                    throw new Exception("failed");
                }

                Assert.True(settings.TrySetValueFromString(cur.Name, value));
            }
        }

        /// <summary>
        /// Now by abbreviation
        /// </summary>
        [Fact]
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
                    throw new Exception("failed");
                }

                Assert.True(settings.TrySetValueFromString(cur.Abbreviation, value));
            }
        }

        [Fact]
        public void SettingChanged1()
        {
            var settings = Create();
            var didRun = false;
            settings.SettingChanged += (unused, args) =>
                {
                    var setting = args.Setting;
                    Assert.Equal(ToggleSettingName, setting.Name);
                    Assert.True(setting.AggregateValue.AsToggleValue().Item);
                    didRun = true;
                };
            settings.TrySetValue(ToggleSettingName, SettingValue.NewToggleValue(true));
            Assert.True(didRun);
        }

        [Fact]
        public void SettingChanged2()
        {
            var settings = Create();
            var didRun = false;
            settings.SettingChanged += (unused, args) =>
                {
                    var setting = args.Setting;
                    Assert.Equal(ToggleSettingName, setting.Name);
                    Assert.True(setting.AggregateValue.AsToggleValue().Item);
                    didRun = true;
                };
            settings.TrySetValueFromString(ToggleSettingName, "true");
            Assert.True(didRun);
        }

        [Fact]
        public void SettingsShouldStartAsDefault()
        {
            var settings = Create();
            foreach (var setting in settings.AllSettings)
            {
                Assert.True(setting.IsValueDefault);
            }
        }

    }
}
