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
    [Export(typeof(IVimBufferCreationListener))]
    [ContentType(VimConstants.ContentType)]
    internal sealed class VimRcLoadNotificationMarginProvider : IVimBufferCreationListener
    {
        private readonly IVim _vim;
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly IToastNotificationServiceProvider _toastNotificationServiceProvider;

        [ImportingConstructor]
        internal VimRcLoadNotificationMarginProvider(IVim vim, IVimApplicationSettings vimApplicationSettings, IToastNotificationServiceProvider toastNotificationServiceProvider)
        {
            _vim = vim;
            _vimApplicationSettings = vimApplicationSettings;
            _toastNotificationServiceProvider = toastNotificationServiceProvider;
        }

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            // If the notification has occured then there is nothing else to do.  We are done
            if (_vimApplicationSettings.HaveNotifiedVimRcLoad)
            {
                return;
            }

            var vimRcPath = ((VimRcState.LoadSucceeded)_vim.VimRcState).Item;
            if (vimRcPath.VimRcKind != VimRcKind.VimRc)
            {
                return;
            }

            var wpfTextView = vimBuffer.TextView as IWpfTextView;
            if (wpfTextView == null)
            {
                return;
            }

            var linkBanner = new LinkBanner();
            linkBanner.LinkAddress = "https://github.com/jaredpar/VsVim/wiki/FAQ#vimrc";
            linkBanner.LinkText = "FAQ";
            linkBanner.BannerText = "VsVim automatically loaded an existing _vimrc file";
            linkBanner.CloseClicked += (sender, e) => { _vimApplicationSettings.HaveNotifiedVimRcLoad = true; };
            _toastNotificationServiceProvider.GetToastNoficationService(wpfTextView).Display(new object(), linkBanner);
        }
    }
}
