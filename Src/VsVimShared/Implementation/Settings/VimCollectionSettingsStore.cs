using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Vim.UI.Wpf;
using Vim;
using Vim.Extensions;

namespace Vim.VisualStudio.Implementation.Settings
{
    /// <summary>
    /// Manages properties for a collection inside a <see cref="WritableSettingsStore"/>. The key feature 
    /// here is caching. This avoids excessive disk / registry access in frequently accessed settings
    /// </summary>
    internal sealed class VimCollectionSettingsStore
    {
        private readonly Dictionary<string, object> _propertyMap = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly string _collectionPath;
        private bool _hasCreatedCollection;
        private readonly WritableSettingsStore _settingsStore;
        private readonly IProtectedOperations _protectedOperations;
        private readonly Func<string, string, bool, bool> _getBoolFunc;
        private readonly Action<string, string, bool> _setBoolFunc;
        private readonly Func<string, string, string, string> _getStringFunc;
        private readonly Action<string, string, string> _setStringFunc;
        private readonly Func<string, string, int, int> _getIntFunc;
        private readonly Action<string, string, int> _setIntFunc;

        internal VimCollectionSettingsStore(string collectionPath, WritableSettingsStore writableSettingsStore, IProtectedOperations protectedOperations)
        {
            _collectionPath = collectionPath;
            _protectedOperations = protectedOperations;
            _settingsStore = writableSettingsStore;
            _getBoolFunc = writableSettingsStore.GetBoolean;
            _setBoolFunc = writableSettingsStore.SetBoolean;
            _getStringFunc = writableSettingsStore.GetString;
            _setStringFunc = writableSettingsStore.SetString;
            _getIntFunc = writableSettingsStore.GetInt32;
            _setIntFunc = writableSettingsStore.SetInt32;
        }

        private T GetProperty<T>(string name, T defaultValue, Func<string, string, T, T> getValueFunc)
        {
            EnsureCollectionExists();
            if (_propertyMap.TryGetValue(name, out object obj))
            {
                if (obj is T t)
                {
                    return t;
                }

                // Property exists at a different type. Fall back to the backing settings store
                _propertyMap.Remove(name);
            }

            try
            {
                var value = getValueFunc(_collectionPath, name, defaultValue);
                _propertyMap[name] = value;
                return value;
            }
            catch (Exception e)
            {
                Report($"Unable to retrieve value {name} from the settings store", e);
                return defaultValue;
            }
        }

        private void SetProperty<T>(string name, T value, Action<string, string, T> setValueFunc)
        {
            EnsureCollectionExists();
            _propertyMap.Remove(name);
            try
            {
                setValueFunc(_collectionPath, name, value);
                _propertyMap[name] = value;
            }
            catch (Exception e)
            {
                Report($"Unable to set value {name} to the settings store", e);
            }
        }

        internal bool GetBoolean(string propertyName, bool defaultValue) =>
            GetProperty<bool>(propertyName, defaultValue, _getBoolFunc);

        internal void SetBoolean(string propertyName, bool value) =>
            SetProperty(propertyName, value, _setBoolFunc);

        internal string GetString(string propertyName, string defaultValue) =>
            GetProperty<string>(propertyName, defaultValue, _getStringFunc);

        internal void SetString(string propertyName, string value) =>
            SetProperty(propertyName, value, _setStringFunc);

        internal int Getint(string propertyName, int defaultValue) =>
            GetProperty<int>(propertyName, defaultValue, _getIntFunc);

        internal void Setint(string propertyName, int value) =>
            SetProperty(propertyName, value, _setIntFunc);

        private void EnsureCollectionExists()
        {
            if (!_hasCreatedCollection)
            {
                try
                {
                    if (!_settingsStore.CollectionExists(_collectionPath))
                    {
                        _settingsStore.CreateCollection(_collectionPath);
                    }

                    _hasCreatedCollection = true;
                }
                catch (Exception e)
                {
                    Report("Unable to create the settings collection", e);
                }
            }
        }

        private void Report(string message, Exception e)
        {
            message = message + ": " + e.Message;
            var exception = new Exception(message, e);
            _protectedOperations.Report(exception);
        }
    }
}
