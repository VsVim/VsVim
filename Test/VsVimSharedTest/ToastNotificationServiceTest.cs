using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Vim.UnitTest;
using Vim.VisualStudio.Implementation.ToastNotification;
using Xunit;

namespace Vim.VisualStudio.UnitTest
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

        [WpfFact]
        public void DisplayAddsNotification()
        {
            var textBlock = new TextBlock();
            _toastNotificationService.Display(new object(), textBlock);
            Assert.Contains(textBlock, _toastControl.ToastNotificationCollection);
        }

        [WpfFact]
        public void HiddenControlDoesNotRemoveNotification()
        {
            var textBlock = new TextBlock();
            _toastNotificationService.Display(new object(), textBlock);
            textBlock.Visibility = Visibility.Collapsed;
            Assert.Contains(textBlock, _toastControl.ToastNotificationCollection);
        }

        [WpfFact]
        public void RemoveInvokesCallback()
        {
            var invokedCallback = false;
            var textBlock = new TextBlock();
            var key = new object();
            _toastNotificationService.Display(key, textBlock, () => { invokedCallback = true; });
            Assert.True(_toastNotificationService.Remove(key));
            Assert.True(invokedCallback);
        }

        /// <summary>
        /// Make sure the code can handle a null callback
        /// </summary>
        [WpfFact]
        public void RemoveNoCallback()
        {
            var textBlock = new TextBlock();
            var key = new object();
            _toastNotificationService.Display(key, textBlock, null);
            Assert.True(_toastNotificationService.Remove(key));
        }

        [WpfFact]
        public void RemoveBadToastNotification()
        {
            Assert.False(_toastNotificationService.Remove(new object()));
        }

        /// <summary>
        /// Make sure the code can handle a Remove call from the remove callback.  This should be a 
        /// no-op but need to verify this
        /// </summary>
        [WpfFact]
        public void RemoveRecursive()
        {
            var invokedCallback = false;
            var key = new object();
            var textBlock = new TextBlock();
            void callback()
            {
                invokedCallback = true;
                Assert.False(_toastNotificationService.Remove(key));
            }

            _toastNotificationService.Display(key, textBlock, callback);
            Assert.True(_toastNotificationService.Remove(key));
            Assert.True(invokedCallback);
        }
    }
}
