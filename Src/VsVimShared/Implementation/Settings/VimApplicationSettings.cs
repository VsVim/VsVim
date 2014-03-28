using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using EditorUtils;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using Vim.UI.Wpf;
using Vim;

namespace VsVim.Implementation.Settings
{
    [Export(typeof(IVimApplicationSettings))]
    internal sealed class VimApplicationSettings : IVimApplicationSettings
    {
        internal const string CollectionPath = "VsVim";
        internal const string DefaultSettingsName = "DefaultSettings";
        internal const string EnableExternalEditMonitoring = "EnableExternalEditMonitoring";
        internal const string HaveUpdatedKeyBindingsName = "HaveUpdatedKeyBindings";
        internal const string HaveNotifiedBackspaceSetting = "HaveNotifiedBackspaceSetting";
        internal const string IgnoredConflictingKeyBindingName = "IgnoredConflictingKeyBinding";
        internal const string RemovedBindingsName = "RemovedBindings";
        internal const string LegacySettingsMigratedName = "LegacySettingsMigrated";
        internal const string KeyMappingIssueFixedName = "EnterDeletekeyMappingIssue";
        internal const string ErrorGetFormat = "Cannot get setting {0}";
        internal const string ErrorSetFormat = "Cannot set setting {0}";

        private readonly WritableSettingsStore _settingsStore;
        private readonly IVimProtectedOperations _protectedOperations;
        private readonly bool _legacySettingsSupported;

        internal event EventHandler<ApplicationSettingsEventArgs> SettingsChanged;

        internal void OnSettingsChanged()
        {
            var handler = SettingsChanged;
            if (handler != null)
            {
                handler(this, new ApplicationSettingsEventArgs());
            }
        }

        internal bool LegacySettingsMigrated
        {
            get
            {
                if (_legacySettingsSupported)
                {
                    return GetBoolean(LegacySettingsMigratedName, false);
                }

                return true;
            }
            set
            {
                if (_legacySettingsSupported)
                {
                    SetBoolean(LegacySettingsMigratedName, value);
                }
            }
        }

        [ImportingConstructor]
        internal VimApplicationSettings(
            SVsServiceProvider vsServiceProvider,
            ILegacySettings legacySettings,
            IVimProtectedOperations protectedOperations)
            : this(vsServiceProvider.GetVisualStudioVersion(), vsServiceProvider.GetWritableSettingsStore(), protectedOperations)
        {
            var dte = vsServiceProvider.GetService<SDTE, _DTE>();
            MigrateLegacySettings(dte, legacySettings);
        }

        internal VimApplicationSettings(VisualStudioVersion visualStudioVersion, WritableSettingsStore settingsStore, IVimProtectedOperations protectedOperations)
        {
            _settingsStore = settingsStore;
            _protectedOperations = protectedOperations;

            // Legacy settings were only supported on Visual Studio 2010 and 2012.  For any other version there is no
            // need to modify the legacy settings
            switch (visualStudioVersion)
            {
                case VisualStudioVersion.Vs2010:
                case VisualStudioVersion.Vs2012:
                    _legacySettingsSupported = true;
                    break;
                default:
                    // Intentionally do nothing 
                    break;
            }
        }

        /// <summary>
        /// Migrate the legacy settings into our new storage if necessary
        /// </summary>
        internal void MigrateLegacySettings(_DTE dte, ILegacySettings legacySettings)
        {
            if (!LegacySettingsMigrated)
            {
                var legacySettingsUsed = legacySettings.HaveUpdatedKeyBindings || legacySettings.IgnoredConflictingKeyBinding || legacySettings.RemovedBindings.Count > 0;
                if (legacySettingsUsed)
                {
                    var settingsMigrator = new SettingsMigrator(dte, this, legacySettings);
                    settingsMigrator.DoMigration();
                }

                LegacySettingsMigrated = true;
            }
        }

        internal bool GetBoolean(string propertyName, bool defaultValue)
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

        internal void SetBoolean(string propertyName, bool value)
        {
            EnsureCollectionExists();
            try
            {
                _settingsStore.SetBoolean(CollectionPath, propertyName, value);
                OnSettingsChanged();
            }
            catch (Exception e)
            {
                Report(String.Format(ErrorSetFormat, propertyName), e);
            }
        }

        internal string GetString(string propertyName, string defaultValue)
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

        internal void SetString(string propertyName, string value)
        {
            EnsureCollectionExists();
            try
            {
                _settingsStore.SetString(CollectionPath, propertyName, value);
                OnSettingsChanged();
            }
            catch (Exception e)
            {
                Report(String.Format(ErrorSetFormat, propertyName), e);
            }
        }

        internal T GetEnum<T>(string propertyName, T defaultValue) where T : struct
        {
            string value = GetString(propertyName, null);
            if (value == null)
            {
                return defaultValue;
            }

            T enumValue;
            if (Enum.TryParse(value, out enumValue))
            {
                return enumValue;
            }

            return defaultValue;
        }

        internal void SetEnum<T>(string propertyName, T value) where T : struct
        {
            SetString(propertyName, value.ToString());
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

        DefaultSettings IVimApplicationSettings.DefaultSettings
        {
            get { return GetEnum(DefaultSettingsName, defaultValue: DefaultSettings.GVim74); }
            set { SetEnum(DefaultSettingsName, value); }
        }

        bool IVimApplicationSettings.EnableExternalEditMonitoring
        {
            get { return GetBoolean(EnableExternalEditMonitoring, defaultValue: true); }
            set { SetBoolean(EnableExternalEditMonitoring, value); }
        }

        bool IVimApplicationSettings.HaveUpdatedKeyBindings
        {
            get { return GetBoolean(HaveUpdatedKeyBindingsName, defaultValue: false); }
            set { SetBoolean(HaveUpdatedKeyBindingsName, value); }
        }

        bool IVimApplicationSettings.HaveNotifiedBackspaceSetting
        {
            get { return GetBoolean(HaveNotifiedBackspaceSetting, defaultValue: false); }
            set { SetBoolean(HaveNotifiedBackspaceSetting, value); }
        }

        bool IVimApplicationSettings.IgnoredConflictingKeyBinding
        {
            get { return GetBoolean(IgnoredConflictingKeyBindingName, defaultValue: false); }
            set { SetBoolean(IgnoredConflictingKeyBindingName, value); }
        }

        bool IVimApplicationSettings.KeyMappingIssueFixed
        {
            get { return GetBoolean(KeyMappingIssueFixedName, defaultValue: false); }
            set { SetBoolean(KeyMappingIssueFixedName, value); }
        }

        ReadOnlyCollection<CommandKeyBinding> IVimApplicationSettings.RemovedBindings
        {
            get { return GetRemovedBindings(); }
            set { SetRemovedBindings(value); }
        }

        event EventHandler<ApplicationSettingsEventArgs> IVimApplicationSettings.SettingsChanged
        {
            add { SettingsChanged += value; }
            remove { SettingsChanged -= value; }
        }

        #endregion
    }
}
