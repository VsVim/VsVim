using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf.Implementation
{
    /// <summary>
    /// Used to synchronize certain Vim setting changes with the equivalent WPF ones
    /// </summary>
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class SettingSynchronizer : IVimBufferCreationListener
    {
        private void OnSettingChanged(IVimBuffer buffer, SettingEventArgs args)
        {
            var setting = args.Setting;
            if (setting.Name == WindowSettingNames.CursorLineName && buffer.TextView.Options != null)
            {
                buffer.TextView.Options.SetOptionValue(DefaultWpfViewOptions.EnableHighlightCurrentLineId, buffer.WindowSettings.CursorLine);
            }
        }

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            vimBuffer.WindowSettings.SettingChanged += (_, args) => OnSettingChanged(vimBuffer, args);
        }
    }
}
