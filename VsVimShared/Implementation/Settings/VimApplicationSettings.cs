using System;
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
        private const string LegacySettingsMigratedName = "LegacySettingsMigrated";
        private const string ErrorGetFormat = "Cannot get setting {0}";
        private const string ErrorSetFormat = "Cannot set setting {0}";

        private readonly WritableSettingsStore _settingsStore;
        private readonly IProtectedOperations _protectedOperations;

        [ImportingConstructor]
        internal VimApplicationSettings(SVsServiceProvider vsServiceProvider, [EditorUtilsImport] IProtectedOperations protectedOperations)
        {
            var shellSettingsManager = new ShellSettingsManager(vsServiceProvider);
            _settingsStore = shellSettingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
            _protectedOperations = protectedOperations;
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
            catch (Exception e)
            {
                Report("Unable to create the settings collection", e);
            }
        }

        private void Report(string message, Exception e)
        {
            message = message + ": " + e.Message;
            var exception = new Exception(message, e);
            _protectedOperations.Report(exception);
        }

        private bool GetBoolean(string propertyName, bool defaultValue)
        {
            EnsureCollectionExists();
            try
            {
                if (!_settingsStore.PropertyExists(CollectionPath, propertyName))
                {
                    return defaultValue;
                }

                return _settingsStore.GetBoolean(CollectionPath, propertyName);
            }
            catch (Exception e)
            {
                Report(String.Format(ErrorGetFormat, propertyName), e);
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
            catch (Exception e)
            {
                Report(String.Format(ErrorSetFormat, propertyName), e);
            }
        }

        private string GetString(string propertyName, string defaultValue)
        {
            EnsureCollectionExists();
            try
            {
                if (!_settingsStore.PropertyExists(CollectionPath, propertyName))
                {
                    return defaultValue;
                }

                return _settingsStore.GetString(CollectionPath, propertyName);
            }
            catch (Exception e)
            {
                Report(String.Format(ErrorGetFormat, propertyName), e);
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
            catch (Exception e)
            {
                Report(String.Format(ErrorSetFormat, propertyName), e);
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
            get { return GetBoolean(HaveUpdatedKeyBindingsName, defaultValue: false); }
            set { SetBoolean(HaveUpdatedKeyBindingsName, value); }
        }

        bool IVimApplicationSettings.IgnoredConflictingKeyBinding
        {
            get { return GetBoolean(IgnoredConflictingKeyBindingName, defaultValue: false); }
            set { SetBoolean(IgnoredConflictingKeyBindingName, value); }
        }

        bool IVimApplicationSettings.LegacySettingsMigrated
        {
            get { return GetBoolean(LegacySettingsMigratedName, defaultValue: false); }
            set { SetBoolean(LegacySettingsMigratedName, value); }
        }

        ReadOnlyCollection<CommandKeyBinding> IVimApplicationSettings.RemovedBindings
        {
            get { return GetRemovedBindings(); }
            set { SetRemovedBindings(value); }
        }

        #endregion
    }
}
