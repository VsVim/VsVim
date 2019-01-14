using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;

namespace Vim.VisualStudio.Implementation.Settings
{
    /// <summary>
    /// Fail-safe implementation of settings store
    /// based on standard visual studio WritableSettingsStore class
    /// </summary>
    [Export(SettingStoreType.PhysicalStore, typeof(ISettingsStore))]
    public class VsSettingsStore : ISettingsStore
    {
        private const string ErrorGetFormat = "Cannot get setting {0}";
        private const string ErrorSetFormat = "Cannot set setting {0}";

        private readonly IProtectedOperations _protectedOperations;
        private readonly WritableSettingsStore _settingsStore;
        private readonly string _collectionPath;

        public event EventHandler<ApplicationSettingsEventArgs> SettingsChanged;

        [ImportingConstructor]
        public VsSettingsStore(
            SVsServiceProvider vsServiceProvider,
            ISettingsCollectionPathProvider collectionPathProvider,
            IProtectedOperations protectedOperations)
            : this(vsServiceProvider.GetWritableSettingsStore(),
                collectionPathProvider.CollectionName,
                protectedOperations)
        {
        }

        internal VsSettingsStore(
            WritableSettingsStore writableSettingsStore,
            string collectionPath,
            IProtectedOperations protectedOperations)
        {
            _protectedOperations = protectedOperations;

            _settingsStore = writableSettingsStore;
            _collectionPath = collectionPath;
        }

        public void SetBoolean(string key, bool value)
        {
            Set(key, value, x => x.SetBoolean);
        }

        public void SetString(string key, string value)
        {
            Set(key, value, x => x.SetString);
        }

        public void SetEnum<T>(string key, T value)
            where T : struct, Enum
        {
            Set(key, value, x => x.SetEnum);
        }

        public void SetBindings(string key, ReadOnlyCollection<CommandKeyBinding> value)
        {
            Set(key, value, x => x.SetRemovedBindings);
        }

        public bool GetBooleanOrDefault(string key, out bool result,bool defaultValue)
        {
            return GetOrDefault(key, defaultValue, out result, x => x.GetBoolean);
        }

        public bool GetStringOrDefault(string key, out string result, string defaultValue)
        {
            return GetOrDefault(key, defaultValue, out result, x => x.GetString);
        }

        public bool GetEnumOrDefault<T>(string key, out T result, T defaultValue)
            where T : struct, Enum
        {
            return GetOrDefault(key, defaultValue, out result, x => x.GetEnum<T>);
        }

        public bool GetBindingsOrDefault(
            string key,
            out ReadOnlyCollection<CommandKeyBinding> result,
            ReadOnlyCollection<CommandKeyBinding> defaultValue)
        {
            return GetOrDefault(key, defaultValue, out result, x => x.GetRemovedBindings);
        }

        private void Set<T>(string key, T value, VsStoreSetProxy<T> setProxy)
        {
            EnsureCollectionExists();

            try
            {
                var setter = setProxy(_settingsStore);
                setter(_collectionPath, key, value);

                OnSettingsChanged(key);
            }
            catch (Exception e)
            {
                Report(string.Format(ErrorSetFormat, key), e);
            }
        }

        private bool GetOrDefault<T>(string key, T defaultValue, out T result, VsStoreGetOrDefaultProxy<T> getProxy)
        {
            EnsureCollectionExists();

            try
            {
                var getter = getProxy(_settingsStore);
                var settingExists = _settingsStore.PropertyExists(_collectionPath, key);

                result = settingExists
                    ? getter(_collectionPath, key)
                    : defaultValue;

                return settingExists;
            }
            catch (Exception e)
            {
                Report(string.Format(ErrorGetFormat, key), e);

                result = defaultValue;
                return false;
            }
        }

        private void EnsureCollectionExists()
        {
            try
            {
                if (!_settingsStore.CollectionExists(_collectionPath))
                {
                    _settingsStore.CreateCollection(_collectionPath);
                }
            }
            catch (Exception e)
            {
                Report("Unable to create the settings collection", e);
            }
        }

        private void Report(string message, Exception innerException)
        {
            var fullMessage = $"{message}: {innerException.Message}";
            var exception = new Exception(fullMessage, innerException);

            _protectedOperations.Report(exception);
        }

        private void OnSettingsChanged(string changedSettingName)
        {
            var eventArgs = new ApplicationSettingsEventArgs(changedSettingName);
            SettingsChanged?.Invoke(this, eventArgs);
        }
    }
}