using System;

namespace Vim.VisualStudio.Implementation.Settings
{
    public static class SettingsStoreExtensions
    {
        public static T Get<T>(this ISettingsStore settingsStore, string key, T defaultValue)
        {
            settingsStore.GetOrDefault(key, out var result, defaultValue);

            return result;
        }
    }
}