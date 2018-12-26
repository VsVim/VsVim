using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Settings;

namespace Vim.VisualStudio.Implementation.Settings
{
    /// <summary>
    /// Provider that initializes specializations for base types
    /// And extend it to allow our custom types
    /// With dynamic support for Enums binding
    /// </summary>
    [Export(typeof(IVsStoreSpecializationProvider))]
    internal class VsStoreStaticSpecializationProvider : IVsStoreSpecializationProvider
    {
        private readonly IVsStoreSpecializedAccessorsMap _accessorsMap;

        [ImportingConstructor]
        public VsStoreStaticSpecializationProvider(IVsStoreSpecializedAccessorsMap accessorsMap)
        {
            _accessorsMap = accessorsMap;

            _accessorsMap.Map<bool>(x => x.GetBoolean, x => x.SetBoolean);
            _accessorsMap.Map<string>(x => x.GetString, x => x.SetString);
            _accessorsMap.Map<int>(x => x.GetInt32, x => x.SetInt32);
            _accessorsMap.Map<long>(x => x.GetInt64, x => x.SetInt64);
            _accessorsMap.Map<uint>(x => x.GetUInt32, x => x.SetUInt32);
            _accessorsMap.Map<ulong>(x => x.GetUInt64, x => x.SetUInt64);

            _accessorsMap.Map<ReadOnlyCollection<CommandKeyBinding>>(
                x => x.GetRemovedBindings,
                x => x.SetRemovedBindings);
        }

        public VsSettingGetter<T> GetGetter<T>(WritableSettingsStore store)
        {
            if (!CheckLazyInitialization<T>())
            {
                throw new NotSupportedException(
                    $"Attempt to read a value of type {typeof(T)} " +
                    $"from VisualStudio Settings Store. " +
                    $"The type {typeof(T)} is not supported.");
            }

            return _accessorsMap.GetGetter<T>(store);
        }

        public VsSettingSetter<T> GetSetter<T>(WritableSettingsStore store)
        {
            if (!CheckLazyInitialization<T>())
            {
                throw new NotSupportedException(
                    $"Attempt to write a value of type {typeof(T)} " +
                    $"to VisualStudio Settings Store. " +
                    $"The type {typeof(T)} is not supported.");
            }

            return _accessorsMap.GetSetter<T>(store);
        }

        private bool CheckLazyInitialization<T>()
        {
            bool initialized = _accessorsMap.Initialized<T>();

            if (!initialized && typeof(T).IsEnum)
            {
                _accessorsMap.Map<T>(x => x.GetEnum<T>, x => x.SetEnum);

                initialized = true;
            }

            return initialized;
        }
    }
}