using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Vim;

namespace Vim.VisualStudio.Implementation.ConflictingKey
{
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class ConflictingKeyBindingMarginProvider : IVimBufferCreationListener
    {
        private readonly IVim _vim;
        private readonly IKeyBindingService _keyBindingService;
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly IToastNotificationServiceProvider _toastNotificationServiceProvider;

        [ImportingConstructor]
        internal ConflictingKeyBindingMarginProvider(IVim vim, IKeyBindingService keyBindingService, IVimApplicationSettings vimApplicationSettings, IToastNotificationServiceProvider toastNotificationServiceProvider)
        {
            _vim = vim;
            _keyBindingService = keyBindingService;
            _vimApplicationSettings = vimApplicationSettings;
            _toastNotificationServiceProvider = toastNotificationServiceProvider;
        }

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            var wpfTextView = vimBuffer.TextView as IWpfTextView;
            if (wpfTextView == null)
            {
                return;
            }

            var toastNotificationService = _toastNotificationServiceProvider.GetToastNoficationService(wpfTextView);
            new ConflictingKeyBindingMargin(_keyBindingService, _vimApplicationSettings, toastNotificationService);
        }
    }
}
