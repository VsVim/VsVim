using System;
using System.Collections.Generic;
using MonoDevelop.Core;

namespace Vim.VisualStudio.Implementation.Settings
{
    /// <summary>
    /// Manages properties for a collection inside a <see cref="WritableSettingsStore"/>. The key feature 
    /// here is caching. This avoids excessive disk / registry access in frequently accessed settings
    /// </summary>
    internal sealed class VimCollectionSettingsStore
    {
        private readonly Dictionary<string, object> _propertyMap = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly IProtectedOperations _protectedOperations;

        internal VimCollectionSettingsStore(IProtectedOperations protectedOperations)
        {
            _protectedOperations = protectedOperations;
        }

        private T GetProperty<T>(string name, T defaultValue)
        {
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
                var value = PropertyService.Get(name, defaultValue);
                _propertyMap[name] = value;
                return value;
            }
            catch (Exception e)
            {
                Report($"Unable to retrieve value {name} from the settings store", e);
                return defaultValue;
            }
        }

        private void SetProperty<T>(string name, T value)
        {
            _propertyMap.Remove(name);
            try
            {
                PropertyService.Set(name, value);
                _propertyMap[name] = value;
            }
            catch (Exception e)
            {
                Report($"Unable to set value {name} to the settings store", e);
            }
        }

        internal bool GetBoolean(string propertyName, bool defaultValue) =>
            GetProperty<bool>(propertyName, defaultValue);

        internal void SetBoolean(string propertyName, bool value) =>
            SetProperty(propertyName, value);

        internal string GetString(string propertyName, string defaultValue) =>
            GetProperty<string>(propertyName, defaultValue);

        internal void SetString(string propertyName, string value) =>
            SetProperty(propertyName, value);

        internal int Getint(string propertyName, int defaultValue) =>
            GetProperty<int>(propertyName, defaultValue);

        internal void Setint(string propertyName, int value) =>
            SetProperty(propertyName, value);

        private void Report(string message, Exception e)
        {
            message = $"{message}: {e.Message}";
            var exception = new Exception(message, e);
            _protectedOperations.Report(exception);
        }
    }
}
