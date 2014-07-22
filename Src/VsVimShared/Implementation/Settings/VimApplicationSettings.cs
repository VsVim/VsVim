using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using EditorUtils;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Vim.UI.Wpf;
using Vim;

namespace Vim.VisualStudio.Implementation.Settings
{
    [Export(typeof(IVimApplicationSettings))]
    internal sealed class VimApplicationSettings : IVimApplicationSettings
    {
        internal const string CollectionPath = "VsVim";
        internal const string DefaultSettingsName = "DefaultSettings";
        internal const string DisplayControlCharsName = "DisplayControlChars";
        internal const string EnableExternalEditMonitoringName = "EnableExternalEditMonitoring";
        internal const string VimRcLoadSettingName = "VimRcLoadSetting";
        internal const string HaveUpdatedKeyBindingsName = "HaveUpdatedKeyBindings";
        internal const string HaveNotifiedVimRcLoadName = "HaveNotifiedVimRcLoad";
        internal const string IgnoredConflictingKeyBindingName = "IgnoredConflictingKeyBinding";
        internal const string RemovedBindingsName = "RemovedBindings";
        internal const string KeyMappingIssueFixedName = "EnterDeletekeyMappingIssue";
        internal const string UseEditorIndentName = "UseEditorIndent";
        internal const string UseEditorDefaultsName = "UseEditorDefaults";
        internal const string UseEditorTabAndBackspaceName = "UseEditorTabAndBackspace";
        internal const string WordWrapDisplayName = "WordWrapDisplay";
        internal const string ErrorGetFormat = "Cannot get setting {0}";
        internal const string ErrorSetFormat = "Cannot set setting {0}";

        private readonly WritableSettingsStore _settingsStore;
        private readonly IVimProtectedOperations _protectedOperations;

        internal event EventHandler<ApplicationSettingsEventArgs> SettingsChanged;

        internal void OnSettingsChanged()
        {
            var handler = SettingsChanged;
            if (handler != null)
            {
                handler(this, new ApplicationSettingsEventArgs());
            }
        }

        [ImportingConstructor]
        internal VimApplicationSettings(
            SVsServiceProvider vsServiceProvider,
            IVimProtectedOperations protectedOperations)
            : this(vsServiceProvider.GetVisualStudioVersion(), vsServiceProvider.GetWritableSettingsStore(), protectedOperations)
        {

        }

        internal VimApplicationSettings(VisualStudioVersion visualStudioVersion, WritableSettingsStore settingsStore, IVimProtectedOperations protectedOperations)
        {
            _settingsStore = settingsStore;
            _protectedOperations = protectedOperations;
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
            get { return GetEnum(DefaultSettingsName, defaultValue: DefaultSettings.GVim73); }
            set { SetEnum(DefaultSettingsName, value); }
        }

        VimRcLoadSetting IVimApplicationSettings.VimRcLoadSetting
        {
            get { return GetEnum(VimRcLoadSettingName, defaultValue: VimRcLoadSetting.Both); }
            set { SetEnum(VimRcLoadSettingName, value); }
        }

        bool IVimApplicationSettings.DisplayControlChars
        {
            get { return GetBoolean(DisplayControlCharsName, defaultValue: true); }
            set { SetBoolean(DisplayControlCharsName, value); }
        }

        bool IVimApplicationSettings.EnableExternalEditMonitoring
        {
            get { return GetBoolean(EnableExternalEditMonitoringName, defaultValue: true); }
            set { SetBoolean(EnableExternalEditMonitoringName, value); }
        }

        bool IVimApplicationSettings.UseEditorDefaults
        {
            get { return GetBoolean(UseEditorDefaultsName, defaultValue: true); }
            set { SetBoolean(UseEditorDefaultsName, value); }
        }

        bool IVimApplicationSettings.UseEditorIndent
        {
            get { return GetBoolean(UseEditorIndentName, defaultValue: true); }
            set { SetBoolean(UseEditorIndentName, value); }
        }

        bool IVimApplicationSettings.UseEditorTabAndBackspace
        {
            get { return GetBoolean(UseEditorTabAndBackspaceName, defaultValue: true); }
            set { SetBoolean(UseEditorTabAndBackspaceName, value); }
        }

        bool IVimApplicationSettings.HaveUpdatedKeyBindings
        {
            get { return GetBoolean(HaveUpdatedKeyBindingsName, defaultValue: false); }
            set { SetBoolean(HaveUpdatedKeyBindingsName, value); }
        }

        bool IVimApplicationSettings.HaveNotifiedVimRcLoad
        {
            get { return GetBoolean(HaveNotifiedVimRcLoadName, defaultValue: false); }
            set { SetBoolean(HaveNotifiedVimRcLoadName, value); }
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

        WordWrapDisplay IVimApplicationSettings.WordWrapDisplay
        {
            get { return GetEnum<WordWrapDisplay>(WordWrapDisplayName, WordWrapDisplay.Glyph); }
            set { SetEnum(WordWrapDisplayName, value); }
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
