using System;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Settings;

namespace Vim.VisualStudio.Implementation.Settings
{
    /// <summary>
    /// Type specialization encapsulating class
    /// Hard dependencies on static classes are not test-friendly
    /// So as a workaround we use a class to encapsulate it
    /// </summary>
    [Export(typeof(IVsStoreSpecializedAccessorsMap))]
    public class VsStoreSpecializedAccessorsMap : IVsStoreSpecializedAccessorsMap
    {
        public bool Initialized<T>()
        {
            return SpecializationMap<T>.Accessors != null;
        }

        public void Map<T>(VsSettingsStoreAccessorsProxy<T> accessors)
        {
            SpecializationMap<T>.Accessors = accessors;
        }

        public VsSettingGetter<T> GetGetter<T>(WritableSettingsStore store)
        {
            Validate<T>();
            return SpecializationMap<T>.Accessors.Getter(store);
        }

        public VsSettingSetter<T> GetSetter<T>(WritableSettingsStore store)
        {
            Validate<T>();
            return SpecializationMap<T>.Accessors.Setter(store);
        }

        private void Validate<T>()
        {
            if (!Initialized<T>())
            {
                throw new NotSupportedException(
                    $"Attempt to read a value of type {typeof(T)} " +
                    $"from VisualStudio Settings Store. " +
                    $"The type {typeof(T)} is not supported.");
            }
        }

        /// <summary>
        /// Enables Type specialization for property accessors
        /// </summary>
        private static class SpecializationMap<T>
        {
            public static VsSettingsStoreAccessorsProxy<T> Accessors { get; set; }
        }
    }
}