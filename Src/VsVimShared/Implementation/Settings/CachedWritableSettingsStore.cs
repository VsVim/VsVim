using Microsoft.VisualStudio.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vim.VisualStudio.Implementation.Settings
{
    /// <summary>
    /// Implementation of <see cref="WritableSettingsStore"/> which implements caching support for 
    /// the values. This avoids the default behavior of going to disk for every query as 
    /// is the default behavior.
    /// </summary>
    internal sealed class CachedWritableSettingsStore : WritableSettingsStore
    {
        internal readonly struct Key : IEquatable<Key>
        {
            internal static StringComparer Comparer { get; } = StringComparer.OrdinalIgnoreCase;
            internal readonly string CollectionPath;
            internal readonly string PropertyName;

            internal Key(string collectionPath, string propertyName)
            {
                CollectionPath = collectionPath;
                PropertyName = propertyName;
            }

            public bool Equals(Key other) =>
                Comparer.Equals(CollectionPath, other.CollectionPath) &&
                Comparer.Equals(PropertyName, other.PropertyName);

            public static bool operator ==(Key left, Key right) => left.Equals(right);
            public static bool operator !=(Key left, Key right) => !(left.Equals(right));
            public override bool Equals(object obj) => obj is Key key && Equals(key);
            public override int GetHashCode() => Comparer.GetHashCode(CollectionPath);
        }

        private readonly Dictionary<Key, object> _propertyMap = new Dictionary<Key, object>();
        private readonly Func<Key, bool> _getBoolFunc;
        private readonly Func<Key, bool, bool> _getBoolWithDefaultFunc;
        private readonly Action<Key, bool> _setBoolFunc;
        private readonly Func<Key, int> _getInt32Func;
        private readonly Func<Key, int, int> _getInt32WithDefaultFunc;
        private readonly Action<Key, int> _setInt32Func;
        private readonly Func<Key, uint> _getUInt32Func;
        private readonly Func<Key, uint, uint> _getUInt32WithDefaultFunc;
        private readonly Action<Key, uint> _setUInt32Func;
        private readonly Func<Key, long> _getInt64Func;
        private readonly Func<Key, long, long> _getInt64WithDefaultFunc;
        private readonly Action<Key, long> _setInt64Func;
        private readonly Func<Key, ulong> _getUInt64Func;
        private readonly Func<Key, ulong, ulong> _getUInt64WithDefaultFunc;
        private readonly Action<Key, ulong> _setUInt64Func;
        private readonly Func<Key, string> _getStringFunc;
        private readonly Func<Key, string, string> _getStringWithDefaultFunc;
        private readonly Action<Key, string> _setStringFunc;
        private readonly Func<Key, MemoryStream> _getMemoryStreamFunc;
        private readonly Action<Key, MemoryStream> _setMemoryStreamFunc;

        internal WritableSettingsStore BackingWritableSettingsStore { get; }

        internal CachedWritableSettingsStore(WritableSettingsStore backingWritableSettingsStore)
        {
            BackingWritableSettingsStore = backingWritableSettingsStore;

            var store = backingWritableSettingsStore;
            _getBoolFunc = key => store.GetBoolean(key.CollectionPath, key.PropertyName);
            _getBoolWithDefaultFunc = (key, defaultValue) => store.GetBoolean(key.CollectionPath, key.PropertyName, defaultValue);
            _setBoolFunc = (key, value) => store.SetBoolean(key.CollectionPath, key.PropertyName, value);
            _getInt32Func = key => store.GetInt32(key.CollectionPath, key.PropertyName);
            _getInt32WithDefaultFunc = (key, defaultValue) => store.GetInt32(key.CollectionPath, key.PropertyName, defaultValue);
            _setInt32Func = (key, value) => store.SetInt32(key.CollectionPath, key.PropertyName, value);
            _getUInt32Func = key => store.GetUInt32(key.CollectionPath, key.PropertyName);
            _getUInt32WithDefaultFunc = (key, defaultValue) => store.GetUInt32(key.CollectionPath, key.PropertyName, defaultValue);
            _setUInt32Func = (key, value) => store.SetUInt32(key.CollectionPath, key.PropertyName, value);
            _getInt64Func = key => store.GetInt64(key.CollectionPath, key.PropertyName);
            _getInt64WithDefaultFunc = (key, defaultValue) => store.GetInt64(key.CollectionPath, key.PropertyName, defaultValue);
            _setInt64Func = (key, value) => store.SetInt64(key.CollectionPath, key.PropertyName, value);
            _getUInt64Func = key => store.GetUInt64(key.CollectionPath, key.PropertyName);
            _getUInt64WithDefaultFunc = (key, defaultValue) => store.GetUInt64(key.CollectionPath, key.PropertyName, defaultValue);
            _setUInt64Func = (key, value) => store.SetUInt64(key.CollectionPath, key.PropertyName, value);
            _getStringFunc = key => store.GetString(key.CollectionPath, key.PropertyName);
            _getStringWithDefaultFunc = (key, defaultValue) => store.GetString(key.CollectionPath, key.PropertyName, defaultValue);
            _setStringFunc = (key, value) => store.SetString(key.CollectionPath, key.PropertyName, value);
            _getMemoryStreamFunc = key => store.GetMemoryStream(key.CollectionPath, key.PropertyName);
            _setMemoryStreamFunc = (key, value) => store.SetMemoryStream(key.CollectionPath, key.PropertyName, value);
        }

        private bool TryGetPropertyCore<T>(Key key, out T value)
        {
            if (_propertyMap.TryGetValue(key, out object obj))
            {
                if (obj is T t)
                {
                    value = t;
                    return true;
                }

                // Property exists at a different type. Fall back to the backing settings store
                _propertyMap.Remove(key);
            }

            value = default;
            return false;
        }

        internal T GetProperty<T>(Key key, Func<Key, T> getFunc)
        {
            if (TryGetPropertyCore(key, out T value))
            {
                return value;
            }

            value = getFunc(key);
            _propertyMap[key] = value;
            return value;
        }

        internal T GetProperty<T>(string collectionPath, string propertyName, Func<Key, T> getFunc) =>
            GetProperty(new Key(collectionPath, propertyName), getFunc);

        internal T GetProperty<T>(Key key, T defaultValue, Func<Key, T, T> getWithDefaultFunc)
        {
            if (TryGetPropertyCore(key, out T value))
            {
                return value;
            }

            value = getWithDefaultFunc(key, defaultValue);
            _propertyMap[key] = value;
            return value;
        }

        internal T GetProperty<T>(string collectionPath, string propertyName, T defaultValue, Func<Key, T, T> getWithDefaultFunc) =>
            GetProperty(new Key(collectionPath, propertyName), defaultValue, getWithDefaultFunc);
         
        internal void SetProperty<T>(Key key, T value, Action<Key, T> setFunc)
        {
            _propertyMap.Remove(key);
            setFunc(key, value);
            _propertyMap[key] = value;
        }

        internal void SetProperty<T>(string collectionPath, string propertyName, T value, Action<Key, T> setFunc) =>
            SetProperty(new Key(collectionPath, propertyName), value, setFunc);

        public override bool GetBoolean(string collectionPath, string propertyName) =>
            GetProperty(collectionPath, propertyName, _getBoolFunc);

        public override bool GetBoolean(string collectionPath, string propertyName, bool defaultValue) =>
            GetProperty(collectionPath, propertyName, defaultValue, _getBoolWithDefaultFunc);

        public override int GetInt32(string collectionPath, string propertyName) =>
            GetProperty(collectionPath, propertyName, _getInt32Func);

        public override int GetInt32(string collectionPath, string propertyName, int defaultValue) =>
            GetProperty(collectionPath, propertyName, defaultValue, _getInt32WithDefaultFunc);

        public override uint GetUInt32(string collectionPath, string propertyName) =>
            GetProperty(collectionPath, propertyName, _getUInt32Func);

        public override uint GetUInt32(string collectionPath, string propertyName, uint defaultValue) =>
            GetProperty(collectionPath, propertyName, defaultValue, _getUInt32WithDefaultFunc);

        public override long GetInt64(string collectionPath, string propertyName) =>
            GetProperty(collectionPath, propertyName, _getInt64Func);

        public override long GetInt64(string collectionPath, string propertyName, long defaultValue) =>
            GetProperty(collectionPath, propertyName, defaultValue, _getInt64WithDefaultFunc);

        public override ulong GetUInt64(string collectionPath, string propertyName) =>
            GetProperty(collectionPath, propertyName, _getUInt64Func);

        public override ulong GetUInt64(string collectionPath, string propertyName, ulong defaultValue) =>
            GetProperty(collectionPath, propertyName, defaultValue, _getUInt64WithDefaultFunc);

        public override string GetString(string collectionPath, string propertyName) =>
            GetProperty(collectionPath, propertyName, _getStringFunc);

        public override string GetString(string collectionPath, string propertyName, string defaultValue) =>
            GetProperty(collectionPath, propertyName, defaultValue, _getStringWithDefaultFunc);

        public override MemoryStream GetMemoryStream(string collectionPath, string propertyName) =>
            GetProperty(collectionPath, propertyName, _getMemoryStreamFunc);

        public override SettingsType GetPropertyType(string collectionPath, string propertyName)
        {
            var key = new Key(collectionPath, propertyName);
            if (_propertyMap.TryGetValue(key, out object value))
            {
                switch(value)
                {
                    case int _: return SettingsType.Int32;
                    case long _: return SettingsType.Int64;
                    case string _: return SettingsType.String;
                    case MemoryStream _: return SettingsType.Binary;
                }
            }

            return BackingWritableSettingsStore.GetPropertyType(collectionPath, propertyName);
        }

        public override bool PropertyExists(string collectionPath, string propertyName) =>
            _propertyMap.ContainsKey(new Key(collectionPath, propertyName)) ||
            BackingWritableSettingsStore.PropertyExists(collectionPath, propertyName);

        public override bool CollectionExists(string collectionPath) =>
            BackingWritableSettingsStore.CollectionExists(collectionPath);

        public override DateTime GetLastWriteTime(string collectionPath) =>
            BackingWritableSettingsStore.GetLastWriteTime(collectionPath);

        public override int GetSubCollectionCount(string collectionPath) =>
            BackingWritableSettingsStore.GetSubCollectionCount(collectionPath);

        public override int GetPropertyCount(string collectionPath) =>
            BackingWritableSettingsStore.GetPropertyCount(collectionPath);

        public override IEnumerable<string> GetSubCollectionNames(string collectionPath) =>
            BackingWritableSettingsStore.GetSubCollectionNames(collectionPath);

        public override IEnumerable<string> GetPropertyNames(string collectionPath) =>
            BackingWritableSettingsStore.GetSubCollectionNames(collectionPath);

        public override void SetBoolean(string collectionPath, string propertyName, bool value) =>
            SetProperty(collectionPath, propertyName, value, _setBoolFunc);

        public override void SetInt32(string collectionPath, string propertyName, int value) =>
            SetProperty(collectionPath, propertyName, value, _setInt32Func);

        public override void SetUInt32(string collectionPath, string propertyName, uint value) =>
            SetProperty(collectionPath, propertyName, value, _setUInt32Func);

        public override void SetInt64(string collectionPath, string propertyName, long value) =>
            SetProperty(collectionPath, propertyName, value, _setInt64Func);

        public override void SetUInt64(string collectionPath, string propertyName, ulong value) =>
            SetProperty(collectionPath, propertyName, value, _setUInt64Func);

        public override void SetString(string collectionPath, string propertyName, string value) =>
            SetProperty(collectionPath, propertyName, value, _setStringFunc);

        public override void SetMemoryStream(string collectionPath, string propertyName, MemoryStream value) =>
            SetProperty(collectionPath, propertyName, value, _setMemoryStreamFunc);

        public override void CreateCollection(string collectionPath) =>
            BackingWritableSettingsStore.CreateCollection(collectionPath);

        public override bool DeleteCollection(string collectionPath) =>
            BackingWritableSettingsStore.DeleteCollection(collectionPath);

        public override bool DeleteProperty(string collectionPath, string propertyName)
        {
            _propertyMap.Remove(new Key(collectionPath, propertyName));
            return BackingWritableSettingsStore.DeleteProperty(collectionPath, propertyName);
        }
    }
}
