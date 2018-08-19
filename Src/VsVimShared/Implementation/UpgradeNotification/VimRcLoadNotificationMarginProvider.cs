using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Vim;

namespace Vim.VisualStudio.Implementation.UpgradeNotification
{
    [Export(typeof(IVimBufferCreationListener))]
    [ContentType(VimConstants.ContentType)]
    internal sealed class VimRcLoadNotificationMarginProvider : IVimBufferCreationListener
    {
        private readonly IVim _vim;
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly IToastNotificationServiceProvider _toastNotificationServiceProvider;
        private readonly IVsAdapter _vsAdapter;
        private readonly object _notifyToastKey = new object();
        private readonly object _errorToastKey = new object();

        [ImportingConstructor]
        internal VimRcLoadNotificationMarginProvider(IVim vim, IVimApplicationSettings vimApplicationSettings, IToastNotificationServiceProvider toastNotificationServiceProvider, IVsAdapter vsAdapter)
        {
            _vim = vim;
            _vimApplicationSettings = vimApplicationSettings;
            _toastNotificationServiceProvider = toastNotificationServiceProvider;
            _vsAdapter = vsAdapter;
        }

        private void OnNotifyClosed()
        {
            RemoveToastWithKey(_notifyToastKey);
            _vimApplicationSettings.HaveNotifiedVimRcLoad = true;
        }

        private void OnErrorClosed()
        {
            RemoveToastWithKey(_errorToastKey);
            _vimApplicationSettings.HaveNotifiedVimRcErrors = true;
        }

        private void RemoveToastWithKey(object key)
        {
            _vim.VimBuffers
                .Select(x => x.TextView)
                .OfType<IWpfTextView>()
                .ForEach(x => _toastNotificationServiceProvider.GetToastNoficationService(x).Remove(key));
        }

        private void OnViewClick(string[] errors)
        {
            RemoveToastWithKey(_errorToastKey);
            try
            {
                var fileName = Path.GetTempFileName();
                File.WriteAllLines(fileName, errors);
                _vsAdapter.OpenFile(fileName);
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.Message);
            }
        }

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            if (!_vim.VimRcState.IsLoadSucceeded)
            {
                return;
            }

            var wpfTextView = vimBuffer.TextView as IWpfTextView;
            if (wpfTextView == null)
            {
                return;
            }

            var state = ((VimRcState.LoadSucceeded)_vim.VimRcState);

            // If the notification has occured then there is nothing else to do.  We are done
            if (!_vimApplicationSettings.HaveNotifiedVimRcLoad && state.VimRcPath.VimRcKind == VimRcKind.VimRc)
            {
                var linkBanner = new LinkBanner
                {
                    LinkAddress = "https://github.com/jaredpar/VsVim/wiki/FAQ#vimrc",
                    LinkText = "FAQ",
                    BannerText = "VsVim automatically loaded an existing _vimrc file"
                };
                _toastNotificationServiceProvider.GetToastNoficationService(wpfTextView).Display(_notifyToastKey, linkBanner, OnNotifyClosed);
            }

            if (!_vimApplicationSettings.HaveNotifiedVimRcErrors && state.Errors.Length != 0)
            {
                var errorBanner = new ErrorBanner();
                errorBanner.ViewClick += (sender, e) => OnViewClick(state.Errors);
                _toastNotificationServiceProvider.GetToastNoficationService(wpfTextView).Display(_errorToastKey, errorBanner, OnErrorClosed);
            }
        }
    }
}
