using System;
using System.Collections.ObjectModel;

namespace Vim.VisualStudio.Implementation.Settings
{
    public static class SettingsStoreExtensions
    {
        public static bool GetBooleanOrDefault(this ISettingsStore settingsStore, string key, bool defaultValue)
        {
            settingsStore.GetBooleanOrDefault(key, out var result, defaultValue);

            return result;
        }

        public static string GetStringOrDefault(this ISettingsStore settingsStore, string key, string defaultValue)
        {
            settingsStore.GetStringOrDefault(key, out var result, defaultValue);

            return result;
        }

        public static T GetEnumOrDefault<T>(this ISettingsStore settingsStore, string key, T defaultValue)
            where T:struct, Enum
        {
            settingsStore.GetEnumOrDefault(key, out var result, defaultValue);

            return result;
        }

        public static ReadOnlyCollection<CommandKeyBinding> GetBindingsOrDefault(
            this ISettingsStore settingsStore,
            string key,
            ReadOnlyCollection<CommandKeyBinding> defaultValue)
        {
            settingsStore.GetBindingsOrDefault(key, out var result, defaultValue);

            return result;
        }
    }
}