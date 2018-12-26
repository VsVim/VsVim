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

        internal event EventHandler<ApplicationSettingsEventArgs> SettingsChanged;

        [ImportingConstructor]
        internal VimApplicationSettings(ISettingsStore settingsStore)
        {
            _settingsStore = settingsStore;

            _emptyBindingsList = new CommandKeyBinding[0].ToReadOnlyCollection();
        }

        private void OnSettingsChanged(string changedSettingName)
        {
            var eventArgs = new ApplicationSettingsEventArgs(changedSettingName);
            SettingsChanged?.Invoke(this, eventArgs);
        }

        internal void Set<T>(string key, T value)
        {
            _settingsStore.Set(key, value);
            OnSettingsChanged(key);
        }

        internal T Get<T>(string key, T defaultValue)
        {
            return _settingsStore.Get(key, defaultValue);
        }

        #region IVimApplicationSettings

        DefaultSettings IVimApplicationSettings.DefaultSettings
        {
            get { return Get(DefaultSettingsName, defaultValue: DefaultSettings.GVim73); }
            set { Set(DefaultSettingsName, value); }
        }

        VimRcLoadSetting IVimApplicationSettings.VimRcLoadSetting
        {
            get { return Get(VimRcLoadSettingName, defaultValue: VimRcLoadSetting.Both); }
            set { Set(VimRcLoadSettingName, value); }
        }

        bool IVimApplicationSettings.DisplayControlChars
        {
            get { return Get(DisplayControlCharsName, defaultValue: true); }
            set { Set(DisplayControlCharsName, value); }
        }

        bool IVimApplicationSettings.EnableExternalEditMonitoring
        {
            get { return Get(EnableExternalEditMonitoringName, defaultValue: true); }
            set { Set(EnableExternalEditMonitoringName, value); }
        }

        bool IVimApplicationSettings.EnableOutputWindow
        {
            get { return Get(EnableOutputWindowName, defaultValue: false); }
            set { Set(EnableOutputWindowName, value); }
        }

        string IVimApplicationSettings.HideMarks
        {
            get { return Get(HideMarksName, defaultValue: ""); }
            set { Set(HideMarksName, value); }
        }

        bool IVimApplicationSettings.UseEditorDefaults
        {
            get { return Get(UseEditorDefaultsName, defaultValue: true); }
            set { Set(UseEditorDefaultsName, value); }
        }

        bool IVimApplicationSettings.UseEditorIndent
        {
            get { return Get(UseEditorIndentName, defaultValue: true); }
            set { Set(UseEditorIndentName, value); }
        }

        bool IVimApplicationSettings.UseEditorTabAndBackspace
        {
            get { return Get(UseEditorTabAndBackspaceName, defaultValue: true); }
            set { Set(UseEditorTabAndBackspaceName, value); }
        }

        bool IVimApplicationSettings.UseEditorCommandMargin
        {
            get { return Get(UseEditorCommandMarginName, defaultValue: true); }
            set { Set(UseEditorCommandMarginName, value); }
        }

        bool IVimApplicationSettings.CleanMacros
        {
            get { return Get(CleanMacrosName, defaultValue: false); }
            set { Set(CleanMacrosName, value); }
        }

        bool IVimApplicationSettings.HaveUpdatedKeyBindings
        {
            get { return Get(HaveUpdatedKeyBindingsName, defaultValue: false); }
            set { Set(HaveUpdatedKeyBindingsName, value); }
        }

        bool IVimApplicationSettings.HaveNotifiedVimRcLoad
        {
            get { return Get(HaveNotifiedVimRcLoadName, defaultValue: false); }
            set { Set(HaveNotifiedVimRcLoadName, value); }
        }

        bool IVimApplicationSettings.HaveNotifiedVimRcErrors
        {
            get { return Get(HaveNotifiedVimRcErrorsName, defaultValue: true); }
            set { Set(HaveNotifiedVimRcErrorsName, value); }
        }

        bool IVimApplicationSettings.IgnoredConflictingKeyBinding
        {
            get { return Get(IgnoredConflictingKeyBindingName, defaultValue: false); }
            set { Set(IgnoredConflictingKeyBindingName, value); }
        }

        bool IVimApplicationSettings.KeyMappingIssueFixed
        {
            get { return Get(KeyMappingIssueFixedName, defaultValue: false); }
            set { Set(KeyMappingIssueFixedName, value); }
        }

        WordWrapDisplay IVimApplicationSettings.WordWrapDisplay
        {                                 
            get { return Get(WordWrapDisplayName, defaultValue: WordWrapDisplay.Glyph); }
            set { Set(WordWrapDisplayName, value); }
        }

        ReadOnlyCollection<CommandKeyBinding> IVimApplicationSettings.RemovedBindings
        {
            get { return Get(RemovedBindingsName, defaultValue: _emptyBindingsList); }
            set { Set(RemovedBindingsName, value); }
        }

        string IVimApplicationSettings.LastVersionUsed
        {
            get { return Get<string>(LastVersionUsedName, defaultValue: null); }
            set { Set(LastVersionUsedName, value); }
        }

        event EventHandler<ApplicationSettingsEventArgs> IVimApplicationSettings.SettingsChanged
        {
            add { SettingsChanged += value; }
            remove { SettingsChanged -= value; }
        }

        #endregion
    }
}
