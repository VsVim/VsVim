using Microsoft.VisualStudio.Settings;

namespace Vim.VisualStudio.Implementation.Settings
{
    public delegate void VsSettingSetter<in T>(string collectionPath, string propertyName, T value);
    public delegate T VsSettingGetter<out T>(string collectionPath, string propertyName);

    public delegate VsSettingSetter<T> VsSettingSetterProxy<in T>(WritableSettingsStore store);
    public delegate VsSettingGetter<T> VsSettingGetterProxy<out T>(WritableSettingsStore store);
}