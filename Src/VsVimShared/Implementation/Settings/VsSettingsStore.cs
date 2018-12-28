using System;
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

        /// <summary>
        /// Getters/Setters specialization provider
        /// </summary>
        private readonly IVsStoreSpecializationProvider _specializationProvider;
        private readonly IProtectedOperations _protectedOperations;
        private readonly WritableSettingsStore _settingsStore;
        private readonly string _collectionPath;

        [ImportingConstructor]
        public VsSettingsStore(
            SVsServiceProvider vsServiceProvider,
            ISettingsCollectionPathProvider collectionPathProvider,
            IVsStoreSpecializationProvider specializationProvider,
            IProtectedOperations protectedOperations)
            : this(vsServiceProvider.GetWritableSettingsStore(),
                collectionPathProvider.CollectionName,
                specializationProvider,
                protectedOperations)
        {
        }

        internal VsSettingsStore(
            WritableSettingsStore writableSettingsStore,
            string collectionPath,
            IVsStoreSpecializationProvider specializationProvider,
            IProtectedOperations protectedOperations)
        {
            _specializationProvider = specializationProvider;
            _protectedOperations = protectedOperations;

            _settingsStore = writableSettingsStore;
            _collectionPath = collectionPath;
        }

        public void Set<T>(string key, T value)
        {
            EnsureCollectionExists();

            try
            {
                var setter = _specializationProvider.GetSetter<T>(_settingsStore);
                setter(_collectionPath, key, value);
            }
            catch (Exception e)
            {
                Report(string.Format(ErrorSetFormat, key), e);
            }
        }

        public bool GetOrDefault<T>(string key, out T value, T defaultValue)
        {
            EnsureCollectionExists();

            try
            {
                var getter = _specializationProvider.GetGetter<T>(_settingsStore);
                var settingExists = _settingsStore.PropertyExists(_collectionPath, key);

                value = settingExists
                    ? getter(_collectionPath, key)
                    : defaultValue;

                return settingExists;
            }
            catch (Exception e)
            {
                Report(string.Format(ErrorGetFormat, key), e);

                value = defaultValue;
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
    }
}