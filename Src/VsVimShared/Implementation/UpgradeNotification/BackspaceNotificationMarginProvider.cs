using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows;
using Vim;

namespace VsVim.Implementation.UpgradeNotification
{
    [Export(typeof(IWpfTextViewMarginProvider))]
    [MarginContainer(PredefinedMarginNames.Top)]
    [ContentType(VimConstants.ContentType)]
    [Name(BackspaceNotificationMarginProvider.Name)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class BackspaceNotificationMarginProvider : IWpfTextViewMarginProvider
    {
        internal const string Name = "Backspace Notification Margin";

        private readonly IVim _vim;
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly IEditorFormatMapService _editorFormatMapService;

        [ImportingConstructor]
        internal BackspaceNotificationMarginProvider(IVim vim, IVimApplicationSettings vimApplicationSettings, IEditorFormatMapService editorFormatMapService)
        {
            _vim = vim;
            _vimApplicationSettings = vimApplicationSettings;
            _editorFormatMapService = editorFormatMapService;
        }

        IWpfTextViewMargin IWpfTextViewMarginProvider.CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            // If the notification has occured then there is nothing else to do.  We are done
            if (_vimApplicationSettings.HaveNotifiedBackspaceSetting)
            {
                return null;
            }

            // On the very first IVimBuffer creation the vimrc will be loaded.  Go ahead and 
            // attempt to get / create the buffer to ensure the vimrc load has been attempted
            var wpfTextView = wpfTextViewHost.TextView;
            IVimBuffer vimBuffer;
            if (!_vim.TryGetOrCreateVimBufferForHost(wpfTextView, out vimBuffer))
            {
                return null;
            }

            // If there is no vimrc or the load does allow backspace over start then there 
            // is no need to display the warning
            if (_vim.GlobalSettings.IsBackspaceStart || !_vim.VimRcState.IsLoadSucceeded)
            {
                return null;
            }

            var editorFormatMap = _editorFormatMapService.GetEditorFormatMap(wpfTextView);
            var linkBanner = new LinkBanner();
            linkBanner.MarginName = Name;
            linkBanner.LinkAddress = "https://github.com/jaredpar/VsVim/wiki/FAQ#wiki-backspace";
            linkBanner.LinkText = "FAQ";
            linkBanner.BannerText = "You may want to change the backspace setting in your vimrc";
            linkBanner.Background = editorFormatMap.GetBackgroundBrush(EditorFormatDefinitionNames.Margin, MarginFormatDefinition.DefaultColor);
            linkBanner.CloseClicked += (sender, e) => { _vimApplicationSettings.HaveNotifiedBackspaceSetting = true; };
            return linkBanner;
        }
    }
}
