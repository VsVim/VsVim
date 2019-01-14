using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;

using Vim.Extensions;

namespace Vim.VisualStudio.Implementation.Settings
{
    [Export(typeof(IVimApplicationSettings))]
    internal sealed class VimApplicationSettings : IVimApplicationSettings
    {
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
        internal const string LastVersionUsedName = "LastVersionUsed";
        internal const string WordWrapDisplayName = "WordWrapDisplay";

        private readonly ISettingsStore _settingsStore;

        private readonly ReadOnlyCollection<CommandKeyBinding> _emptyBindingsList;

        [ImportingConstructor]
        internal VimApplicationSettings([Import(SettingStoreType.CurrentStoreType)]ISettingsStore settingsStore)
        {
            _settingsStore = settingsStore;
            _emptyBindingsList = new CommandKeyBinding[0].ToReadOnlyCollection();
        }

        #region IVimApplicationSettings

        DefaultSettings IVimApplicationSettings.DefaultSettings
        {
            get { return _settingsStore.GetEnumOrDefault(DefaultSettingsName, defaultValue: DefaultSettings.GVim73); }
            set { _settingsStore.SetEnum(DefaultSettingsName, value); }
        }

        VimRcLoadSetting IVimApplicationSettings.VimRcLoadSetting
        {
            get { return _settingsStore.GetEnumOrDefault(VimRcLoadSettingName, defaultValue: VimRcLoadSetting.Both); }
            set { _settingsStore.SetEnum(VimRcLoadSettingName, value); }
        }

        bool IVimApplicationSettings.DisplayControlChars
        {
            get { return _settingsStore.GetBooleanOrDefault(DisplayControlCharsName, defaultValue: true); }
            set { _settingsStore.SetBoolean(DisplayControlCharsName, value); }
        }

        bool IVimApplicationSettings.EnableExternalEditMonitoring
        {
            get { return _settingsStore.GetBooleanOrDefault(EnableExternalEditMonitoringName, defaultValue: true); }
            set { _settingsStore.SetBoolean(EnableExternalEditMonitoringName, value); }
        }

        bool IVimApplicationSettings.EnableOutputWindow
        {
            get { return _settingsStore.GetBooleanOrDefault(EnableOutputWindowName, defaultValue: false); }
            set { _settingsStore.SetBoolean(EnableOutputWindowName, value); }
        }

        string IVimApplicationSettings.HideMarks
        {
            get { return _settingsStore.GetStringOrDefault(HideMarksName, defaultValue: ""); }
            set { _settingsStore.SetString(HideMarksName, value); }
        }

        bool IVimApplicationSettings.UseEditorDefaults
        {
            get { return _settingsStore.GetBooleanOrDefault(UseEditorDefaultsName, defaultValue: true); }
            set { _settingsStore.SetBoolean(UseEditorDefaultsName, value); }
        }

        bool IVimApplicationSettings.UseEditorIndent
        {
            get { return _settingsStore.GetBooleanOrDefault(UseEditorIndentName, defaultValue: true); }
            set { _settingsStore.SetBoolean(UseEditorIndentName, value); }
        }

        bool IVimApplicationSettings.UseEditorTabAndBackspace
        {
            get { return _settingsStore.GetBooleanOrDefault(UseEditorTabAndBackspaceName, defaultValue: true); }
            set { _settingsStore.SetBoolean(UseEditorTabAndBackspaceName, value); }
        }

        bool IVimApplicationSettings.UseEditorCommandMargin
        {
            get { return _settingsStore.GetBooleanOrDefault(UseEditorCommandMarginName, defaultValue: true); }
            set { _settingsStore.SetBoolean(UseEditorCommandMarginName, value); }
        }

        bool IVimApplicationSettings.CleanMacros
        {
            get { return _settingsStore.GetBooleanOrDefault(CleanMacrosName, defaultValue: false); }
            set { _settingsStore.SetBoolean(CleanMacrosName, value); }
        }

        bool IVimApplicationSettings.HaveUpdatedKeyBindings
        {
            get { return _settingsStore.GetBooleanOrDefault(HaveUpdatedKeyBindingsName, defaultValue: false); }
            set { _settingsStore.SetBoolean(HaveUpdatedKeyBindingsName, value); }
        }

        bool IVimApplicationSettings.HaveNotifiedVimRcLoad
        {
            get { return _settingsStore.GetBooleanOrDefault(HaveNotifiedVimRcLoadName, defaultValue: false); }
            set { _settingsStore.SetBoolean(HaveNotifiedVimRcLoadName, value); }
        }

        bool IVimApplicationSettings.HaveNotifiedVimRcErrors
        {
            get { return _settingsStore.GetBooleanOrDefault(HaveNotifiedVimRcErrorsName, defaultValue: true); }
            set { _settingsStore.SetBoolean(HaveNotifiedVimRcErrorsName, value); }
        }

        bool IVimApplicationSettings.IgnoredConflictingKeyBinding
        {
            get { return _settingsStore.GetBooleanOrDefault(IgnoredConflictingKeyBindingName, defaultValue: false); }
            set { _settingsStore.SetBoolean(IgnoredConflictingKeyBindingName, value); }
        }

        bool IVimApplicationSettings.KeyMappingIssueFixed
        {
            get { return _settingsStore.GetBooleanOrDefault(KeyMappingIssueFixedName, defaultValue: false); }
            set { _settingsStore.SetBoolean(KeyMappingIssueFixedName, value); }
        }

        WordWrapDisplay IVimApplicationSettings.WordWrapDisplay
        {
            get { return _settingsStore.GetEnumOrDefault(WordWrapDisplayName, defaultValue: WordWrapDisplay.Glyph); }
            set { _settingsStore.SetEnum(WordWrapDisplayName, value); }
        }

        ReadOnlyCollection<CommandKeyBinding> IVimApplicationSettings.RemovedBindings
        {
            get { return _settingsStore.GetBindingsOrDefault(RemovedBindingsName, defaultValue: _emptyBindingsList); }
            set { _settingsStore.SetBindings(RemovedBindingsName, value); }
        }

        string IVimApplicationSettings.LastVersionUsed
        {
            get { return _settingsStore.GetStringOrDefault(LastVersionUsedName, defaultValue: null); }
            set { _settingsStore.SetString(LastVersionUsedName, value); }
        }
    
        #endregion

        event EventHandler<ApplicationSettingsEventArgs> IVimApplicationSettings.SettingsChanged
        {
            add { _settingsStore.SettingsChanged += value; }
            remove { _settingsStore.SettingsChanged -= value; }
        }
    }
}
