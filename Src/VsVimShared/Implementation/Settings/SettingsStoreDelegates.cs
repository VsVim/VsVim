using System;

using Microsoft.VisualStudio.Settings;

namespace Vim.VisualStudio.Implementation.Settings
{
    public delegate Func<string, string, T>
        VsStoreGetOrDefaultProxy<out T>(
            WritableSettingsStore settingsStore
            );

    public delegate Action<string, string, T>
        VsStoreSetProxy<in T>(
            WritableSettingsStore settingsStore
            );

    public delegate bool SettingsStoreGetter<T>(
        string key,
        out T result,
        T defaultValue);

    public delegate SettingsStoreGetter<T>
           SettingsStoreGetOrDefaultProxy<T>(
               ISettingsStore settingsStore
               );

    public delegate Action<string, T>
        SettingsStoreSetProxy<in T>(
            ISettingsStore settingsStore
            );
}