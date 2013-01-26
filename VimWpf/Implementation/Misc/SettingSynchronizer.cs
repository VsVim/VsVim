using System;
using System.ComponentModel.Composition;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text.Editor;
using EditorUtils;

namespace Vim.UI.Wpf.Implementation.Misc
{
    /// <summary>
    /// Used to synchronize certain Vim setting changes with the equivalent WPF ones
    /// </summary>
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class SettingSynchronizer : IVimBufferCreationListener
    {
        IProtectedOperations _protectedOperations;

        [ImportingConstructor]
        internal SettingSynchronizer([EditorUtilsImport] IProtectedOperations protectedOperations)
        {
            _protectedOperations = protectedOperations;
        }

        private void OnSettingChanged(IVimBuffer vimBuffer, SettingEventArgs args)
        {
            var setting = args.Setting;
            if (setting.Name == WindowSettingNames.CursorLineName)
            {
                SyncVimToEditor(vimBuffer);
            }
        }

        private void SyncVimToEditor(IVimBuffer vimBuffer)
        {
            var options = vimBuffer.TextView.Options;
            if (options == null)
            {
                return;
            }

            options.SetOptionValue(DefaultWpfViewOptions.EnableHighlightCurrentLineId, vimBuffer.WindowSettings.CursorLine);
        }

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            vimBuffer.WindowSettings.SettingChanged += (_, args) => OnSettingChanged(vimBuffer, args);

            // On startup enforce the vim cursor line setting over any Visual Studio default.  This is important
            // because the default changes between Vs 2010 (off) and Vs 2012 (on).  Need to be consistent here and
            // choose the Vim setting
            Action action = () => SyncVimToEditor(vimBuffer);
            _protectedOperations.BeginInvoke(action, DispatcherPriority.Loaded);
        }
    }
}
