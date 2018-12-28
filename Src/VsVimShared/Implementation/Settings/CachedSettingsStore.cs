using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;

namespace Vim.VisualStudio.Implementation.Settings
{
    /// <summary>
    /// Settings cache decorator to reduce number of times we need to query internal VS mounted registry hive
    /// </summary>
    [Export(SettingStoreType.CachingStore, typeof(ISettingsStore))]
    internal class CachedSettingsStore : ISettingsStore
    {
        /// <summary>
        /// Type specialization provider
        /// To abstract code from static "specialization" class
        /// </summary>
        private readonly ISpecializedCacheProvider _cacheProvider;

        private readonly ISettingsStore _underlyingStore;

        private readonly HashSet<string> _notFoundKeys;

        [ImportingConstructor]
        internal CachedSettingsStore(
            ISpecializedCacheProvider cacheProvider,
            [Import(SettingStoreType.PhysicalStore)]ISettingsStore underlyingStore)
        {
            _cacheProvider = cacheProvider;
            _underlyingStore = underlyingStore;

            _notFoundKeys = new HashSet<string>();
        }

        void ISettingsStore.Set<T>(string key, T value)
        {
            var cache = _cacheProvider.Get<T>();

            if (!cache.TryGetValue(key, out var oldValue)
                || !AreEqual(oldValue, value))
            {
                _underlyingStore.Set(key, value);
            }

            cache[key] = value;

            _notFoundKeys.Remove(key);
        }

        /// <summary>
        /// Work-around to allow comparison
        /// While allowing type-safe work with Enums
        /// Since Enums don't implement IEquatable<T>
        /// </summary>
        /// <typeparam name="T">Comparands type</typeparam>
        /// <param name="left">Left comparand</param>
        /// <param name="right">Right comparand</param>
        /// <returns>Whether comparands are equal</returns>
        private bool AreEqual<T>(T left, T right)
        {
            return EqualityComparer<T>.Default.Equals(left, right);
        }

        bool ISettingsStore.GetOrDefault<T>(string key, out T value, T defaultValue)
        {
            if (_notFoundKeys.Contains(key))
            {
                value = defaultValue;
                return false;
            }

            var cache = _cacheProvider.Get<T>();

            return cache.TryGetValue(key, out value) ||
                   TryUpdateFromUnderlyingStore(key, out value, defaultValue);
        }

        private bool TryUpdateFromUnderlyingStore<T>(string key, out T value, T defaultValue)
        {
            // If we know that the key wasn't found before
            // And we didn't update the key
            // There is no reason to re-query underlying store
            // As store ownership is assumed to be exclusive
            Debug.Assert(!_notFoundKeys.Contains(key));

            var foundInUnderlyingStore = _underlyingStore.GetOrDefault(key, out value, defaultValue);

            if (foundInUnderlyingStore)
            {
                var cacheMap = _cacheProvider.Get<T>();
                cacheMap[key] = value;
            }
            else
            {
                _notFoundKeys.Add(key);
            }

            return foundInUnderlyingStore;
        }
    }
}