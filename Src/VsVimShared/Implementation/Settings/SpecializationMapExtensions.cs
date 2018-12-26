namespace Vim.VisualStudio.Implementation.Settings
{
    public static class SpecializationMapExtensions
    {
        public static void Map<T>(
            this IVsStoreSpecializedAccessorsMap map,
            VsSettingGetterProxy<T> getter,
            VsSettingSetterProxy<T> setter)
        {
            map.Map(new VsSettingsStoreAccessorsProxy<T>(getter, setter));
        }
    }
}