using System;
using System.Collections.ObjectModel;

namespace Vim.VisualStudio.Implementation.Settings
{
    /// <summary>
    /// Interface for application settings storage provider
    /// </summary>
    public interface ISettingsStore
    {
        void SetBoolean(string key, bool value);

        void SetString(string key, string value);

        void SetEnum<T>(string key, T value) where T : struct, Enum;

        void SetBindings(string key, ReadOnlyCollection<CommandKeyBinding> value);

        bool GetBooleanOrDefault(string key, out bool result, bool defaultValue);

        bool GetStringOrDefault(string key, out string result, string defaultValue);

        bool GetEnumOrDefault<T>(string key, out T result, T defaultValue)
            where T : struct, Enum;

        bool GetBindingsOrDefault(
                    string key,
                    out ReadOnlyCollection<CommandKeyBinding> result,
                    ReadOnlyCollection<CommandKeyBinding> defaultValue);

        event EventHandler<ApplicationSettingsEventArgs> SettingsChanged;
    }
}