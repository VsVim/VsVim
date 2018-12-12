using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace Vim.VisualStudio.Implementation.Settings
{
    [Export(typeof(ISettingsCache))]
    internal class InMemorySettingsCache : ISettingsCache
    {
        private readonly Dictionary<string, string> _stringsCache;
        private readonly Dictionary<string, bool> _booleansCache;

        [ImportingConstructor]
        internal InMemorySettingsCache()
        {
            _stringsCache = new Dictionary<string, string>();
            _booleansCache = new Dictionary<string, bool>();
        }

        void ISettingsCache.UpdateCache(string propertyName, string value)
        {
            if (!_stringsCache.ContainsKey(propertyName))
            {
                _stringsCache.Add(propertyName, value);
            }
            else
            {
                _stringsCache[propertyName] = value;
            }
        }

        void ISettingsCache.UpdateCache(string propertyName, bool value)
        {
            if (!_booleansCache.ContainsKey(propertyName))
            {
                _booleansCache.Add(propertyName, value);
            }
            else
            {
                _booleansCache[propertyName] = value;
            }
        }

        bool ISettingsCache.CheckBoolean(string propertyName, out bool result)
        {
            if (!_booleansCache.ContainsKey(propertyName))
            {
                result = default;

                return false;
            }

            result = _booleansCache[propertyName];

            return true;
        }

        bool ISettingsCache.CheckString(string propertyName, out string result)
        {
            if (!_stringsCache.ContainsKey(propertyName))
            {
                result = default;

                return false;
            }

            result = _stringsCache[propertyName];

            return true;
        }
    }
}