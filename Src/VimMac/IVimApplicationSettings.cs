using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Vim;

namespace Vim.VisualStudio
{
    /// <summary>
    /// Arguments passed to application settings changed event handlers
    /// </summary>
    public sealed class ApplicationSettingsEventArgs : EventArgs
    {
    }

    public enum VimRcLoadSetting
    {
        None,
        VsVimRc,
        VimRc,
        Both
    }

    public enum WordWrapDisplay
    {
        Glyph,
        AutoIndent,
        All,
    }

    /// <summary>
    /// Settings specific to the VsVim application.  These specifically don't include Vim specific
    /// settings but instead have items like first usage, first import, etc ... 
    /// </summary>
    public interface IVimApplicationSettings
    {
        /// <summary>
        /// The default settings that should be used 
        /// </summary>
        DefaultSettings DefaultSettings { get; set; }

        /// <summary>
        /// Whether or not control characters should be displayed
        /// </summary>
        bool DisplayControlChars { get; set; }

        /// <summary>
        /// Do we want to track events like external edits in R#, snippets, etc ...
        /// </summary>
        bool EnableExternalEditMonitoring { get; set; }

        /// <summary>
        /// Enable debug logging via the OutputWindow
        /// </summary>
        bool EnableOutputWindow { get; set; }

        /// <summary>
        /// Controls which marks should be hidden from display in the margin.
        /// </summary>
        string HideMarks { get; set; }

        /// <summary>
        /// Do we want to enable vim style processing of tab and backspace
        /// </summary>
        bool UseEditorTabAndBackspace { get; set; }

        /// <summary>
        /// Do we want to enable editor style indentation?
        /// </summary>
        bool UseEditorIndent { get; set; }

        /// <summary>
        /// Do we want to use editor tab size, tabs / spaces or vim?
        /// </summary>
        bool UseEditorDefaults { get; set; }

        /// <summary>
        /// Do we use the editor command margin for status?  If false the status bar at the bottom
        /// of VS will be used 
        /// </summary>
        bool UseEditorCommandMargin { get; set; }

        /// <summary>
        /// Do we keep macro recording clean by disabling intellisense, auto formatting, etc ... 
        /// </summary>
        bool CleanMacros { get; set; }

        /// <summary>
        /// Controls how vimrc files are loaded
        /// </summary>
        VimRcLoadSetting VimRcLoadSetting { get; set; }

        /// <summary>
        /// Controls how word wraps are displayed
        /// </summary>
        WordWrapDisplay WordWrapDisplay { get; set; }

        /// <summary>
        /// The key bindings were updated 
        /// </summary>
        bool HaveUpdatedKeyBindings { get; set; }

        /// <summary>
        /// Have we notified the user about loading their vimrc file?
        /// </summary>
        bool HaveNotifiedVimRcLoad { get; set; }

        /// <summary>
        /// Have we notified the user about errors loading their vimrc file?
        /// </summary>
        bool HaveNotifiedVimRcErrors { get; set; }

        /// <summary>
        /// The conflicting key binding margin was ignored
        /// </summary>
        bool IgnoredConflictingKeyBinding { get; set; }

        /// <summary>
        /// Is the bad key mapping issue introduced in 1.4.0 fixed on this installation
        /// </summary>
        bool KeyMappingIssueFixed { get; set; }

        /// <summary>
        /// The last version of VsVim used on this instance.
        /// </summary>
        string LastVersionUsed { get; set; }

        /// <summary>
        /// Whether to report clipboard errors
        /// </summary>
        bool ReportClipboardErrors { get; set; }

        /// <summary>
        /// The set of CommandKeyBinding that VsVim unbound in the conflicting key dialog
        /// </summary>
        ReadOnlyCollection<CommandKeyBinding> RemovedBindings { get; set; }

        /// <summary>
        /// Raised when a settings changes
        /// </summary>
        event EventHandler<ApplicationSettingsEventArgs> SettingsChanged;
    }
}
