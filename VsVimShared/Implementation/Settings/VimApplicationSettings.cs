using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using EditorUtils;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;

namespace VsVim.Implementation.Settings
{
    [Export(typeof(IVimApplicationSettings))]
    internal sealed class VimApplicationSettings : IVimApplicationSettings
    {
        private const string CollectionPath = "VsVim";
        private const string HaveUpdatedKeyBindingsName = "HaveUpdatedKeyBindings";
        private const string IgnoredConflictingKeyBindingName = "IgnoredConflictingKeyBinding";
        private const string RemovedBindingsName = "RemovedBindings";

        private readonly WritableSettingsStore _settingsStore;

        [ImportingConstructor]
        internal VimApplicationSettings(SVsServiceProvider vsServiceProvider)
        {
            var shellSettingsManager = new ShellSettingsManager(vsServiceProvider);
            _settingsStore = shellSettingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
        }

        private void EnsureCollectionExists()
        {
            try
            {
                if (!_settingsStore.CollectionExists(CollectionPath))
                {
                    _settingsStore.CreateCollection(CollectionPath);
                }
            }
            catch
            {
                // TODO: Notify the user
            }
        }

        private bool GetBoolean(string propertyName, bool defaultValue)
        {
            EnsureCollectionExists();
            try
            {
                return _settingsStore.GetBoolean(CollectionPath, propertyName);
            }
            catch
            {
                return defaultValue;
            }
        }

        private void SetBoolean(string propertyName, bool value)
        {
            EnsureCollectionExists();
            try
            {
                _settingsStore.SetBoolean(CollectionPath, propertyName, value);
            }
            catch
            {
                // TODO: Need to notify the user in some way 
            }
        }

        private string GetString(string propertyName, string defaultValue)
        {
            EnsureCollectionExists();
            try
            {
                return _settingsStore.GetString(CollectionPath, propertyName);
            }
            catch
            {
                return defaultValue;
            }
        }

        private void SetString(string propertyName, string value)
        {
            EnsureCollectionExists();
            try
            {
                _settingsStore.SetString(CollectionPath, propertyName, value);
            }
            catch
            {
                // TODO: Need to notify the user in some way 
            }
        }

        private ReadOnlyCollection<CommandKeyBinding> GetRemovedBindings()
        {
            var text = GetString(RemovedBindingsName, string.Empty);
            var list = SettingSerializer.ConvertToCommandKeyBindings(text);
            return list.ToReadOnlyCollectionShallow();
        }

        private void SetRemovedBindings(IEnumerable<CommandKeyBinding> bindings)
        {
            var text = SettingSerializer.ConvertToString(bindings);
            SetString(RemovedBindingsName, text);
        }

        #region IVimApplicationSettings

        bool IVimApplicationSettings.HaveUpdatedKeyBindings
        {
            get { return GetBoolean(HaveUpdatedKeyBindingsName, defaultValue: true); }
            set { SetBoolean(HaveUpdatedKeyBindingsName, value); }
        }

        bool IVimApplicationSettings.IgnoredConflictingKeyBinding
        {
            get { return GetBoolean(IgnoredConflictingKeyBindingName, defaultValue: false); }
            set { SetBoolean(IgnoredConflictingKeyBindingName, value); }
        }

        ReadOnlyCollection<CommandKeyBinding> IVimApplicationSettings.RemovedBindings
        {
            get { return GetRemovedBindings(); }
            set { SetRemovedBindings(value); }
        }

        #endregion
    }
}
