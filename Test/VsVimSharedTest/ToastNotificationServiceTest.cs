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

        [Fact]
        public void DisplayAddsNotification()
        {
            var textBlock = new TextBlock();
            _toastNotificationService.Display(new object(), textBlock);
            Assert.True(_toastControl.ToastNotificationCollection.Contains(textBlock));
        }

        [Fact]
        public void HiddenControlDoesNotRemoveNotification()
        {
            var textBlock = new TextBlock();
            _toastNotificationService.Display(new object(), textBlock);
            textBlock.Visibility = Visibility.Collapsed;
            Assert.True(_toastControl.ToastNotificationCollection.Contains(textBlock));
        }

        [Fact]
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
        [Fact]
        public void RemoveNoCallback()
        {
            var textBlock = new TextBlock();
            var key = new object();
            _toastNotificationService.Display(key, textBlock, null);
            Assert.True(_toastNotificationService.Remove(key));
        }

        [Fact]
        public void RemoveBadToastNotification()
        {
            Assert.False(_toastNotificationService.Remove(new object()));
        }

        /// <summary>
        /// Make sure the code can handle a Remove call from the remove callback.  This should be a 
        /// no-op but need to verify this
        /// </summary>
        [Fact]
        public void RemoveRecursive()
        {
            var invokedCallback = false;
            var key = new object();
            var textBlock = new TextBlock();
            Action callback = () =>
            {
                invokedCallback = true;
                Assert.False(_toastNotificationService.Remove(key));
            };

            _toastNotificationService.Display(key, textBlock, callback);
            Assert.True(_toastNotificationService.Remove(key));
            Assert.True(invokedCallback);
        }
    }
}
