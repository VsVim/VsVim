namespace Vim.VisualStudio.Implementation.Settings
{
    /// <summary>
    /// Settings cache to reduce number of times we need to query internal VS mounted registry hive
    /// </summary>
    public interface ISettingsCache
    {
        void UpdateCache(string propertyName, string value);
        void UpdateCache(string propertyName, bool value);

        bool CheckBoolean(string propertyName, out bool result);
        bool CheckString(string propertyName, out string result);
    }
}