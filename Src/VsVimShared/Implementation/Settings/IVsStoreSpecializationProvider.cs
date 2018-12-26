using Microsoft.VisualStudio.Settings;

namespace Vim.VisualStudio.Implementation.Settings
{
    /// <summary>
    /// Interface for initialized type-specialized container provider
    /// For property accessors
    /// </summary>
    public interface IVsStoreSpecializationProvider
    {
        VsSettingGetter<T> GetGetter<T>(WritableSettingsStore store);
        VsSettingSetter<T> GetSetter<T>(WritableSettingsStore store);
    }
}