using Microsoft.VisualStudio.Settings;

namespace Vim.VisualStudio.Implementation.Settings
{
    /// <summary>
    /// Interface to abstract "Type specialization" code
    /// Since in .net the only way to do it now is to use static generics
    /// Which is not test-friendly if we embed it directly into the class
    /// This allows to have cleaner code without repeating methods for each type
    /// While also providing fast access as it doesn't involve reflection
    /// </summary>
    public interface IVsStoreSpecializedAccessorsMap
    {
        bool Initialized<T>();

        void Map<T>(VsSettingsStoreAccessorsProxy<T> accessors);

        VsSettingGetter<T> GetGetter<T>(WritableSettingsStore store);

        VsSettingSetter<T> GetSetter<T>(WritableSettingsStore store);
    }
}