using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Text;
using Vim.EditorHost;
using EnvDTE;
using Microsoft.VisualStudio.Settings;
using Moq;
using Vim.UI.Wpf;
using Vim.VisualStudio.Implementation.Settings;
using Vim.VisualStudio.UnitTest.Mock;
using Xunit;

namespace Vim.VisualStudio.UnitTest
{
    public abstract class VimApplicationSettingsTest
    {
        #region SimpleWritableSettingsStore

        private sealed class SimpleWritableSettingsStore : WritableSettingsStore
        {
            private readonly Dictionary<string, Dictionary<string, object>> _map = new Dictionary<string, Dictionary<string, object>>();

            private void SetCore<T>(string collectionPath, string propertyName, T value)
            {
                var map = _map[collectionPath];
                map[propertyName] = value;
            }

            private T GetCore<T>(string collectionPath, string propertyName)
            {
                var map = _map[collectionPath];
                return (T)map[propertyName];
            }

            private T GetCore<T>(string collectionPath, string propertyName, T defaultValue)
            {
                try
                {
                    return GetCore<T>(collectionPath, propertyName);
                }
                catch
                {
                    return defaultValue;
                }
            }

            private bool DeleteCore(string collectionPath, string propertyName)
            {
                var map = _map[collectionPath];
                return map.Remove(propertyName);
            }

            public override void CreateCollection(string collectionPath)
            {
                if (_map.ContainsKey(collectionPath))
                {
                    throw new Exception();
                }

                _map[collectionPath] = new Dictionary<string, object>();
            }

            public override bool DeleteCollection(string collectionPath)
            {
                throw new NotImplementedException();
            }

            public override bool DeleteProperty(string collectionPath, string propertyName)
            {
                return DeleteCore(collectionPath, propertyName);
            }

            public override void SetBoolean(string collectionPath, string propertyName, bool value)
            {
                SetCore(collectionPath, propertyName, value);
            }

            public override void SetInt32(string collectionPath, string propertyName, int value)
            {
                SetCore(collectionPath, propertyName, value);
            }

            public override void SetInt64(string collectionPath, string propertyName, long value)
            {
                SetCore(collectionPath, propertyName, value);
            }

            public override void SetMemoryStream(string collectionPath, string propertyName, MemoryStream value)
            {
                SetCore(collectionPath, propertyName, value);
            }

            public override void SetString(string collectionPath, string propertyName, string value)
            {
                SetCore(collectionPath, propertyName, value);
            }

            public override void SetUInt32(string collectionPath, string propertyName, uint value)
            {
                SetCore(collectionPath, propertyName, value);
            }

            public override void SetUInt64(string collectionPath, string propertyName, ulong value)
            {
                SetCore(collectionPath, propertyName, value);
            }

            public override bool CollectionExists(string collectionPath)
            {
                return _map.ContainsKey(collectionPath);
            }

            public override bool GetBoolean(string collectionPath, string propertyName)
            {
                return GetCore<bool>(collectionPath, propertyName);
            }

            public override bool GetBoolean(string collectionPath, string propertyName, bool defaultValue)
            {
                return GetCore<bool>(collectionPath, propertyName, defaultValue);
            }

            public override int GetInt32(string collectionPath, string propertyName)
            {
                return GetCore<int>(collectionPath, propertyName);
            }

            public override int GetInt32(string collectionPath, string propertyName, int defaultValue)
            {
                return GetCore<int>(collectionPath, propertyName, defaultValue);
            }

            public override long GetInt64(string collectionPath, string propertyName)
            {
                return GetCore<long>(collectionPath, propertyName);
            }

            public override long GetInt64(string collectionPath, string propertyName, long defaultValue)
            {
                return GetCore<long>(collectionPath, propertyName, defaultValue);
            }

            public override DateTime GetLastWriteTime(string collectionPath)
            {
                throw new NotImplementedException();
            }

            public override MemoryStream GetMemoryStream(string collectionPath, string propertyName)
            {
                return GetCore<MemoryStream>(collectionPath, propertyName);
            }

            public override int GetPropertyCount(string collectionPath)
            {
                throw new NotImplementedException();
            }

            public override IEnumerable<string> GetPropertyNames(string collectionPath)
            {
                throw new NotImplementedException();
            }

            public override SettingsType GetPropertyType(string collectionPath, string propertyName)
            {
                throw new NotImplementedException();
            }

            public override string GetString(string collectionPath, string propertyName)
            {
                return GetCore<string>(collectionPath, propertyName);
            }

            public override string GetString(string collectionPath, string propertyName, string defaultValue)
            {
                return GetCore<string>(collectionPath, propertyName, defaultValue);
            }

            public override int GetSubCollectionCount(string collectionPath)
            {
                throw new NotImplementedException();
            }

            public override IEnumerable<string> GetSubCollectionNames(string collectionPath)
            {
                throw new NotImplementedException();
            }

            public override uint GetUInt32(string collectionPath, string propertyName)
            {
                return GetCore<uint>(collectionPath, propertyName);
            }

            public override uint GetUInt32(string collectionPath, string propertyName, uint defaultValue)
            {
                return GetCore<uint>(collectionPath, propertyName, defaultValue);
            }

            public override ulong GetUInt64(string collectionPath, string propertyName)
            {
                return GetCore<ulong>(collectionPath, propertyName);
            }

            public override ulong GetUInt64(string collectionPath, string propertyName, ulong defaultValue)
            {
                return GetCore<ulong>(collectionPath, propertyName, defaultValue);
            }

            public override bool PropertyExists(string collectionPath, string propertyName)
            {
                return _map[collectionPath].ContainsKey(propertyName);
            }
        }

        #endregion

        private readonly MockRepository _factory;
        private readonly Mock<IProtectedOperations> _protectedOperations;
        private readonly Mock<IVsStoreSpecializationProvider> _vsStoreSpecialization;
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly VimApplicationSettings _vimApplicationSettingsRaw;
        private readonly WritableSettingsStore _writableSettingsStore;
        private readonly string _collectionPath;

        protected VimApplicationSettingsTest(VisualStudioVersion visualStudioVersion = VisualStudioVersion.Vs2012, WritableSettingsStore settingsStore = null)
        {
            settingsStore = settingsStore ?? new SimpleWritableSettingsStore();
            _factory = new MockRepository(MockBehavior.Strict);
            _protectedOperations = _factory.Create<IProtectedOperations>();
            _collectionPath = VsVimConstantCollectionPathProvider.CollectionPath;
            _vsStoreSpecialization = _factory.Create<IVsStoreSpecializationProvider>();
            var wrappedSettingsStore = new VsSettingsStore(settingsStore, _collectionPath, _vsStoreSpecialization.Object, _protectedOperations.Object);
            _vimApplicationSettingsRaw = new VimApplicationSettings(wrappedSettingsStore);
            _vimApplicationSettings = _vimApplicationSettingsRaw;
            _writableSettingsStore = settingsStore;
        }

        public abstract class CollectionPathTest : VimApplicationSettingsTest
        {
            public sealed class BoolGetTest : CollectionPathTest
            {
                protected override void DoOperation()
                {
                    SetupBoolGet("myProp", true);
                    Assert.True(_vimApplicationSettingsRaw.Get("myProp", false));
                }
            }

            public sealed class BoolSetTest : CollectionPathTest
            {
                protected override void DoOperation()
                {
                    SetupBoolSet("myProp", true);
                    _vimApplicationSettingsRaw.Set("myProp", true);
                }
            }

            public sealed class StringGetTest : CollectionPathTest
            {
                protected override void DoOperation()
                {
                    SetupStringGet("myProp", "cat");
                    Assert.Equal("cat", _vimApplicationSettingsRaw.Get("myProp", ""));
                }
            }

            public sealed class StringSetTest : CollectionPathTest
            {
                protected override void DoOperation()
                {
                    SetupStringSet("myProp", "cat");
                    _vimApplicationSettingsRaw.Set("myProp", "cat");
                }
            }

            private readonly Mock<WritableSettingsStore> _settingsStore;

            protected CollectionPathTest() : base(settingsStore: (new Mock<WritableSettingsStore>(MockBehavior.Strict).Object))
            {
                _settingsStore = Moq.Mock.Get(_writableSettingsStore);
            }

            protected abstract void DoOperation();

            private void SetupBoolGet(string propName, bool value)
            {
                _vsStoreSpecialization.Setup(x => x.GetGetter<bool>(_writableSettingsStore))
                    .Returns((collectionPath, propertyName) => _writableSettingsStore.GetBoolean(collectionPath, propertyName));

                _settingsStore.Setup(x => x.PropertyExists(_collectionPath, propName)).Returns(true);
                _settingsStore.Setup(x => x.GetBoolean(_collectionPath, propName)).Returns(value);
            }

            private void SetupBoolSet(string propName, bool value)
            {
                _vsStoreSpecialization.Setup(x => x.GetSetter<bool>(_writableSettingsStore))
                    .Returns((collectionPath, propertyName, val) => _writableSettingsStore.SetBoolean(collectionPath, propertyName, val));

                _settingsStore.Setup(x => x.SetBoolean(_collectionPath, propName, value)).Verifiable();
            }

            private void SetupStringGet(string propName, string value)
            {
                _vsStoreSpecialization.Setup(x => x.GetGetter<string>(_writableSettingsStore))
                    .Returns((collectionPath, propertyName) => _writableSettingsStore.GetString(collectionPath, propertyName)); ;

                _settingsStore.Setup(x => x.PropertyExists(_collectionPath, propName)).Returns(true);
                _settingsStore.Setup(x => x.GetString(_collectionPath, propName)).Returns(value);
            }

            private void SetupStringSet(string propName, string value)
            {
                _vsStoreSpecialization.Setup(x => x.GetSetter<string>(_writableSettingsStore))
                    .Returns((collectionPath, propertyName, val) => _writableSettingsStore.SetString(collectionPath, propertyName, val));

                _settingsStore.Setup(x => x.SetString(_collectionPath, propName, value)).Verifiable();
            }

            [Fact]
            public void CheckBeforeOperation()
            {
                _settingsStore.Setup(x => x.CollectionExists(_collectionPath)).Returns(true).Verifiable();
                DoOperation();
                _settingsStore.Verify();
            }

            [Fact]
            public void CreateBeforeOperation()
            {
                _settingsStore.Setup(x => x.CollectionExists(_collectionPath)).Returns(false).Verifiable();
                _settingsStore.Setup(x => x.CreateCollection(_collectionPath)).Verifiable();
                DoOperation();
                _settingsStore.Verify();
            }
        }

        public sealed class MiscTest : VimApplicationSettingsTest
        {
            [Fact]
            public void Defaults()
            {
                _vsStoreSpecialization.Setup(x => x.GetGetter<bool>(_writableSettingsStore))
                    .Returns((collectionPath, propertyName) => _writableSettingsStore.GetBoolean(collectionPath, propertyName));

                Assert.True(_vimApplicationSettings.UseEditorDefaults);
                Assert.True(_vimApplicationSettings.UseEditorIndent);
                Assert.True(_vimApplicationSettings.UseEditorTabAndBackspace);
            }
        }
    }
}
