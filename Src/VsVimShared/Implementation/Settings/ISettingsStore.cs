namespace Vim.VisualStudio.Implementation.Settings
{
    /// <summary>
    /// Interface for application settings storage provider
    /// </summary>
    public interface ISettingsStore
    {
        void Set<T>(string key, T value);

        bool Check<T>(string key, out T value, T defaultValue);
    }
}