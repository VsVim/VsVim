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
    [Name(VimRcLoadNotificationMarginProvider.Name)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class VimRcLoadNotificationMarginProvider : IWpfTextViewMarginProvider
    {
        internal const string Name = "VimRc Load Notification Margin";

        private readonly IVim _vim;
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly IEditorFormatMapService _editorFormatMapService;
        private readonly IToastNotificationServiceProvider _toastNotificationServiceProvider;

        [ImportingConstructor]
        internal VimRcLoadNotificationMarginProvider(IVim vim, IVimApplicationSettings vimApplicationSettings, IEditorFormatMapService editorFormatMapService, IToastNotificationServiceProvider toastNotificationServiceProvider)
        {
            _vim = vim;
            _vimApplicationSettings = vimApplicationSettings;
            _editorFormatMapService = editorFormatMapService;
            _toastNotificationServiceProvider = toastNotificationServiceProvider;
        }

        IWpfTextViewMargin IWpfTextViewMarginProvider.CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            // If the notification has occured then there is nothing else to do.  We are done
            if (_vimApplicationSettings.HaveNotifiedVimRcLoad)
            {
                return null;
            }

            // Only display the notification for ITextView instances which are bound to a 
            // VsVim instance 
            var wpfTextView = wpfTextViewHost.TextView;
            IVimBuffer vimBuffer;
            if (!_vim.TryGetOrCreateVimBufferForHost(wpfTextView, out vimBuffer))
            {
                return null;
            }

            if (!_vim.VimRcState.IsLoadSucceeded)
            {
                return null;
            }

            var vimRcPath = ((VimRcState.LoadSucceeded)_vim.VimRcState).Item;
            if (vimRcPath.VimRcKind != VimRcKind.VimRc)
            {
                return null;
            }

            var editorFormatMap = _editorFormatMapService.GetEditorFormatMap(wpfTextView);
            var linkBanner = new LinkBanner();
            linkBanner.MarginName = Name;
            linkBanner.LinkAddress = "https://github.com/jaredpar/VsVim/wiki/FAQ#vimrc";
            linkBanner.LinkText = "FAQ";
            linkBanner.BannerText = "VsVim automatically loaded an existing _vimrc file";
            linkBanner.Background = editorFormatMap.GetBackgroundBrush(EditorFormatDefinitionNames.Margin, MarginFormatDefinition.DefaultColor);
            linkBanner.CloseClicked += (sender, e) => { _vimApplicationSettings.HaveNotifiedVimRcLoad = true; };
            _toastNotificationServiceProvider.GetToastNoficationService(wpfTextView).Display(linkBanner);
            return null;
        }
    }
}
