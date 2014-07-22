using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.VisualStudio.Implementation.ConflictingKey
{
    internal sealed class ConflictingKeyBindingMargin 
    {
        private readonly IKeyBindingService _keyBindingService;
        private readonly ConflictingKeyBindingMarginControl _control;
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly IToastNotificationService _toastNotificationService;
        private readonly object _toastKey = new object();
        private bool _hasDisplayed;

        internal ConflictingKeyBindingMargin(IKeyBindingService service, IVimApplicationSettings vimApplicationSettings, IToastNotificationService toastNotificationService)
        {
            _keyBindingService = service;
            _vimApplicationSettings = vimApplicationSettings;
            _control = new ConflictingKeyBindingMarginControl();
            _control.ConfigureClick += OnConfigureClick;
            _keyBindingService.ConflictingKeyBindingStateChanged += OnStateChanged;
            _toastNotificationService = toastNotificationService;
            _toastNotificationService.TextView.Closed += OnTextViewClosed;

            OnStateChanged(this, EventArgs.Empty);
        }

        private void Unsubscribe()
        {
            _control.ConfigureClick -= OnConfigureClick;
            _keyBindingService.ConflictingKeyBindingStateChanged -= OnStateChanged;
            _toastNotificationService.TextView.Closed -= OnTextViewClosed;
        }

        private void OnConfigureClick(object sender, EventArgs e)
        {
            _keyBindingService.ResolveAnyConflicts();
            _vimApplicationSettings.IgnoredConflictingKeyBinding = true;
        }

        private void OnTextViewClosed(object sender, EventArgs e)
        {
            Unsubscribe();
        }

        private void OnNotificationClosed()
        {
            _keyBindingService.IgnoreAnyConflicts();
            _vimApplicationSettings.IgnoredConflictingKeyBinding = true;
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            switch (_keyBindingService.ConflictingKeyBindingState)
            {
                case ConflictingKeyBindingState.HasNotChecked:
                case ConflictingKeyBindingState.ConflictsIgnoredOrResolved:
                    if (_hasDisplayed)
                    {
                        _toastNotificationService.Remove(_toastKey);
                    }
                    break;
                case ConflictingKeyBindingState.FoundConflicts:
                    _toastNotificationService.Display(_toastKey, _control, OnNotificationClosed);
                    _hasDisplayed = true;
                    break;
                default:
                    throw new Exception("Enum value unknown");
            }
        }
    }
}
