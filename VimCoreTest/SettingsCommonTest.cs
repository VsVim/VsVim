using System.Linq;
using Vim.Extensions;
using Xunit;
using System;
using System.Collections.Generic;

namespace Vim.UnitTest
{
    public abstract class SettingsCommonTest
    {
        private readonly IVimSettings _settings;

        protected SettingsCommonTest()
        {
            _settings = Create();
        }

        protected abstract string ToggleSettingName { get; }

        protected abstract string NumberSettingName { get; }

        protected abstract IVimSettings Create();

        protected Setting ToggleSetting
        {
            get { return _settings.GetSetting(ToggleSettingName).Value; }
        }

        protected Setting NumberSetting
        {
            get { return _settings.GetSetting(NumberSettingName).Value; }
        }

        #region Derived Implementations 

        public sealed class LocalSettingsTest : SettingsCommonTest
        {
            protected override string ToggleSettingName
            {
                get { return LocalSettingNames.NumberName; }
            }

            protected override string NumberSettingName
            {
                get { return LocalSettingNames.ShiftWidthName; }
            }

            protected override IVimSettings Create()
            {
                return new LocalSettings(new GlobalSettings());
            }
        }

        public sealed class GlobalSettingsTest : SettingsCommonTest
        {
            protected override string ToggleSettingName
            {
                get { return GlobalSettingNames.IgnoreCaseName; }
            }

            protected override string NumberSettingName
            {
                get { return GlobalSettingNames.ScrollOffsetName; }
            }

            protected override IVimSettings Create()
            {
                return new GlobalSettings();
            }
        }

        public sealed class WindowSettingsTest : SettingsCommonTest
        {
            protected override string ToggleSettingName
            {
                get { return WindowSettingNames.CursorLineName; }
            }

            protected override string NumberSettingName
            {
                get { return WindowSettingNames.ScrollName; }
            }

            protected override IVimSettings Create()
            {
                return new WindowSettings(new GlobalSettings());
            }
        }

        #endregion

        /// <summary>
        /// Value returned from all should be immutable
        /// </summary>
        [Fact]
        public void SettingsAreImmutable()
        {
            var all = _settings.AllSettings;
            var value = all.Single(x => x.Name == GlobalSettingNames.ScrollOffsetName);
            var prev = value.Value.AsNumber().Item;
            Assert.NotEqual(42, prev);
            Assert.True(_settings.TrySetValue(GlobalSettingNames.ScrollOffsetName, SettingValue.NewNumber(42)));
            value = all.Single(x => x.Name == GlobalSettingNames.ScrollOffsetName);
            Assert.Equal(prev, value.Value.AsNumber().Item);
        }

        /// <summary>
        /// Every setting should be accessible by their name
        /// </summary>
        [Fact]
        public void GetSettingByName()
        {
            foreach (var setting in _settings.AllSettings)
            {
                var found = _settings.GetSetting(setting.Name);
                Assert.True(found.IsSome());
                Assert.Equal(setting.Name, found.Value.Name);
                Assert.Equal(setting.Abbreviation, found.Value.Abbreviation);
            }
        }

        /// <summary>
        /// Every setting should be accessible by their abbreviation
        /// </summary>
        [Fact]
        public void GetSettingByAbbreviation()
        {
            foreach (var setting in _settings.AllSettings)
            {
                var found = _settings.GetSetting(setting.Abbreviation);
                Assert.True(found.IsSome());
                Assert.Equal(setting.Name, found.Value.Name);
                Assert.Equal(setting.Abbreviation, found.Value.Abbreviation);
            }
        }

        [Fact]
        public void GetSettingMissing()
        {
            var found = _settings.GetSetting("NotASettingName");
            Assert.True(found.IsNone());
        }

        [Fact]
        public void TrySetValue1()
        {
            Assert.True(_settings.TrySetValue(GlobalSettingNames.IgnoreCaseName, SettingValue.NewToggle(true)));
            var value = _settings.GetSetting(GlobalSettingNames.IgnoreCaseName);
            Assert.True(value.IsSome());
            Assert.Equal(true, value.Value.Value.AsToggle().Item);
        }

        [Fact]
        public void TrySetValue2()
        {
            Assert.True(_settings.TrySetValue(GlobalSettingNames.ScrollOffsetName, SettingValue.NewNumber(42)));
            var value = _settings.GetSetting(GlobalSettingNames.ScrollOffsetName);
            Assert.True(value.IsSome());
            Assert.Equal(42, value.Value.Value.AsNumber().Item);
        }

        /// <summary>
        /// Set by abbreviation
        /// </summary>
        [Fact]
        public void TrySetValue3()
        {
            foreach (var cur in _settings.AllSettings)
            {
                SettingValue value = null;
                if (cur.Kind.IsToggle)
                {
                    value = SettingValue.NewToggle(true);
                }
                else if (cur.Kind.IsString)
                {
                    value = SettingValue.NewString("foo");
                }
                else if (cur.Kind.IsNumber)
                {
                    value = SettingValue.NewNumber(42);
                }
                else
                {
                    throw new Exception("failed");
                }

                Assert.True(_settings.TrySetValue(cur.Abbreviation, value));
            }
        }

        [Fact]
        public void TrySetValueFromString1()
        {
            foreach (var cur in _settings.AllSettings)
            {
                string value = null;
                if (cur.Kind.IsToggle)
                {
                    value = "true";
                }
                else if (cur.Kind.IsString)
                {
                    value = "hello world";
                }
                else if (cur.Kind.IsNumber)
                {
                    value = "42";
                }
                else
                {
                    throw new Exception("failed");
                }

                Assert.True(_settings.TrySetValueFromString(cur.Name, value));
            }
        }

        /// <summary>
        /// Now by abbreviation
        /// </summary>
        [Fact]
        public void TrySetValueFromString2()
        {
            foreach (var cur in _settings.AllSettings)
            {
                string value = null;
                if (cur.Kind.IsToggle)
                {
                    value = "true";
                }
                else if (cur.Kind.IsString)
                {
                    value = "hello world";
                }
                else if (cur.Kind.IsNumber)
                {
                    value = "42";
                }
                else
                {
                    throw new Exception("failed");
                }

                Assert.True(_settings.TrySetValueFromString(cur.Abbreviation, value));
            }
        }

        [Fact]
        public void SetByAbbreviation_Number()
        {
            Assert.True(_settings.TrySetValueFromString(NumberSetting.Abbreviation, "2"));
            Assert.Equal(2, NumberSetting.Value.AsNumber().Item);
        }

        [Fact]
        public void SetByAbbreviation_Toggle()
        {
            Assert.True(_settings.TrySetValueFromString(ToggleSetting.Abbreviation, "true"));
            Assert.True(ToggleSetting.Value.AsToggle().Item);
        }

        [Fact]
        public void SettingChanged1()
        {
            var didRun = false;
            _settings.SettingChanged += (unused, args) =>
                {
                    var setting = args.Setting;
                    Assert.Equal(ToggleSettingName, setting.Name);
                    Assert.True(setting.Value.AsToggle().Item);
                    didRun = true;
                };
            _settings.TrySetValue(ToggleSettingName, SettingValue.NewToggle(true));
            Assert.True(didRun);
        }

        [Fact]
        public void SettingChanged2()
        {
            var didRun = false;
            _settings.SettingChanged += (unused, args) =>
                {
                    var setting = args.Setting;
                    Assert.Equal(ToggleSettingName, setting.Name);
                    Assert.True(setting.Value.AsToggle().Item);
                    didRun = true;
                };
            _settings.TrySetValueFromString(ToggleSettingName, "true");
            Assert.True(didRun);
        }

        [Fact]
        public void SettingsShouldStartAsDefault()
        {
            foreach (var setting in _settings.AllSettings)
            {
                Assert.True(setting.IsValueDefault);
            }
        }

    }
}
