using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EditorUtils;
using Microsoft.VisualStudio.Settings;
using Moq;
using VsVim.Implementation.Settings;
using Xunit;

namespace VsVim.Shared.UnitTest
{
    public abstract class VimApplicationSettingsTest
    {
        private readonly MockRepository _factory;
        private readonly Mock<IProtectedOperations> _protectedOperations;
        private readonly Mock<WritableSettingsStore> _settingsStore;
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly VimApplicationSettings _vimApplicationSettingsRaw;

        protected VimApplicationSettingsTest(VisualStudioVersion visualStudioVersion = VisualStudioVersion.Vs2010)
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _protectedOperations = _factory.Create<IProtectedOperations>();
            _settingsStore = _factory.Create<WritableSettingsStore>();
            _vimApplicationSettingsRaw = new VimApplicationSettings(visualStudioVersion, _settingsStore.Object, _protectedOperations.Object);
            _vimApplicationSettings = _vimApplicationSettingsRaw;
        }

        private void SetupBoolGet(string propName, bool value)
        {
            _settingsStore.Setup(x => x.PropertyExists(VimApplicationSettings.CollectionPath, propName)).Returns(true);
            _settingsStore.Setup(x => x.GetBoolean(VimApplicationSettings.CollectionPath, propName)).Returns(value);
        }

        private void SetupBoolSet(string propName, bool value)
        {
            _settingsStore.Setup(x => x.SetBoolean(VimApplicationSettings.CollectionPath, propName, value)).Verifiable();
        }

        private void SetupStringGet(string propName, string value)
        {
            _settingsStore.Setup(x => x.PropertyExists(VimApplicationSettings.CollectionPath, propName)).Returns(true);
            _settingsStore.Setup(x => x.GetString(VimApplicationSettings.CollectionPath, propName)).Returns(value);
        }

        private void SetupStringSet(string propName, string value)
        {
            _settingsStore.Setup(x => x.SetString(VimApplicationSettings.CollectionPath, propName, value)).Verifiable();
        }

        public abstract class CollectionPathTest : VimApplicationSettingsTest
        {
            public sealed class BoolGetTest : CollectionPathTest
            {
                protected override void DoOperation()
                {
                    SetupBoolGet("myProp", true);
                    Assert.True(_vimApplicationSettingsRaw.GetBoolean("myProp", false));
                }
            }

            public sealed class BoolSetTest : CollectionPathTest
            {
                protected override void DoOperation()
                {
                    SetupBoolSet("myProp", true);
                    _vimApplicationSettingsRaw.SetBoolean("myProp", true);
                }
            }

            public sealed class StringGetTest : CollectionPathTest
            {
                protected override void DoOperation()
                {
                    SetupStringGet("myProp", "cat");
                    Assert.Equal("cat", _vimApplicationSettingsRaw.GetString("myProp", ""));
                }
            }

            public sealed class StringSetTest : CollectionPathTest
            {
                protected override void DoOperation()
                {
                    SetupStringSet("myProp", "cat");
                    _vimApplicationSettingsRaw.SetString("myProp", "cat");
                }
            }

            protected abstract void DoOperation();

            [Fact]
            public void CheckBeforeOperation()
            {
                _settingsStore.Setup(x => x.CollectionExists(VimApplicationSettings.CollectionPath)).Returns(true).Verifiable();
                DoOperation();
                _settingsStore.Verify();
            }

            [Fact]
            public void CreateBeforeOperation()
            {
                _settingsStore.Setup(x => x.CollectionExists(VimApplicationSettings.CollectionPath)).Returns(false).Verifiable();
                _settingsStore.Setup(x => x.CreateCollection(VimApplicationSettings.CollectionPath)).Verifiable();
                DoOperation();
                _settingsStore.Verify();
            }
        }

        public sealed class FutureVisualStudioVersionsTest : VimApplicationSettingsTest
        {
            public FutureVisualStudioVersionsTest()
                : base(VisualStudioVersion.Unknown)
            {

            }

            /// <summary>
            /// We don't support legacy settings on future versions of Visual Studio so the API should just pretend
            /// that they are already migrated
            /// </summary>
            [Fact]
            public void LegacySettingsAlreadyMigrated()
            {
                Assert.True(_vimApplicationSettings.LegacySettingsMigrated);
            }

            /// <summary>
            /// Just ignore any modifications to the legacy settings here.  Note this isn't a hack, because they 
            /// IVimApplicationSettings interface is implemented on top of a non-memory store we always have to 
            /// anticipate that setting values will fail.  
            /// </summary>
            [Fact]
            public void SetLegacySettingsIsNoop()
            {
                _vimApplicationSettings.LegacySettingsMigrated = false;
                Assert.True(_vimApplicationSettings.LegacySettingsMigrated);
            }
        }
    }
}
