using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Vim.UnitTest;
using VsVim.Implementation.ToastNotification;
using Xunit;

namespace VsVim.Shared.UnitTest
{
    public sealed class ToastNotificationServiceTest : VimTestBase
    {
        private readonly IWpfTextView _wpfTextView;
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly IToastNotificationService _toastNotificationService;
        private readonly ToastNotificationService _toastNotificationServiceRaw;
        private readonly ToastControl _toastControl;

        public ToastNotificationServiceTest()
        {
            _wpfTextView = CreateTextView();
            _editorFormatMap = CompositionContainer.GetExportedValue<IEditorFormatMapService>().GetEditorFormatMap(_wpfTextView);
            _toastNotificationServiceRaw = new ToastNotificationService(_wpfTextView, _editorFormatMap);
            _toastNotificationService = _toastNotificationServiceRaw;
            _toastControl = _toastNotificationServiceRaw.ToastControl;
        }

        [Fact]
        public void DisplayAddsNotification()
        {
            var textBlock = new TextBlock();
            _toastNotificationService.Display(textBlock);
            Assert.True(_toastControl.ToastNotificationCollection.Contains(textBlock));
        }

        [Fact]
        public void HiddenControlDoesNotRemoveNotification()
        {
            var textBlock = new TextBlock();
            _toastNotificationService.Display(textBlock);
            textBlock.Visibility = Visibility.Collapsed;
            Assert.True(_toastControl.ToastNotificationCollection.Contains(textBlock));
        }
    }
}
