using System;
using System.Collections.ObjectModel;

using Microsoft.VisualStudio.Settings;

using Vim.Extensions;

namespace Vim.VisualStudio.Implementation.Settings
{
    /// <summary>
    /// Extensions for converting some custom types
    /// To format supported by Visual Studio settings store
    /// </summary>
    public static class VsSettingsStoreExtensions
    {
        public static void SetEnum<T>(
            this WritableSettingsStore store,
            string collectionPath,
            string propertyName,
            T value)
        {
            store.SetString(collectionPath, propertyName, value.ToString());
        }

        public static T GetEnum<T>(
            this WritableSettingsStore store,
            string collectionPath,
            string propertyName)
        {
            var enumString = store.GetString(collectionPath, propertyName);

            if (!Enum.IsDefined(typeof(T), enumString))
            {
                return default;
            }

            return (T)Enum.Parse(typeof(T), enumString);
        }

        public static ReadOnlyCollection<CommandKeyBinding> GetRemovedBindings(
            this WritableSettingsStore store,
            string collectionPath,
            string propertyName)
        {
            var text = store.GetString(collectionPath, propertyName, string.Empty);
            var list = SettingSerializer.ConvertToCommandKeyBindings(text);

            return list.ToReadOnlyCollectionShallow();
        }

        public static void SetRemovedBindings(
            this WritableSettingsStore store,
            string collectionPath,
            string propertyName,
            ReadOnlyCollection<CommandKeyBinding> bindings)
        {
            var text = SettingSerializer.ConvertToString(bindings);
            store.SetString(collectionPath, propertyName, text);
        }
    }
}