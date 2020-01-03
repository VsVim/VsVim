using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Settings;
//using Microsoft.VisualStudio.Shell;
using Vim.UI.Wpf;
using Vim;
using Vim.Extensions;

namespace Vim.VisualStudio.Implementation.Settings
{
    [Export(typeof(IVimApplicationSettings))]
    internal sealed class VimApplicationSettings : IVimApplicationSettings
    {
        internal const string CollectionPath = "VsVim";
        internal const string DefaultSettingsName = "DefaultSettings";
        internal const string DisplayControlCharsName = "DisplayControlChars";
        internal const string EnableExternalEditMonitoringName = "EnableExternalEditMonitoring";
        internal const string EnableOutputWindowName = "EnableOutputWindow";
        internal const string VimRcLoadSettingName = "VimRcLoadSetting";
        internal const string HaveUpdatedKeyBindingsName = "HaveUpdatedKeyBindings";
        internal const string HaveNotifiedVimRcLoadName = "HaveNotifiedVimRcLoad";
        internal const string HideMarksName = "HideMarks";
        internal const string HaveNotifiedVimRcErrorsName = "HaveNotifiedVimRcErrors";
        internal const string IgnoredConflictingKeyBindingName = "IgnoredConflictingKeyBinding";
        internal const string RemovedBindingsName = "RemovedBindings";
        internal const string KeyMappingIssueFixedName = "EnterDeletekeyMappingIssue";
        internal const string UseEditorIndentName = "UseEditorIndent";
        internal const string UseEditorDefaultsName = "UseEditorDefaults";
        internal const string UseEditorTabAndBackspaceName = "UseEditorTabAndBackspace";
        internal const string UseEditorCommandMarginName = "UseEditorCommandMargin";
        internal const string CleanMacrosName = "CleanMacros";
        internal const string ReportClipboardErrorsName = "ReportClipboardErrors";
        internal const string LastVersionUsedName = "LastVersionUsed";
        internal const string WordWrapDisplayName = "WordWrapDisplay";
        internal const string ErrorGetFormat = "Cannot get setting {0}";
        internal const string ErrorSetFormat = "Cannot set setting {0}";

        private readonly VimCollectionSettingsStore _settingsStore;

        internal event EventHandler<ApplicationSettingsEventArgs> SettingsChanged;

        internal void OnSettingsChanged()
        {
            SettingsChanged?.Invoke(this, new ApplicationSettingsEventArgs());
        }

        [ImportingConstructor]
        internal VimApplicationSettings(IProtectedOperations protectedOperations)
        {
            _settingsStore = new VimCollectionSettingsStore(protectedOperations);
        }

        internal bool GetBoolean(string propertyName, bool defaultValue) => _settingsStore.GetBoolean(propertyName, defaultValue);

        internal void SetBoolean(string propertyName, bool value)
        {
            _settingsStore.SetBoolean(propertyName, value);
            OnSettingsChanged();
        }

        internal string GetString(string propertyName, string defaultValue) => _settingsStore.GetString(propertyName, defaultValue);

        internal void SetString(string propertyName, string value)
        {
            _settingsStore.SetString(propertyName, value);
            OnSettingsChanged();
        }

        internal T GetEnum<T>(string propertyName, T defaultValue) where T : struct, Enum
        {
            var value = GetString(propertyName, null);
            if (value == null)
            {
                return defaultValue;
            }

            if (Enum.TryParse(value, out T enumValue))
            {
                return enumValue;
            }

            return defaultValue;
        }

        internal void SetEnum<T>(string propertyName, T value) where T : struct, Enum
        {
            SetString(propertyName, value.ToString());
        }

        private ReadOnlyCollection<CommandKeyBinding> GetRemovedBindings()
        {
            var text = GetString(RemovedBindingsName, string.Empty) ?? "";
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

        bool IVimApplicationSettings.EnableOutputWindow
        {
            get { return GetBoolean(EnableOutputWindowName, defaultValue: false); }
            set { SetBoolean(EnableOutputWindowName, value); }
        }

        string IVimApplicationSettings.HideMarks
        {
            get { return GetString(HideMarksName, defaultValue: ""); }
            set { SetString(HideMarksName, value); }
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

        bool IVimApplicationSettings.UseEditorCommandMargin
        {
            get { return GetBoolean(UseEditorCommandMarginName, defaultValue: true); }
            set { SetBoolean(UseEditorCommandMarginName, value); }
        }

        bool IVimApplicationSettings.CleanMacros
        {
            get { return GetBoolean(CleanMacrosName, defaultValue: false); }
            set { SetBoolean(CleanMacrosName, value); }
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

        bool IVimApplicationSettings.HaveNotifiedVimRcErrors
        {
            get { return GetBoolean(HaveNotifiedVimRcErrorsName, defaultValue: true); }
            set { SetBoolean(HaveNotifiedVimRcErrorsName, value); }
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

        string IVimApplicationSettings.LastVersionUsed
        {
            get { return GetString(LastVersionUsedName, null); }
            set { SetString(LastVersionUsedName, value); }
        }

        bool IVimApplicationSettings.ReportClipboardErrors
        {
            get { return GetBoolean(ReportClipboardErrorsName, defaultValue: false); }
            set { SetBoolean(ReportClipboardErrorsName, value); }
        }

        event EventHandler<ApplicationSettingsEventArgs> IVimApplicationSettings.SettingsChanged
        {
            add { SettingsChanged += value; }
            remove { SettingsChanged -= value; }
        }

        #endregion
    }
}
