namespace Vim.VisualStudio.Implementation.Settings
{
    public class VsSettingsStoreAccessorsProxy<T>
    {
        public VsSettingSetterProxy<T> Setter { get; }
        public VsSettingGetterProxy<T> Getter { get; }

        public VsSettingsStoreAccessorsProxy(
            VsSettingGetterProxy<T> getter, 
            VsSettingSetterProxy<T> setter)
        {
            Getter = getter;
            Setter = setter;
        }
    }
}